using System.Windows;
using System.Diagnostics;

namespace WatchMark.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Add global exception handlers
        this.DispatcherUnhandledException += (s, args) =>
        {
            Debug.WriteLine($"FATAL ERROR: {args.Exception}");
            System.Windows.MessageBox.Show($"Application Error:\n\n{args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}", 
                "WatchMark Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Debug.WriteLine($"FATAL UNHANDLED: {ex}");
            System.Windows.MessageBox.Show($"Fatal Error:\n\n{ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}", 
                "WatchMark Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }
}
