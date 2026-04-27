using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DekapuFerrettaAnalyzer.ViewModels;

public partial class BoardCellViewModel : ObservableObject
{
    public int Number { get; }
    public bool IsCenter { get; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReach))]
    [NotifyPropertyChangedFor(nameof(IsMultiTrigger))]
    [NotifyPropertyChangedFor(nameof(ReachBadge))]
    private int _reachLineCount;

    public bool IsReach => ReachLineCount >= 1;
    public bool IsMultiTrigger => ReachLineCount >= 2;
    public string ReachBadge => ReachLineCount >= 2 ? ReachLineCount.ToString() : "";

    private readonly Action _onChanged;

    public BoardCellViewModel(int number, bool isCenter, bool initialActive, Action onChanged)
    {
        Number = number;
        IsCenter = isCenter;
        _isActive = initialActive;
        _onChanged = onChanged;
    }

    [RelayCommand]
    private void Toggle()
    {
        if (IsCenter) return;
        IsActive = !IsActive;
        _onChanged();
    }

    public void SetActiveSilently(bool value)
    {
        if (IsCenter) { IsActive = true; return; }
        IsActive = value;
    }
}
