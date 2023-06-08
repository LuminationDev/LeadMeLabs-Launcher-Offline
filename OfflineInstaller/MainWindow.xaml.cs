using OfflineInstaller._notification;
using OfflineInstaller._managers;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OfflineInstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }
        public LoadingWindow? LoadingWindow { get; set; }

        public static string? installerLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly Dictionary<string, TextBlock> textBlockMap;

        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainWindowViewModel();
            this.DataContext = viewModel;
            MockConsole.SetViewModel(viewModel);
            DownloadManager.SetViewModel(viewModel);
            LocationChanged += MainWindow_LocationChanged;
            Instance = this;

            CheckForNetworkConnection();

            textBlockMap = new Dictionary<string, TextBlock>
            {
                { "Launcher", LauncherVersion },
                { "nuc", NucVersion },
                { "station", StationVersion }
            };
            CheckProgramVersions();
        }

        /// <summary>
        /// Event handler for the change in the location of the MainWindow.
        /// Adjusts the position of the LoadingWindow to stay centered on the MainWindow.
        /// </summary>
        /// <param name="sender">The object that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (LoadingWindow != null && LoadingWindow.IsVisible)
            {
                double left = Left + (Width - LoadingWindow.ActualWidth) / 2;
                double top = Top + (Height - LoadingWindow.ActualHeight) / 2;
                LoadingWindow.Left = left;
                LoadingWindow.Top = top;
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
        }

        /// <summary>
        /// Checks the version numbers of the currently stored software.
        /// </summary>
        public void CheckProgramVersions()
        {
            Task.Run(() =>
            {
                string launcherVersion = FileManager.ExtractVersionFromYaml();
                string nucVersion = FileManager.CheckProgramVersion("nuc");
                string stationVersion = FileManager.CheckProgramVersion("station");

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
            textBlockMap.TryGetValue(programName, out TextBlock? textBlock);
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
                case "Start":
                    ServerManager.StartServer();
                    break;
                case "Stop":
                    ServerManager.StopServer();
                    break;
                case "Update":
                    DownloadManager.UpdateAllPrograms(productionMode.IsChecked == true);
                    break;
                default:
                    MockConsole.WriteLine("Unknown action.");
                    break;
            }
        }
    }
}
