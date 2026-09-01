using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Watchers;

public interface IVolumeWatcher
{
    char DriveLetter { get; }
    bool IsRunning { get; }
    event Action<FileChangeEvent>? ChangeDetected;
    void Start();
    void Stop();
}
