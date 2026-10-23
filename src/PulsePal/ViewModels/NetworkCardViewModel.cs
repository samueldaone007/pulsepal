using CommunityToolkit.Mvvm.ComponentModel;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections.ObjectModel;

namespace PulsePal.ViewModels;

public partial class NetworkCardViewModel : MetricCardViewModel
{
    private readonly IMetricsService _metricsService;
    private bool _updatingAdapters;

    public NetworkCardViewModel(IMetricsService metricsService)
    {
        _metricsService = metricsService;
        ValueUnit = "Mb/s";
        ValueFormat = "0.0";
        Detail = "↑ n/a";
        SecondaryDetail = "no active adapter";
        StatUnit = " Mb/s";
    }

    public ObservableCollection<NetworkAdapterOption> NetworkAdapters { get; } = [];

    [ObservableProperty]
    private NetworkAdapterOption? _selectedAdapter;

    partial void OnSelectedAdapterChanged(NetworkAdapterOption? value)
    {
        if (!_updatingAdapters && value is not null)
        {
            _metricsService.SelectNetworkAdapter(value.Id);
        }
    }

    public void Apply(SystemMetricSnapshot snapshot)
    {
        UpdateAdapterChoices(snapshot.NetworkAdapters, snapshot.NetworkAdapterId);

        if (snapshot.NetworkDownBytesPerSecond is double down &&
            snapshot.NetworkUpBytesPerSecond is double up)
        {
            var downMbps = down * 8 / 1_000_000d;
            var upMbps = up * 8 / 1_000_000d;
            Value = downMbps;
            ValueFormat = downMbps switch
            {
                < 1 => "0.00",
                < 10 => "0.0",
                _ => "0"
            };
            Detail = $"↑ {FormatRate(up * 8)}";
            SecondaryDetail = snapshot.NetworkAdapterName ?? "active adapter";
            Push(downMbps + upMbps);
            IsAlert = false;
        }
        else
        {
            Value = double.NaN;
            Detail = "↑ n/a";
            SecondaryDetail = snapshot.NetworkAdapterName ?? "no active adapter";
        }
    }

    private void UpdateAdapterChoices(IReadOnlyList<NetworkAdapterOption> adapters, string? activeId)
    {
        var currentIds = NetworkAdapters.Select(a => a.Id).ToArray();
        var nextIds = adapters.Select(a => a.Id).ToArray();
        var listChanged = !currentIds.SequenceEqual(nextIds, StringComparer.OrdinalIgnoreCase);

        _updatingAdapters = true;
        try
        {
            if (listChanged)
            {
                NetworkAdapters.Clear();
                foreach (var adapter in adapters)
                {
                    NetworkAdapters.Add(adapter);
                }
            }

            var target = adapters.FirstOrDefault(a =>
                             string.Equals(a.Id, activeId, StringComparison.OrdinalIgnoreCase))
                         ?? adapters.FirstOrDefault();

            if (!ReferenceEquals(SelectedAdapter, target) &&
                !string.Equals(SelectedAdapter?.Id, target?.Id, StringComparison.OrdinalIgnoreCase))
            {
                SelectedAdapter = target;
            }
        }
        finally
        {
            _updatingAdapters = false;
        }
    }

    private static string FormatRate(double bitsPerSecond)
    {
        if (double.IsNaN(bitsPerSecond) || double.IsInfinity(bitsPerSecond) || bitsPerSecond < 0)
        {
            return "n/a";
        }

        string[] units = ["b/s", "Kb/s", "Mb/s", "Gb/s"];
        var value = bitsPerSecond;
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        return unit >= 2 ? $"{value:0.0} {units[unit]}" : $"{value:0} {units[unit]}";
    }
}
