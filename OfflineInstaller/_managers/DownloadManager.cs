using Newtonsoft.Json.Linq;
using OfflineInstaller._notification;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace OfflineInstaller._managers
{
    public class DownloadManager
    {
        // Track whether the software is from Development or Production
        protected static string? Mode { get; private set; }
        protected static string? BaseUrl { get; private set; }

#if DEBUG
        private static readonly string LauncherURL = "http://localhost:8084"; //normally 8084
#elif RELEASE
        private static readonly string LauncherURL = "https://electronlauncher.herokuapp.com"; //production version - needs the Launcher.zip added
#endif

        private static MainWindowViewModel? _viewModel;

        public static void SetViewModel(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        ///<summary>
        /// Updates all programs by downloading them based on the specified mode.
        ///</summary>
        ///<param name="isProduction">A boolean value indicating whether to download programs in production mode.</param>
        public static async void UpdateAllPrograms(bool isProduction)
        {
            Mode = isProduction ? "production" : "development";

#if DEBUG
            BaseUrl = "http://localhost:8082"; //For testing purposes only            
#elif RELEASE
            BaseUrl = Mode.Equals("development") ? "https://learninglablauncherdevelopment.herokuapp.com" : "https://learninglablauncher.herokuapp.com";
#endif

            MockConsole.WriteLine($"Downloading software from {Mode}.");

            if (_viewModel == null)
            {
                MockConsole.WriteLine($"Download incomplete please restart the software.");
                return;
            }

            using (LoadingWindow loadingWindow = new(_viewModel))
            {
                MainWindow.Instance.LoadingWindow = loadingWindow;
                loadingWindow.Owner = MainWindow.Instance;
                loadingWindow.Show();

                // Perform necessary operations with the loadingWindow
                //await DownloadSoftware("nuc");
                //await DownloadSoftware("station");
                //await DownloadSoftware("SetVol");
                //await DownloadSoftware("steamcmd");
                await DownloadLauncher();

                // Close the loadingWindow when you're done with it
                loadingWindow.Close();

                // Dispose of the loadingWindow and release any associated resources
                loadingWindow.Dispose();
            }

            MockConsole.WriteLine($"Software downloaded from {Mode}.");
        }

        ///<summary>
        /// Asynchronously downloads the specified software program and extracts it to the destination folder.
        ///</summary>
        ///<param name="software">The name of the software program to download.</param>
        ///<returns>A Task representing the asynchronous operation.</returns>
        private static async Task DownloadSoftware(string software)
        {
            if(BaseUrl == null)
            {
                MockConsole.WriteLine($"BaseUrl is null, cannot download {char.ToUpper(software[0]) + software[1..]}.");
                return;
            }

            string fileName;
            if (software.Equals("station"))
            {
                fileName = "Station";
            } else if (software.Equals("nuc"))
            {
                fileName = "NUC";
            } else
            {
                fileName = software;
            }

            await DownloadProgram(BaseUrl, software, fileName);

            if(software.Equals("station") || software.Equals("nuc"))
            {
                // Unzip the launcher for the internal server
                _viewModel.DownloadText = $"Extracting {char.ToUpper(software[0]) + software[1..]} software.";

                string savePath = @$"{MainWindow.installerLocation}\_programs\{fileName}.zip";
                await FileManager.UnzipFolderAsync(savePath, $@"{Path.GetDirectoryName(savePath)}\{fileName}", Progress);
            }
        }

        /// <summary>
        /// Download the nuc or station software, the URL depends on the mode and software passed into the constructor.
        /// </summary>
        private static async Task DownloadProgram(string serverUrl, string software, string overrideFileName = "")
        {
            if (_viewModel != null)
            {
                _viewModel.DownloadText = $"Downloading {char.ToUpper(software[0]) + software[1..]} software.";
            }

            MockConsole.WriteLine($"Downloading {char.ToUpper(software[0]) + software[1..]}.");

            string downloadUrl = $"{serverUrl}/program-{software}"; // Specify the URL of the zip folder to download

            string name = string.IsNullOrEmpty(overrideFileName) ? software : overrideFileName;
            string savePath = @$"{MainWindow.installerLocation}\_programs\{name}.zip"; // Specify the path where you want to save the downloaded file

            await Download(downloadUrl, savePath);
        }

        ///<summary>
        /// Gets an instance of IProgress to track the progress of a task.
        ///</summary>
        ///<returns>An instance of IProgress that reports the progress percentage.</returns>
        private static IProgress<double> Progress
        {
            // Create an instance of Progress<int> to track the progress
            get => new Progress<double>(progressPercentage =>
            {
                // Update your UI or perform any action based on the progress percentage
                if (_viewModel != null)
                {
                    _viewModel.DownloadProgressText = $"{progressPercentage}%";
                    _viewModel.DownloadProgress = progressPercentage;
                }
            });
        }

        /// <summary>
        /// Downloads a file from the specified URL and saves it to the specified path.
        /// Reports the download progress using the provided progress callback.
        /// </summary>
        /// <param name="downloadUrl">The URL of the file to download.</param>
        /// <param name="savePath">The local file system path where the downloaded file should be saved.</param>
        /// <returns>A task representing the asynchronous download operation.</returns>
        private static async Task Download(string downloadUrl, string savePath)
        {
            IProgress<double> progress = Progress;

            using HttpClient client = new();
            try
            {
                HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                {
                    using FileStream fileStream = new(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    
                    byte[] buffer = new byte[8192]; // Set the buffer size according to your needs
                    int bytesRead;
                    long totalBytesRead = 0;
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;

                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalBytesRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            double progressPercentage = (double)((totalBytesRead * 100) / totalBytes);
                            progress?.Report(progressPercentage);
                        }
                    }
                }

                MockConsole.WriteLine("File downloaded successfully.");
            }
            catch (Exception ex)
            {
                MockConsole.WriteLine("An error occurred during file download: " + ex.Message);
            }
        }


        #region Electron Download
        /// <summary>
        /// Downloads the Launcher software by retrieving the folder content from the server and recursively downloading files and nested folders.
        /// Reports the overall download progress using the provided progress callback.
        /// </summary>
        /// <returns>A task representing the asynchronous download operation.</returns>
        private static async Task DownloadLauncher()
        {
            IProgress<double> progress = Progress;

            if (_viewModel != null)
            {
                _viewModel.DownloadText = $"Downloading Launcher software.";
            }

            MockConsole.WriteLine($"Downloading Launcher.");

            string serverUrl = $"{LauncherURL}/download-folder";
            string destinationPath = @$"{MainWindow.installerLocation}\_programs\electron-launcher\";

            using HttpClient client = new HttpClient();
            try
            {
                HttpResponseMessage response = await client.GetAsync(serverUrl, HttpCompletionOption.ResponseContentRead);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    JArray folderContentArray = JArray.Parse(responseContent);

                    // Create the destination folder if it doesn't exist
                    Directory.CreateDirectory(destinationPath);

                    // Download files and recursively download nested folders
                    foreach (JToken item in folderContentArray)
                    {
                        JObject? itemObject = item as JObject;
                        string? itemType = itemObject?["type"]?.ToString();
                        string? itemName = itemObject?["name"]?.ToString();

                        if (itemType == null) continue;
                        if (itemName == null) continue;

                        if (itemType == "file")
                        {
                            // Item is a file
                            await DownloadFile(client, serverUrl, itemName, destinationPath);
                        }
                        else if (itemType == "folder")
                        {
                            // Item is a nested folder
                            await DownloadFolder(client, serverUrl, itemName, destinationPath, progress);
                        }
                    }

                    MockConsole.WriteLine("Electron folder downloaded successfully.");
                }
                else
                {
                    MockConsole.WriteLine("Failed to download folder. Server returned status code: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                MockConsole.WriteLine("An error occurred: " + ex.Message);
            }
        }

        /// <summary>
        /// Downloads a file from the specified URL and saves it to the specified destination path.
        /// </summary>
        /// <param name="client">The HttpClient instance used for making HTTP requests.</param>
        /// <param name="baseUrl">The base URL of the server where the file is located.</param>
        /// <param name="file">The name of the file to download.</param>
        /// <param name="destinationPath">The local file system path where the downloaded file should be saved.</param>
        /// <returns>A task representing the asynchronous download operation.</returns>
        private static async Task DownloadFile(HttpClient client, string baseUrl, string file, string destinationPath)
        {
            string sanitizedFileName = SanitizeFileName(file);
            string filePath = Path.Combine(destinationPath, sanitizedFileName);

            using HttpResponseMessage fileResponse = await client.GetAsync(baseUrl + "/" + file);
            using Stream contentStream = await fileResponse.Content.ReadAsStreamAsync();
            using Stream fileStream = File.Create(filePath);
            await contentStream.CopyToAsync(fileStream);
        }

        /// <summary>
        /// Recursively downloads a folder and its contents from the specified base URL and saves them to the specified destination path.
        /// Reports the overall download progress using the provided progress callback.
        /// </summary>
        /// <param name="client">The HttpClient instance used for making HTTP requests.</param>
        /// <param name="baseUrl">The base URL of the server where the folder is located.</param>
        /// <param name="folderName">The name of the folder to download.</param>
        /// <param name="destinationPath">The local file system path where the downloaded folder should be saved.</param>
        /// <param name="progress">An instance of IProgress&lt;double&gt; to track the overall download progress.</param>
        /// <returns>A task representing the asynchronous download operation.</returns>
        private static async Task DownloadFolder(HttpClient client, string baseUrl, string folderName, string destinationPath, IProgress<double> progress)
        {
            string folderPath = Path.Combine(destinationPath, folderName);
            Directory.CreateDirectory(folderPath);

            string folderUrl = baseUrl + "/" + folderName;
            HttpResponseMessage response = await client.GetAsync(folderUrl);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                JArray folderContentArray = JArray.Parse(responseContent);
                int totalItems = folderContentArray.Count;
                int downloadedItems = 0;

                foreach (JToken item in folderContentArray)
                {
                    JObject? itemObject = item as JObject;
                    string? itemType = itemObject?["type"]?.ToString();
                    string? itemName = itemObject?["name"]?.ToString();

                    if (itemType == null) continue;
                    if (itemName == null) continue;

                    if (itemType == "file")
                    {
                        // Item is a file within the nested folder
                        await DownloadFile(client, folderUrl, itemName, folderPath);
                    }
                    else if (itemType == "folder")
                    {
                        // Item is a subfolder within the nested folder
                        await DownloadFolder(client, folderUrl, itemName, folderPath, progress);
                    }

                    downloadedItems++;
                    double progressPercentage = (double)(downloadedItems * 100) / totalItems;
                    progress?.Report(progressPercentage);
                }
            }
            else
            {
                MockConsole.WriteLine($"Failed to download folder '{folderName}'. Server returned status code: {response.StatusCode}");
            }
        }

        /// <summary>
        /// Sanitizes the provided file name by replacing any invalid characters with underscores.
        /// </summary>
        /// <param name="fileName">The file name to sanitize.</param>
        /// <returns>The sanitized file name with invalid characters replaced by underscores.</returns>
        private static string SanitizeFileName(string fileName)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            return fileName;
        }
        #endregion
    }
}
