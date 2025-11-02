using System.Windows;
using Application = System.Windows.Application;

namespace SteamManifestToggler
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var config = AppConfig.Load() ?? new AppConfigData();

            if (!SteamScanner.IsValidSteamRoot(config.SteamRoot))
            {
                var initial = config.SteamRoot;
                initial ??= SteamScanner.GetDefaultSteamPath();

                var startupWindow = new StartupWindow(initial);
                if (startupWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }

                config.SteamRoot = startupWindow.SelectedRoot;
                AppConfig.Save(config);
            }

            var mainWindow = new MainWindow(config);
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}

