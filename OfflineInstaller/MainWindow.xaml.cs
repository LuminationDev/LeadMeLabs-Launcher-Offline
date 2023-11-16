using OfflineInstaller._notification;
using OfflineInstaller._managers;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OfflineInstaller._pages;

namespace OfflineInstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }
        public LoadingWindow? LoadingWindow { get; set; }
        private ConfirmWindow? ConfirmWindow { get; set; }
        private MainWindowViewModel ViewModel { get; set; }

        public static readonly string? InstallerLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly Dictionary<string, TextBlock> _textBlockMap = new();
        public readonly Dictionary<string, string> VersionMap = new();

        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainWindowViewModel();
            this.DataContext = ViewModel;
            MockConsole.SetViewModel(ViewModel);
            DownloadManager.SetViewModel(ViewModel);
            LocationChanged += MainWindow_LocationChanged;
            Loaded += AskUserToCreateInboundRule;
            Instance = this;

            // Check if in admin mode first
            if (!SystemManager.IsRunningAsAdmin())
            {
                MockConsole.WriteLine("This program requires admin privileges.");
                MockConsole.WriteLine("Please run the program as an administrator.");
                return;
            }

            _textBlockMap = new Dictionary<string, TextBlock>
            {
                { "Launcher", LauncherVersion },
                { "NUC", NucVersion },
                { "Station", StationVersion }
            };

            CheckForNetworkConnection();
            CheckProgramVersions();

            App.SetWindowTitle($"Offline Installer - {SystemManager.GetVersionNumber()}");
        }

        /// <summary>
        /// Event handler for the change in the location of the MainWindow.
        /// Adjusts the position of the LoadingWindow to stay centered on the MainWindow.
        /// </summary>
        /// <param name="sender">The object that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (LoadingWindow is { IsVisible: true })
            {
                double left = Left + (Width - LoadingWindow.ActualWidth) / 2;
                double top = Top + (Height - LoadingWindow.ActualHeight) / 2;
                LoadingWindow.Left = left;
                LoadingWindow.Top = top;
            }
            
            if (ConfirmWindow is { IsVisible: true })
            {
                double left = Left + (Width - ConfirmWindow.ActualWidth) / 2;
                double top = Top + (Height - ConfirmWindow.ActualHeight) / 2;
                ConfirmWindow.Left = left;
                ConfirmWindow.Top = top;
            }
        }

        /// <summary>
        /// Check if there is network connection and if heroku can be pinged, otherwise the user
        /// cannot download anything. If there is no internet connection, automatically start up
        /// the internal server ready for the Launcher to contact.
        /// </summary>
        private void CheckForNetworkConnection()
        {
            bool connection = NetworkManager.GetInternetAccess();

            NetworkWarning.Visibility = connection ? Visibility.Hidden : Visibility.Visible;
            Update.IsEnabled = connection;

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

        /// <summary>
        /// Prompt the user if they wish to create the inbound tcp firewall rule automatically if it is not already
        /// present.
        /// </summary>
        private void AskUserToCreateInboundRule(object? sender, EventArgs e)
        {
            if (FirewallManager.IsPortAllowed()) return;
            
            MockConsole.WriteLine($"WARNING: Port 8088 for TCP connection is not permitted in Inbound Rules. " +
                                  $"Cannot serve programs over the network, only on the local computer");
            
            try
            {
                ViewModel.TitleText = "Would you like to create the Inbound Rule?";
                using ConfirmWindow confirmWindow = new(ViewModel);
                
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.ConfirmWindow = confirmWindow;
                    confirmWindow.Owner = MainWindow.Instance;
                }
                
                var result = confirmWindow.ShowDialog();

                    
                // Await user interaction
                // Check the DialogResult property to get the user's choice
                if (result.HasValue && result.Value)
                {
                    MockConsole.ClearConsole();
                    
                    // Perform actions for confirm
                    MockConsole.WriteLine("Attempting to create inbound rule.");
                    FirewallManager.CreateInboundRule();
                    
                    if (!FirewallManager.IsPortAllowed())
                    {
                        MockConsole.WriteLine($"WARNING: Port 8088 for TCP connection is not permitted in Inbound Rules. " +
                                              $"Cannot serve programs over the network, only on the local computer");
                    }
                    else
                    {
                        MockConsole.WriteLine("Inbound rule created successfully.");
                        CheckForNetworkConnection();
                        CheckProgramVersions();
                    }
                }
                else
                {
                    // Perform actions for cancel
                    MockConsole.WriteLine("User clicked Cancel or closed the window.");
                }

                // Close the confirmWindow when you're done with it
                confirmWindow.Close();

                // Dispose of the confirmWindow and release any associated resources
                confirmWindow.Dispose();
            }
            catch (Exception ex)
            {
                MockConsole.WriteLine($"ERROR: {ex}");
            }
        }

        ///<summary>
        /// Checks the program versions and updates the UI with the version numbers.
        /// If the 'delay' parameter is set to true, it adds a delay before retrieving the versions.
        ///</summary>
        ///<param name="delay">Flag indicating whether to introduce a delay before retrieving the versions.</param>
        private void CheckProgramVersions(bool delay = false)
        {
            Task.Run(() =>
            {
                if (delay)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LauncherVersion.Text = "Loading";
                        NucVersion.Text = "Loading";
                        StationVersion.Text = "Loading";
                    });
                    Task.Delay(1500).Wait();
                }

                string launcherVersion = FileManager.ExtractVersionFromYaml();
                string nucVersion = FileManager.CheckProgramVersion("nuc");
                string stationVersion = FileManager.CheckProgramVersion("station");

                VersionMap["nuc"] = nucVersion;
                VersionMap["station"] = stationVersion;

                // Update the UI with the version numbers
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LauncherVersion.Text = launcherVersion;
                    NucVersion.Text = nucVersion;
                    StationVersion.Text = stationVersion;
                });
            });
        }

        /// <summary>
        /// Retrieves the TextBlock associated with the specified program name from the textBlockMap.
        /// </summary>
        /// <param name="programName">The name of the program.</param>
        /// <returns>The TextBlock associated with the program name, or null if not found.</returns>
        public TextBlock? GetTextBlock(string programName)
        {
            _textBlockMap.TryGetValue(programName, out TextBlock? textBlock);
            return textBlock;
        }

        /// <summary>
        /// Handle a button click, use the Name of the button to determine what action needs to be performed.
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = (Button)sender;

            switch (clickedButton.Name)
            {
                case "Refresh":
                    RotationRefresh.BeginAnimation(RotateTransform.AngleProperty, _animation);
                    CheckProgramVersions(true);
                    break;
                case "Start":
                    ServerManager.StartServer();
                    break;
                case "Stop":
                    ServerManager.StopServer();
                    break;
                case "Update":
                    DownloadManager.UpdateAllPrograms(productionMode.IsChecked == true);
                    break;
                case "Clear":
                    RotationClear.BeginAnimation(RotateTransform.AngleProperty, _animation);
                    MockConsole.ClearConsole();
                    break;
                default:
                    MockConsole.WriteLine($"Unknown action: {clickedButton.Name}.");
                    break;
            }
        }

        /// <summary>
        /// The standard rotation animation for the refresh action.
        /// </summary>
        private readonly DoubleAnimation _animation = new()
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(2),
            RepeatBehavior = new RepeatBehavior(1)
        };

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // For example, show a confirmation dialog
            MessageBoxResult result = MessageBox.Show("Are you sure you want to exit?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            // If the user clicks "No," cancel the closing event
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                // Attempt to stop any open servers
                ServerManager.StopServer();

                // Wait for the server to be fully shutdown
                Task.Delay(1000).Wait();

                // Exit the application
                Application.Current.Shutdown();
            }
        }
    }
}
