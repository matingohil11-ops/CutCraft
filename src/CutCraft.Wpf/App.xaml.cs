using System.Windows;
using CutCraft.Core.Diagnostics;

namespace CutCraft.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CrashLogger.Initialize();

        DispatcherUnhandledException += (s, args) =>
        {
            CrashLogger.LogError("Dispatcher unhandled exception caught", args.Exception);
            MessageBox.Show(
                $"CutCraft encountered an unexpected issue:\n\n{args.Exception.Message}\n\nThe application will attempt to keep your project safe.",
                "CutCraft Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                CrashLogger.LogError("AppDomain unhandled exception caught", ex);
            }
        };
    }
}
