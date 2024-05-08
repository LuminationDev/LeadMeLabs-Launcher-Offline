using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using OfflineInstaller._pages;
using OfflineInstaller.Components._notification;
using OfflineInstaller.MVC.Controller;
using OfflineInstaller.MVC.View;
using OfflineInstaller.MVC.ViewModel;

namespace OfflineInstaller.Components._managers;

public static class DownloadManager
{
    // Track whether the software is from Development or Production
    private static string? Mode { get; set; }
    private static string? VultrBucketName { get; set; }
    private static string? VultrLauncherBucketName { get; set; }
    private static string? VultrLauncherBucketPath { get; set; }
    private static string? BaseUrl { get; set; }
    private static string? LauncherUrl { get; set; }
    
    // Set Vultr Object Storage credentials and file information
    private const string AccessKey = "6561KRO6F7MZF4RR5Y9X";
    private const string SecretKey = "CqChoBKwxY0ROaXuUJjZ9XHrp02wApmAvxaNdx1t";

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

        // Determine if Vultr can be reached, otherwise use heroku as the backup
        
#if DEBUG
        BaseUrl = "http://localhost:8082"; //For testing purposes only    
        LauncherUrl = "http://localhost:8084"; //For testing purposes only 
#elif RELEASE
        if (MainController.CanAccessVultr)
        {
            BaseUrl = "https://sgp1.vultrobjects.com";
            
            VultrBucketName = Mode.Equals("development")
                ? "leadme-internal-debug"
                : "leadme-internal";
            
            VultrLauncherBucketName = "leadme-tools";
            
            VultrLauncherBucketPath = Mode.Equals("development")
                ? "leadme-launcher-debug"
                : "leadme-launcher";
        }
        else
        {
            BaseUrl = Mode.Equals("development")
                ? "https://learninglablauncherdevelopment.herokuapp.com"
                : "https://learninglablauncher.herokuapp.com";
            
            LauncherUrl = Mode.Equals("development")
                ? "https://leadme-launcher-development-92514d5e709f.herokuapp.com"
                : "https://electronlauncher.herokuapp.com";
        }
#endif

        MockConsole.WriteLine($"Downloading software from {Mode}.");

        if (_viewModel == null)
        {
            MockConsole.WriteLine($"Download incomplete please restart the software.");
            return;
        }

        using (LoadingWindow loadingWindow = new(_viewModel))
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.LoadingWindow = loadingWindow;
                loadingWindow.Owner = MainWindow.Instance;
            }

            loadingWindow.Show();

            // Perform necessary operations with the loadingWindow
            await DownloadSoftware("nuc");
            await DownloadSoftware("station");
            await DownloadSoftware("steamcmd"); //TODO this will be depreciated soon

            if (MainController.CanAccessVultr)
            {
                await DownloadVultrLauncher();
            }
            else
            {
                await DownloadLauncher();
            }

            // Close the loadingWindow when you're done with it
            loadingWindow.Close();

            // Dispose of the loadingWindow and release any associated resources
            loadingWindow.Dispose();
        }

        MockConsole.WriteLine($"Software downloaded from {Mode}.");
        ResetDownloadProgress();
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
            if (_viewModel != null)
            {
                _viewModel.DownloadText = $"Extracting {char.ToUpper(software[0]) + software[1..]} software.";
            }

            string savePath = @$"{MainWindow.InstallerLocation}\_programs\{fileName}.zip";
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
        
        string name = string.IsNullOrEmpty(overrideFileName) ? software : overrideFileName;
        string savePath = @$"{MainWindow.InstallerLocation}\_programs\{name}.zip"; // Specify the path where you want to save the downloaded file

        if (MainController.CanAccessVultr)
        {
            MockConsole.WriteLine("Downloading from Vultr.");
            string bucketPath = $"{overrideFileName}/{overrideFileName}.zip";
            await DownloadVultr(bucketPath, savePath);
        }
        else
        {
            MockConsole.WriteLine("Downloading from Heroku.");
            var downloadUrl = $"{serverUrl}/program-{software}"; // Specify the URL of the zip folder to download
            await Download(downloadUrl, savePath);
        }
    }

    /// <summary>
    /// After all downloads have finished, reset the text values 
    /// </summary>
    private static void ResetDownloadProgress()
    {
        // Update your UI or perform any action based on the progress percentage
        if (_viewModel == null) return;
        
        _viewModel.DownloadProgressText = "0%";
        _viewModel.DownloadProgress = 0;
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
            if (_viewModel == null) return;
            
            _viewModel.DownloadProgressText = $"{progressPercentage}%";
            _viewModel.DownloadProgress = progressPercentage;
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

            await using (Stream contentStream = await response.Content.ReadAsStreamAsync())
            {
                await using FileStream fileStream = new(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                byte[] buffer = new byte[32768]; // Set the buffer size according to your needs
                int bytesRead;
                long totalBytesRead = 0;
                long totalBytes = response.Content.Headers.ContentLength ?? -1;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (totalBytes <= 0) continue;
                    
                    double progressPercentage = (double)((totalBytesRead * 100) / totalBytes);
                    progress?.Report(progressPercentage);
                }
            }

            MockConsole.WriteLine("File downloaded successfully.");
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine("An error occurred during file download: " + ex.Message);
        }
    }

    /// <summary>
    /// Downloads a file from Vultr Object Storage asynchronously.
    /// </summary>
    /// <param name="bucketPath">The path of the file in the Vultr Object Storage bucket.</param>
    /// <param name="savePath">The local path where the downloaded file will be saved.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    private static async Task DownloadVultr(string bucketPath, string savePath)
    {
        IProgress<double> progress = Progress;
        
        using var client = new AmazonS3Client(AccessKey, SecretKey, new AmazonS3Config
        {
            ServiceURL = BaseUrl
        });

        var request = new GetObjectRequest
        {
            BucketName = VultrBucketName,
            Key = bucketPath
        };
        
        try
        {
            // Get the object from Vultr Object Storage
            using var response = await client.GetObjectAsync(request);
            await using var responseStream = response.ResponseStream;
            await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[32768]; // 32KB buffer (adjust as needed)
            var totalBytesRead = 0L;
            var totalFileSize = response.ContentLength;

            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                // Write the buffer to the file stream
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;

                // Calculate and report progress percentage
                var progressPercentage = (double)totalBytesRead / totalFileSize * 100;
                progress?.Report(progressPercentage);
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
    /// Downloads the Launcher software from Vultr Object Storage asynchronously.
    /// </summary>
    /// <remarks>
    /// This method retrieves the files associated with the Launcher software from a specific bucket
    /// and saves them to the appropriate directory. It also updates the user interface with the
    /// download progress and displays any error messages encountered during the download process.
    /// </remarks>
    /// <returns>A task representing the asynchronous download operation.</returns>
    private static async Task DownloadVultrLauncher()
    {
        IProgress<double> progress = Progress;
        
        if (_viewModel != null)
        {
            _viewModel.DownloadText = $"Downloading Launcher software.";
        }

        MockConsole.WriteLine($"Downloading Launcher from Vultr.");
        
        using var client = new AmazonS3Client(AccessKey, SecretKey, new AmazonS3Config
        {
            ServiceURL = BaseUrl
        });

        var listObjectsRequest = new ListObjectsV2Request
        {
            BucketName = VultrLauncherBucketName,
            Prefix = VultrLauncherBucketPath + "/"
        };
        
        try
        {
            var listObjectsResponse = await client.ListObjectsV2Async(listObjectsRequest);

            var totalFiles = listObjectsResponse.S3Objects.Count;
            var filesDownloaded = 0;

            foreach (var s3Object in listObjectsResponse.S3Objects)
            {
                var getObjectRequest = new GetObjectRequest
                {
                    BucketName = VultrLauncherBucketName,
                    Key = s3Object.Key
                };

                string destinationPath = @$"{MainWindow.InstallerLocation}\_programs\electron-launcher\";
                
                // Create the destination folder if it doesn't exist
                Directory.CreateDirectory(destinationPath);
                string subPath = s3Object.Key.Replace("leadme-launcher-debug/", "");
                var localFilePath = Path.Combine(destinationPath, subPath);
                
                await DownloadObjectAsync(client, getObjectRequest, localFilePath, percentage =>
                {
                    progress.Report(percentage);
                });

                filesDownloaded++;
                MockConsole.WriteLine($"File {filesDownloaded} of {totalFiles} downloaded.");
            }

            MockConsole.WriteLine("All files downloaded successfully.");
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                //Update the text block and the version map
                TextBlock? textBlock = MainWindow.Instance?.GetTextBlock("Launcher");
                if (textBlock == null) return;

                string version = FileManager.ExtractVersionFromYaml();
                textBlock.Text = version;
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.VersionMap["Launcher"] = version;
                }
            });
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine("An error occurred: " + ex.Message);
        }
    }
    
    /// <summary>
    /// Downloads an object asynchronously from Vultr Object Storage and saves it to the specified local file path.
    /// </summary>
    /// <param name="client">The Amazon S3 client used to perform the download operation.</param>
    /// <param name="request">The request object specifying the bucket and key of the object to download.</param>
    /// <param name="localFilePath">The local file path where the downloaded object will be saved.</param>
    /// <param name="progressCallback">An optional callback to report the progress of the download operation.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    private static async Task DownloadObjectAsync(AmazonS3Client client, GetObjectRequest request, string localFilePath, Action<double> progressCallback)
    {
        try
        {
            // Create the parent directory if it doesn't exist
            var parentDirectory = Path.GetDirectoryName(localFilePath);
            if (!Directory.Exists(parentDirectory) && parentDirectory != null)
            {
                Directory.CreateDirectory(parentDirectory);
            }
            
            using var response = await client.GetObjectAsync(request);
            await using var responseStream = response.ResponseStream;
            await using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[32768]; // 32KB buffer (adjust as needed)
            var totalBytesRead = 0L;
            var totalFileSize = response.ContentLength;

            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;

                var progressPercentage = (double)totalBytesRead / totalFileSize * 100;
                progressCallback?.Invoke(progressPercentage);
            }
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"Failed to download {request.Key}: {ex.Message}");
        }
    }
    
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

        MockConsole.WriteLine($"Downloading Launcher from Heroku.");

        string serverUrl = $"{LauncherUrl}/download-folder";
        string destinationPath = @$"{MainWindow.InstallerLocation}\_programs\electron-launcher\";

        using HttpClient client = new HttpClient();
        try
        {
            HttpResponseMessage response =
                await client.GetAsync(serverUrl, HttpCompletionOption.ResponseContentRead);

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


                Application.Current.Dispatcher.Invoke(() =>
                {
                    //Update the text block and the version map
                    TextBlock? textBlock = MainWindow.Instance?.GetTextBlock("Launcher");
                    if (textBlock == null) return;

                    string version = FileManager.ExtractVersionFromYaml();
                    textBlock.Text = version;
                    if (MainWindow.Instance != null)
                    {
                        MainWindow.Instance.VersionMap["Launcher"] = version;
                    }
                });
            }
            else
            {
                MockConsole.WriteLine("Failed to download folder. Server returned status code: " +
                                      response.StatusCode);
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
        await using Stream contentStream = await fileResponse.Content.ReadAsStreamAsync();
        await using Stream fileStream = File.Create(filePath);
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

                switch (itemType)
                {
                    case "file":
                        // Item is a file within the nested folder
                        await DownloadFile(client, folderUrl, itemName, folderPath);
                        break;
                    case "folder":
                        // Item is a subfolder within the nested folder
                        await DownloadFolder(client, folderUrl, itemName, folderPath, progress);
                        break;
                }

                downloadedItems++;
                double progressPercentage = (double)(downloadedItems * 100) / totalItems;
                // Round to two decimal places
                progressPercentage = Math.Round(progressPercentage * 100.0) / 100.0;
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
