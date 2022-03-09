using System.Windows;
using System.Windows.Threading;

namespace LazyLineReader;

public partial class App : Application
{
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (Current.MainWindow.DataContext is MainWindowModel model)
            model.Close();
    }
}
