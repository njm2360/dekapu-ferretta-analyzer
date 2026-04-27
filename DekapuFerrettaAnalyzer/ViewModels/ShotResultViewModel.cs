using CommunityToolkit.Mvvm.ComponentModel;
using DekapuFerrettaAnalyzer.Models;

namespace DekapuFerrettaAnalyzer.ViewModels;

public partial class ShotResultViewModel : ObservableObject
{
    public ShotType Shot { get; }
    public string Name { get; }
    public bool IsPurchasable { get; }

    [ObservableProperty] private double _expected;
    [ObservableProperty] private string _expectedDisplay = "";

    [ObservableProperty] private double _conditionalExpected;
    [ObservableProperty] private string _conditionalExpectedDisplay = "";

    [ObservableProperty] private double _deltaActive;
    [ObservableProperty] private string _deltaActiveDisplay = "";

    [ObservableProperty] private double _deltaReach;
    [ObservableProperty] private string _deltaReachDisplay = "";

    [ObservableProperty] private double _triggerCells;
    [ObservableProperty] private string _triggerCellsDisplay = "";

    [ObservableProperty] private double _resetCells;
    [ObservableProperty] private string _resetCellsDisplay = "";

    [ObservableProperty] private bool _isBestExpected;
    [ObservableProperty] private bool _isBestConditional;
    [ObservableProperty] private bool _isBestDeltaActive;
    [ObservableProperty] private bool _isBestDeltaReach;
    [ObservableProperty] private bool _isBestTriggerCells;

    public DistributionEntryViewModel[] Distribution { get; }

    public ShotResultViewModel(ShotType shot)
    {
        Shot = shot;
        Name = shot.DisplayName();
        IsPurchasable = shot.IsPurchasable();
        Distribution = new DistributionEntryViewModel[ShotResult.MaxLines];
        for (int i = 0; i < ShotResult.MaxLines; i++)
            Distribution[i] = new DistributionEntryViewModel(i + 1);
    }

    public void Apply(ShotResult result)
    {
        Expected = result.Expected.ToDouble();
        ExpectedDisplay = Expected.ToString("F4");

        if (result.ConditionalExpected.HasValue)
        {
            ConditionalExpected = result.ConditionalExpected.Value.ToDouble();
            ConditionalExpectedDisplay = ConditionalExpected.ToString("F3");
        }
        else
        {
            ConditionalExpected = 0;
            ConditionalExpectedDisplay = "—";
        }

        DeltaActive = result.ExpectedDeltaActive.ToDouble();
        DeltaActiveDisplay = FormatSigned(DeltaActive, "F3");

        DeltaReach = result.ExpectedDeltaReach.ToDouble();
        DeltaReachDisplay = FormatSigned(DeltaReach, "F3");

        TriggerCells = result.ExpectedTriggerCells.ToDouble();
        TriggerCellsDisplay = TriggerCells.ToString("F3");

        ResetCells = result.ExpectedResetCells.ToDouble();
        ResetCellsDisplay = ResetCells.ToString("F3");

        for (int i = 0; i < ShotResult.MaxLines; i++)
            Distribution[i].Apply(result.Distribution[i + 1]);
    }

    private static string FormatSigned(double v, string fmt)
    {
        if (v > 0) return "+" + v.ToString(fmt);
        return v.ToString(fmt);
    }
}

public partial class DistributionEntryViewModel : ObservableObject
{
    public int Lines { get; }
    public string Label { get; }

    [ObservableProperty]
    private double _probability;

    [ObservableProperty]
    private string _probabilityDisplay = "";

    [ObservableProperty]
    private string _probabilityExact = "";

    [ObservableProperty]
    private bool _hasMass;

    public DistributionEntryViewModel(int lines)
    {
        Lines = lines;
        Label = $"L={lines}";
    }

    public void Apply(Fraction value)
    {
        Probability = value.ToDouble();
        ProbabilityDisplay = Probability == 0 ? "—" : Probability.ToString("P2");
        ProbabilityExact = $"P(L={Lines}) = {value.Numerator} / {value.Denominator}";
        HasMass = value.Numerator.Sign > 0;
    }
}
