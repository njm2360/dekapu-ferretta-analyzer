using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DekapuFerrettaAnalyzer.Models;
using DekapuFerrettaAnalyzer.Services;

namespace DekapuFerrettaAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly ShotType[] AllShots =
    [
        ShotType.None,
        ShotType.Random1,
        ShotType.Random2,
        ShotType.Vertical,
        ShotType.Horizontal,
        ShotType.Cross,
        ShotType.Random4,
        ShotType.VerticalLine,
        ShotType.HorizontalLine,
        ShotType.Giant,
        ShotType.Random8,
    ];

    public ObservableCollection<BoardCellViewModel> Cells { get; } = new();
    public ObservableCollection<ShotResultViewModel> Results { get; } = new();
    public ObservableCollection<LineGeometry> CompletedLineGeometries { get; } = new();
    public ObservableCollection<LineGeometry> ReachLineGeometries { get; } = new();

    [ObservableProperty]
    private string _activeCountText = "";

    [ObservableProperty]
    private bool _isDirty = true;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isAutoCalculate = true;

    [ObservableProperty]
    private string _statusText = "「計算実行」を押してください";

    [ObservableProperty]
    private string _lastElapsedText = "";

    [ObservableProperty]
    private bool _hasCompletedLine;

    [ObservableProperty]
    private string _completedLineMessage = "";

    public MainViewModel()
    {
        for (int row = 0; row < BoardLayout.Size; row++)
        {
            for (int col = 0; col < BoardLayout.Size; col++)
            {
                int number = BoardLayout.NumberAt(row, col);
                bool isCenter = number == BoardLayout.CenterNumber;
                Cells.Add(new BoardCellViewModel(number, isCenter, isCenter, MarkDirty));
            }
        }

        foreach (var shot in AllShots)
            Results.Add(new ShotResultViewModel(shot));

        UpdateActiveCountText();

        if (IsAutoCalculate && !HasCompletedLine)
            _ = CalculateAsync();
    }

    private void MarkDirty()
    {
        IsDirty = true;
        UpdateActiveCountText();
        if (IsAutoCalculate)
        {
            if (!IsCalculating)
                _ = CalculateAsync();
        }
        else
        {
            StatusText = "盤面が変更されました — 「計算実行」を押してください";
        }
    }

    partial void OnIsAutoCalculateChanged(bool value)
    {
        if (value && IsDirty && !IsCalculating)
            _ = CalculateAsync();
        else if (!value && !IsDirty)
            StatusText = "計算完了 (手動モード)";
        else if (!value && IsDirty)
            StatusText = "盤面が変更されました — 「計算実行」を押してください";
    }

    partial void OnHasCompletedLineChanged(bool value)
    {
        if (value)
        {
            // ライン成立中は計算結果を保持しない
            foreach (var r in Results) r.Reset();
            StatusText = "ラインが既に成立しています";
            LastElapsedText = "";
        }
        else if (!IsCalculating)
        {
            // 解除されたら自動で再計算
            _ = CalculateAsync();
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var c in Cells) c.SetActiveSilently(false);
        MarkDirty();
    }

    [RelayCommand]
    private void FillAll()
    {
        foreach (var c in Cells) c.SetActiveSilently(true);
        MarkDirty();
    }

    private uint BuildActiveMask()
    {
        uint mask = 0;
        foreach (var cell in Cells)
            if (cell.IsActive)
                mask |= BoardLayout.Bit(cell.Number);
        return mask;
    }

    private void UpdateActiveCountText()
    {
        uint mask = BuildActiveMask() | BoardLayout.CenterBit;
        int activeCount = System.Numerics.BitOperations.PopCount(mask);
        ActiveCountText = $"アクティブマス数: {activeCount} / 25";
        UpdateBoardAnalysis(mask);
    }

    private void UpdateBoardAnalysis(uint mask)
    {
        var reachCounts = new int[BoardLayout.CellCount];
        bool anyCompleted = false;
        var lineMasks = BoardLayout.LineMasks;

        CompletedLineGeometries.Clear();
        ReachLineGeometries.Clear();

        for (int i = 0; i < lineMasks.Length; i++)
        {
            uint lineMask = lineMasks[i];
            uint inLine = mask & lineMask;
            int popCount = System.Numerics.BitOperations.PopCount(inLine);
            if (popCount == 5)
            {
                anyCompleted = true;
                CompletedLineGeometries.Add(GetLineGeometry(i));
            }
            else if (popCount == 4)
            {
                uint missing = lineMask & ~inLine;
                int idx = System.Numerics.BitOperations.TrailingZeroCount(missing);
                reachCounts[idx]++;
                ReachLineGeometries.Add(GetLineGeometry(i));
            }
        }

        foreach (var cell in Cells)
            cell.ReachLineCount = reachCounts[cell.Number - 1];

        HasCompletedLine = anyCompleted;
        CompletedLineMessage = anyCompleted
            ? "⚠ ラインが既に成立しています — 該当マスを解除してください"
            : "";
    }

    private static LineGeometry GetLineGeometry(int lineIndex)
    {
        const double Cell = 68;
        const double Half = 34;
        const double Start = 15;
        const double End = 5 * Cell - 15; // = 325

        if (lineIndex < 5)
        {
            double y = lineIndex * Cell + Half;
            return new LineGeometry(Start, y, End, y);
        }
        if (lineIndex < 10)
        {
            double x = (lineIndex - 5) * Cell + Half;
            return new LineGeometry(x, Start, x, End);
        }
        if (lineIndex == 10)
            return new LineGeometry(Start, Start, End, End);
        return new LineGeometry(End, Start, Start, End);
    }

    [RelayCommand]
    private async Task CalculateAsync()
    {
        if (IsCalculating) return;
        IsCalculating = true;

        long totalElapsed = 0;
        do
        {
            IsDirty = false;

            if (HasCompletedLine)
            {
                StatusText = "計算スキップ — 成立済みラインを解除してください";
                LastElapsedText = "";
                IsCalculating = false;
                return;
            }

            StatusText = "計算中...";

            uint mask = BuildActiveMask() | BoardLayout.CenterBit;
            var sw = Stopwatch.StartNew();

            var results = await Task.Run(() =>
            {
                var arr = new ShotResult[AllShots.Length];
                for (int i = 0; i < AllShots.Length; i++)
                {
                    arr[i] = ProbabilityCalculator.Calculate(mask, AllShots[i]);
                }
                return arr;
            });

            sw.Stop();
            totalElapsed = sw.ElapsedMilliseconds;

            for (int i = 0; i < Results.Count; i++)
                Results[i].Apply(results[i]);

            var purchasable = Results.Where(r => r.IsPurchasable).ToList();
            if (purchasable.Count > 0)
            {
                double maxExpected = purchasable.Max(r => r.Expected);
                double maxConditional = purchasable.Max(r => r.ConditionalExpected);
                double maxDeltaActive = purchasable.Max(r => r.DeltaActive);
                double maxDeltaReach = purchasable.Max(r => r.DeltaReach);
                double maxTrigger = purchasable.Max(r => r.TriggerCells);
                foreach (var r in purchasable)
                {
                    r.IsBestExpected = r.Expected > 0 && r.Expected == maxExpected;
                    r.IsBestConditional = r.ConditionalExpected > 0 && r.ConditionalExpected == maxConditional;
                    r.IsBestDeltaActive = r.DeltaActive == maxDeltaActive;
                    r.IsBestDeltaReach = r.DeltaReach == maxDeltaReach;
                    r.IsBestTriggerCells = r.TriggerCells > 0 && r.TriggerCells == maxTrigger;
                }
            }
            foreach (var r in Results.Where(r => !r.IsPurchasable))
            {
                r.IsBestExpected = false;
                r.IsBestConditional = false;
                r.IsBestDeltaActive = false;
                r.IsBestDeltaReach = false;
                r.IsBestTriggerCells = false;
            }
        }
        while (IsAutoCalculate && IsDirty);

        IsCalculating = false;
        LastElapsedText = $"計算時間: {totalElapsed} ms";
        StatusText = IsAutoCalculate ? "計算完了 (自動モード)" : "計算完了";
    }
}
