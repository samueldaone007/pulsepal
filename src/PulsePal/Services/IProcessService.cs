using PulsePal.Models;

namespace PulsePal.Services;

public interface IProcessService
{
    IReadOnlyList<ProcessSample> Sample();

    (bool Success, string Message) EndProcess(int pid);
}
