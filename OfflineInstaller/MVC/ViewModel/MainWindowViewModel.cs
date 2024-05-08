using CommunityToolkit.Mvvm.ComponentModel;

namespace OfflineInstaller.MVC.ViewModel;

public class MainWindowViewModel : ObservableRecipient
{
    /// <summary>
    /// Used for binding the MainWindow Mockconsole
    /// </summary>
    private string _consoleText = "";

    ///<summary>
    /// Gets or sets the text value of the console.
    ///</summary>
    public string ConsoleText
    {
        get => _consoleText;
        set => SetProperty(ref _consoleText, value);
    }
    
    /// <summary>
    /// Display what question the user is being asked.
    /// </summary>
    private string _titleText = "";

    ///<summary>
    /// Gets or sets the text value of the title text.
    ///</summary>
    public string TitleText
    {
        get => _titleText;
        set => SetProperty(ref _titleText, value);
    }

    /// <summary>
    /// Display what software is currently being downloaded, unzipped or otherwise being worked on.
    /// </summary>
    private string _downloadText = "";

    ///<summary>
    /// Gets or sets the text value of the download text.
    ///</summary>
    public string DownloadText
    {
        get => _downloadText;
        set => SetProperty(ref _downloadText, value);
    }

    /// <summary>
    /// Display the current download percent as an int for the progress bar value.
    /// </summary>
    private double _downloadProgress;

    ///<summary>
    /// Gets or sets the text value of the download progress.
    ///</summary>
    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    /// <summary>
    /// Display the current download percent as a string.
    /// </summary>
    private string _downloadProgressText = "";

    ///<summary>
    /// Gets or sets the text value of the download progress string.
    ///</summary>
    public string DownloadProgressText
    {
        get => _downloadProgressText;
        set => SetProperty(ref _downloadProgressText, value);
    }
}
