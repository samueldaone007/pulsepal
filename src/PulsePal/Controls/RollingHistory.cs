using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace PulsePal.Controls;

/// <summary>
/// A deliberately small, fixed-capacity series. Keeping the last 60 samples
/// makes every sparkline a true one-minute window.
/// </summary>
public sealed class RollingHistory : ObservableCollection<double>
{
    public RollingHistory(int capacity = 60)
    {
        Capacity = Math.Max(2, capacity);
    }

    public int Capacity { get; }

    public void Push(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return;
        }

        Add(value);
        while (Count > Capacity)
        {
            RemoveAt(0);
        }
    }
}
