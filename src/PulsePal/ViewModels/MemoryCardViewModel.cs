using PulsePal.Models;

namespace PulsePal.ViewModels;

public partial class MemoryCardViewModel : MetricCardViewModel
{
    public MemoryCardViewModel()
    {
        ValueUnit = "%";
    }

    public void Apply(SystemMetricSnapshot snapshot)
    {
        if (snapshot.MemoryPercent is double percent)
        {
            Value = percent;
            Push(percent);
            IsAlert = percent >= 90;
        }
        else
        {
            Value = double.NaN;
            IsAlert = false;
        }

        Detail = snapshot.MemoryUsedGb is double used && snapshot.MemoryTotalGb is double total
            ? $"{used:0.0} / {total:0.0} GB"
            : "memory reading n/a";

        SecondaryDetail = snapshot.MemoryPercent is double p
            ? p >= 85 ? "allocators are feasting" : "plenty of breathing room"
            : string.Empty;
    }
}
