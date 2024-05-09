using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using OfflineInstaller.Components._notification;
using OfflineInstaller.MVC.Controller;
using OfflineInstaller.MVC.View;
using OfflineInstaller.MVC.ViewModel;

namespace OfflineInstaller.Components._managers;

public static class DownloadManager
{
    // Track whether the software is from Development or Production
    private static string? Mode { get; set; }
    private static string? BaseUrl { get; set; }
    private static string? LauncherUrl { get; set; }
    private static string? VultrLauncherBucketPath { get; set; }
    
    //Track how many files have been downloaded
    private static int _launchFileCount = 0;
    private const int LaunchFileNumber = 37;

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
        LauncherUrl = "http://localhost:8084"; //For testing purposes only 
#elif RELEASE
        if (MainController.CanAccessVultr)
        {
            BaseUrl = Mode.Equals("development")
                ? "https://leadme-internal-debug.sgp1.vultrobjects.com"
                : "https://leadme-internal.sgp1.vultrobjects.com";

            LauncherUrl = "https://leadme-tools.sgp1.vultrobjects.com";
            
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
            await DownloadSoftware("steamcmd");

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
            UpdateDownloadText($"Extracting {char.ToUpper(software[0]) + software[1..]} software.");

            string savePath = @$"{MainWindow.InstallerLocation}\_programs\{fileName}.zip";
            await FileManager.UnzipFolderAsync(savePath, $@"{Path.GetDirectoryName(savePath)}\{fileName}", Progress);
        }
    }

    /// <summary>
    /// Download the nuc or station software, the URL depends on the mode and software passed into the constructor.
    /// </summary>
    private static async Task DownloadProgram(string serverUrl, string software, string overrideFileName = "")
    {
        UpdateDownloadText($"Downloading {char.ToUpper(software[0]) + software[1..]} software.");
        UpdateDownloadFileText($"{overrideFileName}.zip");
        
        MockConsole.WriteLine($"Downloading {char.ToUpper(software[0]) + software[1..]}.");
        
        string name = string.IsNullOrEmpty(overrideFileName) ? software : overrideFileName;
        string savePath = @$"{MainWindow.InstallerLocation}\_programs\{name}.zip"; // Specify the path where you want to save the downloaded file

        string downloadUrl;
        if (MainController.CanAccessVultr)
        {
            MockConsole.WriteLine("Downloading from Vultr.");
            downloadUrl = $"{BaseUrl}/{overrideFileName}/{overrideFileName}.zip";
        }
        else
        {
            MockConsole.WriteLine("Downloading from Heroku.");
            downloadUrl = $"{serverUrl}/program-{software}"; // Specify the URL of the zip folder to download
        }
        
        await Download(downloadUrl, savePath);
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
        UpdateDownloadText("Downloading Launcher software.");

        string baseBucketPath = $"{LauncherUrl}/{VultrLauncherBucketPath}";
        string destinationPath = Path.Combine(MainWindow.InstallerLocation, "_programs", "electron-launcher");

        Directory.CreateDirectory(destinationPath);
        string version = await DownloadLatestYaml(baseBucketPath, destinationPath);

        if (string.IsNullOrEmpty(version))
        {
            MockConsole.WriteLine("Could not read latest.yml");
            return;
        }

        await DownloadExecutables(baseBucketPath, destinationPath, version);
        await DownloadWinUnpackedFiles(baseBucketPath, destinationPath);
        await DownloadBatchFiles(baseBucketPath, destinationPath);
        await DownloadResourcesFiles(baseBucketPath, destinationPath);

        UpdateLauncherVersion();
    }

    /// <summary>
    /// Downloads the latest YAML file from the specified base bucket path and saves it to the destination folder.
    /// Parses the YAML content to extract the version information.
    /// </summary>
    /// <param name="baseBucketPath">The base path of the bucket where the latest YAML file is located.</param>
    /// <param name="destinationPath">The local destination folder where the YAML file will be saved.</param>
    /// <returns>The version string extracted from the YAML content, or "Unknown" if parsing fails.</returns>
    private static async Task<string> DownloadLatestYaml(string baseBucketPath, string destinationPath)
    {
        string latestYamlUrl = $"{baseBucketPath}/latest.yml";
        string latestYamlFilePath = Path.Combine(destinationPath, "latest.yml");

        using HttpClient client = new HttpClient();
        
        HttpResponseMessage response = await client.GetAsync(latestYamlUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        string yamlContent = await response.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(latestYamlFilePath, yamlContent);
        
        string versionPattern = @"version:\s*(\d+\.\d+\.\d+)";
        Match match = Regex.Match(yamlContent, versionPattern);
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    /// <summary>
    /// Downloads the executable files associated with the specified version from the base bucket path.
    /// </summary>
    /// <param name="baseBucketPath">The base path of the bucket where the executable files are located.</param>
    /// <param name="destinationPath">The local destination folder where the executable files will be saved.</param>
    /// <param name="version">The version number of the executable files to download.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task DownloadExecutables(string baseBucketPath, string destinationPath, string version)
    {
        await DownloadVultrFile($"{baseBucketPath}/LeadMe Setup {version}.exe", Path.Combine(destinationPath, $"LeadMe Setup {version}.exe"), $"LeadMe Setup {version}.exe");
        await DownloadVultrFile($"{baseBucketPath}/LeadMe Setup {version}.exe.blockmap", Path.Combine(destinationPath, $"LeadMe Setup {version}.exe.blockmap"), $"LeadMe Setup {version}.exe.blockmap");
    }

    /// <summary>
    /// Downloads files related to the win-unpacked folder from the specified Vultr bucket to the destination path.
    /// </summary>
    /// <param name="baseBucketPath">The base path of the Vultr bucket.</param>
    /// <param name="destinationPath">The destination path where the files will be saved.</param>
    private static async Task DownloadWinUnpackedFiles(string baseBucketPath, string destinationPath)
    {
        string winFolder = Path.Combine(destinationPath, "win-unpacked");
        Directory.CreateDirectory(winFolder);
        
        var filesToDownload = new List<string>
        {
            "LICENSE.electron.txt",
            "LICENSES.chromium.html",
            "chrome_100_percent.pak",
            "chrome_200_percent.pak",
            "d3dcompiler_47.dll",
            "ffmpeg.dll",
            "icudtl.dat",
            "libEGL.dll",
            "libGLESv2.dll",
            "resources.pak",
            "snapshot_blob.bin",
            "v8_context_snapshot.bin",
            "vk_swiftshader.dll",
            "vk_swiftshader_icd.json",
            "vulkan-1.dll"
        };
        
        await DownloadBatchFiles(filesToDownload, baseBucketPath, destinationPath, "win-unpacked");
    }

    /// <summary>
    /// Downloads batch files from the specified Vultr bucket to the destination path.
    /// </summary>
    /// <param name="baseBucketPath">The base path of the Vultr bucket.</param>
    /// <param name="destinationPath">The destination path where the files will be saved.</param>
    private static async Task DownloadBatchFiles(string baseBucketPath, string destinationPath)
    {
        string batchFolder = Path.Combine(destinationPath, "win-unpacked", "_batch");
        Directory.CreateDirectory(batchFolder);
        
        var filesToDownload = new List<string>
        {
            "LeadMeLabs-NucChecker.deps.json",
            "LeadMeLabs-NucChecker.dll",
            "LeadMeLabs-NucChecker.exe",
            "LeadMeLabs-NucChecker.pdb",
            "LeadMeLabs-NucChecker.runtimeconfig.json",
            "LeadMeLabs-SoftwareChecker.deps.json",
            "LeadMeLabs-SoftwareChecker.dll",
            "LeadMeLabs-SoftwareChecker.exe",
            "LeadMeLabs-SoftwareChecker.pdb",
            "LeadMeLabs-SoftwareChecker.runtimeconfig.json",
            "LeadMeLabs-StationChecker.deps.json",
            "LeadMeLabs-StationChecker.deps.json",
            "LeadMeLabs-StationChecker.dll",
            "LeadMeLabs-StationChecker.exe",
            "LeadMeLabs-StationChecker.pdb",
            "LeadMeLabs-StationChecker.runtimeconfig.json",
            "README.md"
        };
        
        await DownloadBatchFiles(filesToDownload, baseBucketPath, destinationPath, "win-unpacked/_batch");
    }

    /// <summary>
    /// Downloads resource files from the specified Vultr bucket to the destination path.
    /// </summary>
    /// <param name="baseBucketPath">The base path of the Vultr bucket.</param>
    /// <param name="destinationPath">The destination path where the files will be saved.</param>

    private static async Task DownloadResourcesFiles(string baseBucketPath, string destinationPath)
    {
        string resourcesFolder = Path.Combine(destinationPath, "win-unpacked", "resources");
        Directory.CreateDirectory(resourcesFolder);
        
        var filesToDownload = new List<string>
        {
            "app-update.yml",
            "app.asar",
            "elevate.exe"
        };
        
        await DownloadBatchFiles(filesToDownload, baseBucketPath, destinationPath, "win-unpacked/resources");
    }
    
    /// <summary>
    /// Downloads files from the specified Vultr bucket to the destination path.
    /// </summary>
    /// <param name="fileNames">The list of file names to download.</param>
    /// <param name="baseBucketPath">The base path of the Vultr bucket.</param>
    /// <param name="destinationPath">The destination path where the files will be saved.</param>
    /// <param name="subFolder">Optional. The subfolder within the bucket from which to download the files.</param>
    private static async Task DownloadBatchFiles(IEnumerable<string> fileNames, string baseBucketPath, string destinationPath, string subFolder = "")
    {
        foreach (string fileName in fileNames)
        {
            string filePath = Path.Combine(destinationPath, subFolder, fileName);
            string fileUrl = $"{baseBucketPath}/{subFolder}/{fileName}";

            await DownloadVultrFile(fileUrl, filePath, fileName);
        }
    }
    
    /// <summary>
    /// Downloads a file from the specified URL and saves it to the specified local path.
    /// Reports download progress if a progress reporter is provided.
    /// </summary>
    /// <param name="downloadUrl">The URL of the file to download.</param>
    /// <param name="savePath">The local path where the downloaded file will be saved.</param>
    /// <param name="fileName">The name of the file to be downloaded.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task DownloadVultrFile(string downloadUrl, string savePath, string fileName)
    {
        UpdateDownloadFileText(fileName);
        
        _launchFileCount++;
        
        IProgress<double> progress = Progress;
        
        var parentDirectory = Path.GetDirectoryName(savePath);
        if (parentDirectory != null && !Directory.Exists(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }
        
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

            MockConsole.WriteLine($"File downloaded successfully {_launchFileCount} of {LaunchFileNumber}.");
        }
        catch (Exception ex)
        {
            MockConsole.WriteLine($"An error occurred during file download of {fileName}: {ex.Message}");
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

        UpdateDownloadText("Downloading Launcher software.");

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
                
                UpdateLauncherVersion();
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
    
    private static void UpdateDownloadText(string message)
    {
        if (_viewModel != null)
        {
            _viewModel.DownloadText = message;
        }
    }
    
    private static void UpdateDownloadFileText(string message)
    {
        if (_viewModel != null)
        {
            _viewModel.DownloadFileText = message;
        }
    }
    
    private static void UpdateLauncherVersion()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TextBlock? textBlock = MainWindow.Instance?.GetTextBlock("Launcher");
            if (textBlock == null) return;

            string currentVersion = FileManager.ExtractVersionFromYaml();
            textBlock.Text = currentVersion;
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.VersionMap["Launcher"] = currentVersion;
            }
        });
    }
    #endregion
}
