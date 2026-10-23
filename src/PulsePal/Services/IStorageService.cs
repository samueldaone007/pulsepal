using PulsePal.Models;

namespace PulsePal.Services;

public interface IStorageService
{
    IReadOnlyList<DriveSnapshot> Sample();
}
