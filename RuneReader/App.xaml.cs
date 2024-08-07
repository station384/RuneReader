﻿using ControlzEx.Theming;
using System.Threading;
using System.Windows;


namespace RuneReader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex ;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "RuneReader";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // App is already running! Exiting the application
                MessageBox.Show("An instance of the application is already running.");
                _mutex = null;
                Application.Current.Shutdown();
            }

//            ThemeManager.Current.ChangeTheme(this, "Dark.Blue");

            ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
            ThemeManager.Current.SyncTheme();

            base.OnStartup(e);

        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex = null;
            }

            base.OnExit(e);
        }


    }
}
