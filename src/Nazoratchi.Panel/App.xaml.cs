using System.Windows;
using System.Windows.Threading;

namespace Nazoratchi.Panel;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Dasturda kutilmagan xatolik yuz berdi:\n\n{e.Exception.Message}",
            "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"Kritik xatolik:\n\n{ex.Message}",
                "Kritik Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
