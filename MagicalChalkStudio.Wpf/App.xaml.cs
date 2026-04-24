using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace MagicalChalkStudio
{
    public partial class App : Application
    {
        private DispatcherTimer? _splashTimer;
        private readonly Stopwatch _splashWatch = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Localization.LoadPrefs();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var splash = new SplashWindow();
            splash.Show();
            _splashWatch.Restart();
            _splashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _splashTimer.Tick += (_, __) => OnSplashTick(splash);
            _splashTimer.Start();
        }

        private void OnSplashTick(SplashWindow splash)
        {
            if (_splashWatch.Elapsed < TimeSpan.FromSeconds(10))
                return;
            _splashTimer?.Stop();
            _splashTimer = null;
            try { splash.Close(); } catch { /* ignore */ }
            var main = new MainWindow();
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }
    }
}
