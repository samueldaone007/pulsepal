using PulsePal.Models;

namespace PulsePal.ViewModels;

public partial class CpuCardViewModel : MetricCardViewModel
{
    public CpuCardViewModel()
    {
        ValueUnit = "%";
    }

    public void Apply(SystemMetricSnapshot snapshot)
    {
        if (snapshot.CpuPercent is double cpu)
        {
            Value = cpu;
            Push(cpu);
            IsAlert = cpu >= 90;
        }
        else
        {
            Value = double.NaN;
            IsAlert = false;
        }

        Detail = snapshot.CpuGhz > 0
            ? $"{snapshot.CpuGhz:0.0} GHz · {snapshot.LogicalCores} cores"
            : $"{snapshot.LogicalCores} logical cores";
    }
}
