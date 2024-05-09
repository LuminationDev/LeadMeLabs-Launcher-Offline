using System.Windows;
using OfflineInstaller.Components._managers;
using OfflineInstaller.Components._notification;
using OfflineInstaller.Components._utils;
using OfflineInstaller.MVC.View;

namespace OfflineInstaller.MVC.Controller;

public class MainController
{
    public static bool CanAccessVultr;

    public bool StartUp()
    {
        // Check if in admin mode first
        if (!SystemManager.IsRunningAsAdmin())
        {
            MockConsole.WriteLine("This program requires admin privileges.");
            MockConsole.WriteLine("Please run the program as an administrator.");
            return false;
        }
        
        CanAccessVultr = NetworkManager.CheckVultrAccess();
        MockConsole.WriteLine(CanAccessVultr
            ? "Programs downloading from Vultr."
            : "Programs downloading from Heroku.");
        
        CheckForNetworkConnection();
        App.SetWindowTitle($"Offline Installer - {SystemManager.GetVersionNumber()}");
        return true;
    }
    
    /// <summary>
    /// Check if there is network connection and if heroku can be pinged, otherwise the user
    /// cannot download anything. If there is no internet connection, automatically start up
    /// the internal server ready for the Launcher to contact.
    /// </summary>
    public void CheckForNetworkConnection()
    {
        bool connection = NetworkManager.GetInternetAccess();

        if (MainWindow.Instance != null)
        {
            MainWindow.Instance.NetworkWarning.Visibility = connection ? Visibility.Hidden : Visibility.Visible;
            MainWindow.Instance.Update.IsEnabled = true;
        }

        MockConsole.WriteLine($"Internet connection available: {connection}");
        
        // Start up the internal server if there is not internet.
        if(!connection)
        {
            ServerManager.StartServer();
        }
        else
        {
            MockConsole.WriteLine($"WARNING: Internet detected, server will not auto start.");
        }
    }
}
