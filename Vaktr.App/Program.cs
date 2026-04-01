using Microsoft.UI.Xaml;
using System.IO;

namespace Vaktr.App;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        StartupTrace.Reset();
        StartupTrace.Write("Program.Main start // launch-cut-v18");
        StartupTrace.Write($"Assembly path: {typeof(Program).Assembly.Location}");
        StartupTrace.Write($"Assembly timestamp: {File.GetLastWriteTime(typeof(Program).Assembly.Location):O}");
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                StartupTrace.WriteException("AppDomain.CurrentDomain.UnhandledException", exception);
            }
            else if (eventArgs.ExceptionObject is not null)
            {
                StartupTrace.Write($"Unhandled exception object: {eventArgs.ExceptionObject}");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            StartupTrace.WriteException("TaskScheduler.UnobservedTaskException", eventArgs.Exception);
        };
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            StartupTrace.Write("Application.Start callback");
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            StartupTrace.Write("SynchronizationContext ready");
            new App();
            StartupTrace.Write("App instance created");
        });
    }
}
