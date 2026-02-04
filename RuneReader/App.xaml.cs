//using ControlzEx.Theming;
using System.Threading;
using Avalonia;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;


namespace RuneReader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex ;

        // public override void Initialize()
        // {
        //     AvaloniaXamlLoader.Load(this);
        // }

        public override void OnFrameworkInitializationCompleted()
        {
            const string appName = "RuneReader";
            bool createdNew;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _mutex = new Mutex(true, appName, out createdNew);
                // Equivalent to "create MainWindow and show it"
                desktop.MainWindow = new MainWindow();

                if (!createdNew)
                {
                    // App is already running! Exiting the application
                    var box = MessageBoxManager
                        .GetMessageBoxStandard("RuneReader", "An instance of the application is already running.", ButtonEnum.Ok);
                     box.ShowWindowAsync();
                
                    _mutex = null;
                    desktop.Shutdown();
            
                }
                

                // Equivalent-ish to Startup/Exit hooks (optional)
                desktop.Startup += (_, e) =>
                {
                    var args = e.Args; // command-line args
                    // init stuff here
                };

                desktop.Exit += (_, e) =>
                {
                    // cleanup here
                    if (_mutex != null)
                    {
                        _mutex.ReleaseMutex();
                        _mutex = null;
                    }
                };
            }
            base.OnFrameworkInitializationCompleted();            




//            ThemeManager.Current.ChangeTheme(this, "Dark.Blue");

//            ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
//            ThemeManager.Current.SyncTheme();

       

        }



    }
}
