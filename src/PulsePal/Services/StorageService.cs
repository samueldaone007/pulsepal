using PulsePal.Models;
using System.IO;

namespace PulsePal.Services;

public sealed class StorageService : IStorageService
{
    public IReadOnlyList<DriveSnapshot> Sample()
    {
        var drives = new List<DriveSnapshot>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady ||
                        drive.DriveType is not (DriveType.Fixed or DriveType.Removable) ||
                        drive.TotalSize <= 0)
                    {
                        continue;
                    }

                    var free = Math.Max(0, drive.TotalFreeSpace);
                    var used = Math.Max(0, drive.TotalSize - free);
                    var usedPercent = used * 100d / drive.TotalSize;

                    drives.Add(new DriveSnapshot(
                        drive.Name,
                        drive.VolumeLabel ?? string.Empty,
                        drive.DriveFormat ?? "—",
                        drive.DriveType.ToString(),
                        drive.TotalSize,
                        free,
                        Math.Clamp(usedPercent, 0, 100),
                        true));
                }
                catch
                {
                    // A drive can disappear or deny metadata mid-enumeration.
                }
            }
        }
        catch
        {
            // Return whichever drives were read before the failure.
        }

        return drives
            .OrderBy(d => d.DriveType == "Fixed" ? 0 : 1)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
