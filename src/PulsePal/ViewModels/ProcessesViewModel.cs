using CommunityToolkit.Mvvm.ComponentModel;
using PulsePal.Models;
using PulsePal.Services;
using System.Collections.ObjectModel;

namespace PulsePal.ViewModels;

public partial class ProcessesViewModel : ObservableObject
{
    public ObservableCollection<ProcessSample> TopProcesses { get; } = new();

    private readonly IProcessService _processService;

    public ProcessesViewModel(IProcessService processService)
    {
        _processService = processService;
        var samples = processService.Sample();
        foreach (var p in samples.Take(6))
        {
            TopProcesses.Add(p);
        }
    }
}