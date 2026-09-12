using System.Threading;
using System.Windows;

namespace YouTube4KDownloader;

public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        _instance = new Mutex(false, @"Local\BigNim.YouTube4KDownloader");
        try { _ownsInstance = _instance.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsInstance = true; }
        if (!_ownsInstance)
        {
            MessageBox.Show("YouTube4KDownloader is already running.");
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
