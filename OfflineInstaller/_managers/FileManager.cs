using OfflineInstaller._notification;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OfflineInstaller._managers
{
    public class FileManager
    {
        ///<summary>
        /// Asynchronously unzips a folder from a specified zip file to a destination folder path, while reporting the progress.
        ///</summary>
        ///<param name="zipFilePath">The path of the zip file to extract.</param>
        ///<param name="destinationFolderPath">The destination folder path where the files will be extracted.</param>
        ///<param name="progress">An instance of IProgress to report the extraction progress.</param>
        ///<param name="deleteAfterExtraction">Optional. Specifies whether to delete the zip file after extraction. Default is false.</param>
        ///<returns>A Task representing the asynchronous operation.</returns>
        public static async Task UnzipFolderAsync(string zipFilePath, string destinationFolderPath, IProgress<double> progress, bool deleteAfterExtraction = false)
        {
            if (!File.Exists(zipFilePath))
            {
                MockConsole.WriteLine($"Cannot find {zipFilePath}.");
                return;
            }

            byte[] buffer = new byte[4096];
            long totalBytes = 0;

            using (FileStream zipFile = new(zipFilePath, FileMode.Open))
            using (ZipArchive archive = new(zipFile, ZipArchiveMode.Read))
            {
                long totalBytesToExtract = archive.Entries.Sum(entry => entry.Length);

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryPath = Path.Combine(destinationFolderPath, entry.FullName);
                    string entryDirPath = Path.GetDirectoryName(entryPath);

                    // Create the directory if it doesn't exist
                    Directory.CreateDirectory(entryDirPath);

                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        using Stream entryStream = entry.Open();
                        using FileStream destinationStream = new(entryPath, FileMode.Create);
                        int bytesRead;
                        while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await destinationStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytes += bytesRead;

                            double progressPercentage = (double)totalBytes / totalBytesToExtract * 100;
                            double roundedProgress = Math.Round(progressPercentage, 2);
                            progress?.Report(roundedProgress);
                        }
                    }
                }
            }

            MockConsole.WriteLine("Folder unzipped successfully.");

            if (deleteAfterExtraction && File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
                MockConsole.WriteLine("Zipped folder deleted successfully.");
            }

            //Update the version code in the MainWindow UI
            await Task.Run(() =>
            {
                string fileName = Path.GetFileNameWithoutExtension(zipFilePath);
                string version = zipFilePath.Contains("Launcher") ? version = ExtractVersionFromYaml() : version = CheckProgramVersion(fileName);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextBlock textBlock = MainWindow.Instance.GetTextBlock(fileName);
                    if(textBlock != null)
                    {
                        textBlock.Text = version;
                    }
                });
            });
        }

        /// <summary>
        /// Checks the version of a program.
        /// </summary>
        /// <param name="programName">The name of the program.</param>
        /// <returns>The version of the program, or "Not found" if the program is not found, or "Unknown" if the version file is empty.</returns>
        public static string CheckProgramVersion(string programName)
        {
            // Specify the path to the program you want to start
            string programPath = @$"{MainWindow.installerLocation}\_programs\{programName}\";

            string programExecutable = $"{programName}.exe";
            string programVersion = $@"_logs\version.txt";

            // Specify the command-line arguments
            string arguments = "writeversion";

            if (!File.Exists(programPath + programExecutable))
            {
                return "Not found";
            }

            // Start the program with the specified arguments
            Process process = Process.Start(programPath + programExecutable, arguments);
            process.WaitForExit();

            string fileContent = File.ReadAllText(programPath + programVersion);
            return string.IsNullOrEmpty(fileContent) ? "Unknown" : fileContent;
        }

        /// <summary>
        /// Extracts the version number from a YAML file.
        /// </summary>
        /// <returns>The extracted version number, or "Not found" if the file is not found, or "Unknown" if the version pattern is not found.</returns>
        public static string ExtractVersionFromYaml()
        {
            string filePath = @$"{MainWindow.installerLocation}\_programs\electron-launcher\latest.yml";

            if (!File.Exists(filePath))
            {
                return "Not found";
            }

            string fileContent = File.ReadAllText(filePath);
            string versionPattern = @"version:\s*(\d+\.\d+\.\d+)";

            Match match = Regex.Match(fileContent, versionPattern);
            return match.Success ? match.Groups[1].Value : "Unknown";
        }
    }
}
