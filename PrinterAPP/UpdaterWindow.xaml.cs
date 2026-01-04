using PrinterAPP.Models;
using PrinterAPP.Services;

namespace PrinterAPP;

public partial class UpdaterWindow : ContentPage
{
    private readonly UpdateService _updateService;
    private UpdateInfo? _updateInfo;

    public UpdaterWindow(UpdateService updateService)
    {
        InitializeComponent();
        _updateService = updateService;
        
        // Show current version immediately
        CurrentVersionLabel.Text = _updateService.GetCurrentVersion();
    }

    private async void OnCheckForUpdatesClicked(object sender, EventArgs e)
    {
        try
        {
            CheckButton.IsEnabled = false;
            StatusLabel.Text = "Checking for updates...";
            StatusLabel.TextColor = Colors.Gray;

            _updateInfo = await _updateService.CheckForUpdateAsync();

            if (_updateInfo.UpdateAvailable)
            {
                // Update available
                LatestVersionLabel.Text = _updateInfo.LatestVersion;
                LatestVersionFrame.IsVisible = true;

                if (!string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes))
                {
                    ReleaseNotesLabel.Text = _updateInfo.ReleaseNotes;
                    ReleaseNotesFrame.IsVisible = true;
                }

                UpdateButton.IsVisible = true;
                StatusLabel.Text = $"New version {_updateInfo.LatestVersion} is available!";
                StatusLabel.TextColor = Colors.Green;
            }
            else
            {
                // No update available
                LatestVersionLabel.Text = _updateInfo.LatestVersion;
                LatestVersionFrame.IsVisible = true;
                StatusLabel.Text = "You have the latest version!";
                StatusLabel.TextColor = Colors.Green;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error checking for updates: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void OnUpdateNowClicked(object sender, EventArgs e)
    {
        if (_updateInfo == null || !_updateInfo.UpdateAvailable)
            return;

        try
        {
            UpdateButton.IsEnabled = false;
            CheckButton.IsEnabled = false;
            CloseButton.IsEnabled = false;

            DownloadProgressBar.IsVisible = true;
            StatusLabel.Text = "Downloading update...";
            StatusLabel.TextColor = Colors.Blue;

            var progress = new Progress<int>(percent =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DownloadProgressBar.Progress = percent / 100.0;
                    StatusLabel.Text = $"Downloading update... {percent}%";
                });
            });

            bool success = await _updateService.DownloadAndInstallUpdateAsync(_updateInfo, progress);

            if (success)
            {
                StatusLabel.Text = "Update successful! App will restart...";
                StatusLabel.TextColor = Colors.Green;
            }
            else
            {
                StatusLabel.Text = "Update failed. Please try again or download manually.";
                StatusLabel.TextColor = Colors.Red;
                UpdateButton.IsEnabled = true;
                CheckButton.IsEnabled = true;
                CloseButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error installing update: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
            UpdateButton.IsEnabled = true;
            CheckButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
