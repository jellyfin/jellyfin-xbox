using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.Storage;

namespace Jellyfin.ViewModels;

/// <summary>
/// Represents a log file with its filename and date.
/// </summary>
public partial class LogfileViewModel : ObservableObject
{
    private StorageFile _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogfileViewModel"/> class.
    /// </summary>
    /// <param name="log">The StorageFile of the logfile.</param>
    public LogfileViewModel(StorageFile log) : this(log.Name, log.DateCreated.DateTime)
    {
        _log = log;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogfileViewModel"/> class.
    /// </summary>
    /// <param name="filename">The filename of the log file.</param>
    /// <param name="date">The date of the log file.</param>
    public LogfileViewModel(string filename, DateTime date)
    {
        Filename = filename;
        Date = date;

        UploadLogfileCommand = new RelayCommand(UploadLogfileExecute);
    }

    /// <summary>
    /// Gets or sets the filename of the log file.
    /// </summary>
    public string Filename { get => field; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets the date of the log file.
    /// </summary>
    public DateTime Date { get => field; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets the callback used for uploading the log file. This callback is intended to be invoked when the user initiates an upload action, allowing the application to handle the upload process accordingly.
    /// </summary>
    public Action<object> UploadCallback { get; internal set; }

    /// <summary>
    /// Gets or Sets the UI command to start the upload of the selected logfile.
    /// </summary>
    public IRelayCommand UploadLogfileCommand { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this log represents the current active logfile.
    /// </summary>
    public bool IsLatestLogfile { get => field; set => SetProperty(ref field, value); }

    private void UploadLogfileExecute()
    {
        UploadCallback?.Invoke(this);
    }
}
