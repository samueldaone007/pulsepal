using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace PulsePal.ViewModels;

public enum ProcessSortField
{
    Cpu,
    Memory,
    Name,
    Pid
}

public partial class ProcessesViewModel : ObservableObject
{
    private readonly IProcessService _processService;
    private readonly IDialogService _dialogs;
    private readonly ListCollectionView _itemsView;
    private int _lastSelectedPid = -1;

    public ProcessesViewModel(IProcessService processService, IDialogService dialogs)
    {
        _processService = processService;
        _dialogs = dialogs;

        _itemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(Processes);
        _itemsView.Filter = MatchesSearch;
        _itemsView.CustomSort = new ProcessComparer(this);

        ApplyProcesses(processService.Sample());
    }

    public ObservableCollection<ProcessSample> Processes { get; } = new();

    public ICollectionView ItemsView => _itemsView;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProcessSample? _selectedProcess;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "reading the room...";

    [ObservableProperty]
    private ProcessSortField _sortField = ProcessSortField.Cpu;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private int _visibleCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private double _totalMemoryMb;

    [ObservableProperty]
    private string _footerText = "Pick a row, then end it politely.";

    public string NameSortGlyph => SortField == ProcessSortField.Name ? (SortDescending ? "  ↓" : "  ↑") : string.Empty;

    public string CpuSortGlyph => SortField == ProcessSortField.Cpu ? (SortDescending ? "  ↓" : "  ↑") : string.Empty;

    public string MemorySortGlyph => SortField == ProcessSortField.Memory ? (SortDescending ? "  ↓" : "  ↑") : string.Empty;

    public string PidSortGlyph => SortField == ProcessSortField.Pid ? (SortDescending ? "  ↓" : "  ↑") : string.Empty;

    public string TotalMemoryText => TotalMemoryMb >= 1024
        ? $"{TotalMemoryMb / 1024:0.0} GB"
        : $"{TotalMemoryMb:0} MB";

    public void ApplyProcesses(IReadOnlyList<ProcessSample> samples)
    {
        var selectedPid = SelectedProcess?.Pid ?? _lastSelectedPid;
        var byPid = samples.ToDictionary(p => p.Pid);

        for (var i = Processes.Count - 1; i >= 0; i--)
        {
            if (byPid.Remove(Processes[i].Pid, out var updated))
            {
                Processes[i] = updated;
            }
            else
            {
                Processes.RemoveAt(i);
            }
        }

        foreach (var added in byPid.Values)
        {
            Processes.Add(added);
        }

        TotalCount = Processes.Count;
        TotalMemoryMb = Processes.Sum(p => p.MemoryMb);
        _itemsView.Refresh();

        if (selectedPid >= 0)
        {
            SelectedProcess = Processes.FirstOrDefault(p => p.Pid == selectedPid);
        }

        RefreshCounts();
        UpdateFooter();
    }

    [RelayCommand]
    private void SortBy(string? field)
    {
        if (!Enum.TryParse<ProcessSortField>(field, ignoreCase: true, out var parsed))
        {
            return;
        }

        if (SortField == parsed)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortField = parsed;
            SortDescending = parsed is ProcessSortField.Cpu or ProcessSortField.Memory;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEndSelected))]
    private async Task EndSelectedAsync()
    {
        var process = SelectedProcess;
        if (process is null)
        {
            return;
        }

        if (!process.CanEnd)
        {
            _dialogs.ShowMessage(
                "PULSEPAL / HANDS OFF",
                "Protected process",
                $"{process.Name} is doing protected Windows work, or it is PulsePal itself. Ending it could make the room worse, so this one stays.",
                "Got it");
            return;
        }

        var confirmed = _dialogs.Confirm(
            "PULSEPAL / CONFIRM",
            "End this process?",
            $"{process.Name} (#{process.Pid}) will be asked to leave. Any unsaved work in that app may be lost.",
            "End process",
            "Leave it alone");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        StatusText = $"ending {process.Name}...";

        try
        {
            var result = await Task.Run(() => _processService.EndProcess(process.Pid));

            StatusText = result.Message;

            if (!result.Success)
            {
                _dialogs.ShowMessage(
                    "PULSEPAL / PERMISSION",
                    "Windows said no",
                    result.Message,
                    "Okay");
                return;
            }

            SelectedProcess = null;
            _lastSelectedPid = -1;
            ApplyProcesses(_processService.Sample());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanEndSelected() => !IsBusy && SelectedProcess is { CanEnd: true };

    partial void OnSearchTextChanged(string value)
    {
        _itemsView.Refresh();
        RefreshCounts();
        UpdateFooter();
    }

    partial void OnSortFieldChanged(ProcessSortField value)
    {
        _itemsView.Refresh();
        NotifySortGlyphs();
        RefreshCounts();
    }

    partial void OnSortDescendingChanged(bool value)
    {
        _itemsView.Refresh();
        NotifySortGlyphs();
        RefreshCounts();
    }

    partial void OnSelectedProcessChanged(ProcessSample? value)
    {
        if (value is not null)
        {
            _lastSelectedPid = value.Pid;
        }

        EndSelectedCommand.NotifyCanExecuteChanged();
        UpdateFooter();
    }

    partial void OnIsBusyChanged(bool value)
    {
        EndSelectedCommand.NotifyCanExecuteChanged();
        UpdateFooter();
    }

    partial void OnTotalMemoryMbChanged(double value) => OnPropertyChanged(nameof(TotalMemoryText));

    private bool MatchesSearch(object item)
    {
        if (item is not ProcessSample process)
        {
            return false;
        }

        var query = SearchText.Trim();
        if (query.Length == 0)
        {
            return true;
        }

        return process.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               process.Pid.ToString().Contains(query, StringComparison.Ordinal) ||
               (process.Path?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void RefreshCounts()
    {
        VisibleCount = _itemsView.Cast<ProcessSample>().Count();
    }

    private void UpdateFooter()
    {
        if (IsBusy)
        {
            FooterText = "ending process / one moment";
            return;
        }

        if (SelectedProcess is { } selected)
        {
            var state = selected.CanEnd ? "ready to end" : "protected / hands off";
            FooterText = $"{selected.Name}  #{selected.Pid}  ·  {state}  ·  {selected.MemoryMb:0} MB";
            return;
        }

        FooterText = VisibleCount == TotalCount
            ? $"{VisibleCount} processes  ·  {TotalMemoryText} mapped  ·  pick a row to act"
            : $"{VisibleCount} of {TotalCount} shown  ·  {TotalMemoryText} mapped  ·  filtered";
    }

    private void NotifySortGlyphs()
    {
        OnPropertyChanged(nameof(NameSortGlyph));
        OnPropertyChanged(nameof(CpuSortGlyph));
        OnPropertyChanged(nameof(MemorySortGlyph));
        OnPropertyChanged(nameof(PidSortGlyph));
    }

    private sealed class ProcessComparer(ProcessesViewModel owner) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not ProcessSample left || y is not ProcessSample right)
            {
                return 0;
            }

            var result = owner.SortField switch
            {
                ProcessSortField.Name => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase),
                ProcessSortField.Pid => left.Pid.CompareTo(right.Pid),
                ProcessSortField.Memory => left.MemoryMb.CompareTo(right.MemoryMb),
                _ => left.CpuPercent.CompareTo(right.CpuPercent)
            };

            if (result == 0)
            {
                result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            }

            return owner.SortDescending ? -result : result;
        }
    }
}
