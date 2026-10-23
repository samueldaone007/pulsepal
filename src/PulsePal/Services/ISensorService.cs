using PulsePal.Models;

namespace PulsePal.Services;

public interface ISensorService : IDisposable
{
    SensorSnapshot Sample();
}
