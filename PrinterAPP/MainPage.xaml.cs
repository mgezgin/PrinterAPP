// MainPage.xaml.cs
using Microsoft.Maui.Controls;
using PrinterAPP.Models;
using PrinterAPP.Services;

namespace PrinterAPP;

public partial class MainPage : ContentPage
{
    private readonly IPrinterService _printerService;
    private PrinterConfiguration _config;
    private bool _isServiceRunning = false;

    public MainPage(IPrinterService printerService)
    {
        InitializeComponent();
        _printerService = printerService;
        _config = new PrinterConfiguration();

        // Initialize the UI asynchronously
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Show loading state
            StatusLabel.Text = "Loading configuration...";
            StatusLabel.TextColor = Colors.Gray;

            // Load saved configuration
            _config = await _printerService.LoadConfigurationAsync();

            // Update UI with loaded configuration
            ApiUrlEntry.Text = _config.ApiBaseUrl;
            RestaurantNameEntry.Text = _config.RestaurantName;
            KitchenLocationEntry.Text = _config.KitchenLocation;
            AutoPrintSwitch.IsToggled = _config.AutoPrint;
            PrintCopiesEntry.Text = _config.PrintCopies.ToString();
            PaperWidthPicker.SelectedIndex = _config.PaperWidth == 80 ? 0 : 1;

            // Load available printers
            await LoadPrintersAsync();

            // Update service status (Windows only)
            UpdateServiceStatus();

            StatusLabel.Text = "Configuration loaded";
            StatusLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Error loading configuration";
            StatusLabel.TextColor = Colors.Red;
            await DisplayAlert("Error", $"Failed to initialize: {ex.Message}", "OK");
        }
    }

    private async Task LoadPrintersAsync()
    {
        try
        {
            var printers = await _printerService.GetAvailablePrintersAsync();

            if (printers != null && printers.Count > 0)
            {
                PrinterPicker.ItemsSource = printers;

                // Select saved printer if it exists
                if (!string.IsNullOrEmpty(_config.PrinterName))
                {
                    var savedPrinter = printers.FirstOrDefault(p => p.Contains(_config.PrinterName));
                    if (savedPrinter != null)
                    {
                        PrinterPicker.SelectedItem = savedPrinter;
                    }
                    else if (printers.Count > 0)
                    {
                        PrinterPicker.SelectedIndex = 0;
                    }
                }
                else if (printers.Count > 0)
                {
                    PrinterPicker.SelectedIndex = 0;
                }
            }
            else
            {
                await DisplayAlert("Info", "No printers found. Make sure printers are installed.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load printers: {ex.Message}", "OK");
        }
    }

    private void UpdateServiceStatus()
    {
#if WINDOWS
        // This would typically check if a Windows service is running
        // For now, we'll simulate it
        _isServiceRunning = _config.IsServiceRunning;
        
        if (_isServiceRunning)
        {
            StatusLabel.Text = "Service is running";
            StatusLabel.TextColor = Colors.Green;
            ServiceToggleButton.Text = "Stop Service";
            ServiceToggleButton.BackgroundColor = Colors.Red;
        }
        else
        {
            StatusLabel.Text = "Service is stopped";
            StatusLabel.TextColor = Colors.Orange;
            ServiceToggleButton.Text = "Start Service";
            ServiceToggleButton.BackgroundColor = Colors.Green;
        }
#else
        StatusLabel.Text = "Ready";
        StatusLabel.TextColor = Colors.Green;
#endif
    }

    // Event Handlers

    private async void OnServiceToggleClicked(object sender, EventArgs e)
    {
#if WINDOWS
        try
        {
            ServiceToggleButton.IsEnabled = false;
            
            if (_isServiceRunning)
            {
                // Stop service
                StatusLabel.Text = "Stopping service...";
                StatusLabel.TextColor = Colors.Orange;
                
                // Simulate service stop (in real app, you'd call Windows service API)
                await Task.Delay(1000);
                _isServiceRunning = false;
                _config.IsServiceRunning = false;
                
                await DisplayAlert("Success", "Service stopped successfully", "OK");
            }
            else
            {
                // Start service
                StatusLabel.Text = "Starting service...";
                StatusLabel.TextColor = Colors.Orange;
                
                // Simulate service start
                await Task.Delay(1000);
                _isServiceRunning = true;
                _config.IsServiceRunning = true;
                
                await DisplayAlert("Success", "Service started successfully", "OK");
            }
            
            UpdateServiceStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to toggle service: {ex.Message}", "OK");
        }
        finally
        {
            ServiceToggleButton.IsEnabled = true;
        }
#endif
    }

    private async void OnTestApiClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            StatusLabel.Text = "Testing API connection...";
            StatusLabel.TextColor = Colors.Orange;

            var apiUrl = ApiUrlEntry.Text;
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                await DisplayAlert("Error", "Please enter an API URL", "OK");
                return;
            }

            var success = await _printerService.TestApiConnectionAsync(apiUrl);

            if (success)
            {
                StatusLabel.Text = "API connection successful";
                StatusLabel.TextColor = Colors.Green;
                await DisplayAlert("Success", "API connection successful!", "OK");
            }
            else
            {
                StatusLabel.Text = "API connection failed";
                StatusLabel.TextColor = Colors.Red;
                await DisplayAlert("Error", "Failed to connect to API. Please check the URL and try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Connection test failed: {ex.Message}", "OK");
        }
        finally
        {
            if (sender is Button button) button.IsEnabled = true;
        }
    }

    private async void OnRefreshPrintersClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            StatusLabel.Text = "Refreshing printer list...";
            StatusLabel.TextColor = Colors.Orange;

            await LoadPrintersAsync();

            StatusLabel.Text = "Printer list refreshed";
            StatusLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to refresh printers: {ex.Message}", "OK");
        }
        finally
        {
            if (sender is Button button) button.IsEnabled = true;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            // Update configuration from UI
            _config.ApiBaseUrl = ApiUrlEntry.Text;
            _config.RestaurantName = RestaurantNameEntry.Text;
            _config.KitchenLocation = KitchenLocationEntry.Text;
            _config.AutoPrint = AutoPrintSwitch.IsToggled;
            _config.PaperWidth = PaperWidthPicker.SelectedIndex == 0 ? 80 : 58;

            if (int.TryParse(PrintCopiesEntry.Text, out int copies))
            {
                _config.PrintCopies = Math.Max(1, Math.Min(copies, 5)); // Limit 1-5
            }

            if (PrinterPicker.SelectedItem != null)
            {
                _config.PrinterName = PrinterPicker.SelectedItem.ToString();
            }

            // Save configuration
            await _printerService.SaveConfigurationAsync(_config);

            StatusLabel.Text = "Configuration saved";
            StatusLabel.TextColor = Colors.Green;

            await DisplayAlert("Success", "Configuration saved successfully!", "OK");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Failed to save configuration";
            StatusLabel.TextColor = Colors.Red;
            await DisplayAlert("Error", $"Failed to save configuration: {ex.Message}", "OK");
        }
    }

    private async void OnTestPrintClicked(object sender, EventArgs e)
    {
        try
        {
            if (PrinterPicker.SelectedItem == null)
            {
                await DisplayAlert("Error", "Please select a printer first", "OK");
                return;
            }

            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            StatusLabel.Text = "Printing test receipt...";
            StatusLabel.TextColor = Colors.Orange;

            // Update config with current UI values
            _config.RestaurantName = RestaurantNameEntry.Text;
            _config.KitchenLocation = KitchenLocationEntry.Text;
            _config.PaperWidth = PaperWidthPicker.SelectedIndex == 0 ? 80 : 58;

            var printerName = PrinterPicker.SelectedItem.ToString();
            var success = await _printerService.PrintTestReceiptAsync(printerName, _config);

            if (success)
            {
                StatusLabel.Text = "Test receipt printed";
                StatusLabel.TextColor = Colors.Green;
                await DisplayAlert("Success", "Test receipt printed successfully!", "OK");
            }
            else
            {
                StatusLabel.Text = "Print failed";
                StatusLabel.TextColor = Colors.Red;
                await DisplayAlert("Error", "Failed to print test receipt. Please check your printer.", "OK");
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Print error";
            StatusLabel.TextColor = Colors.Red;
            await DisplayAlert("Error", $"Print failed: {ex.Message}", "OK");
        }
        finally
        {
            if (sender is Button button) button.IsEnabled = true;
        }
    }

    private async void OnResetClicked(object sender, EventArgs e)
    {
        try
        {
            var confirm = await DisplayAlert(
                "Confirm Reset",
                "Are you sure you want to reset all settings to default?",
                "Yes",
                "No");

            if (confirm)
            {
                // Reset to defaults
                _config = new PrinterConfiguration();

                // Update UI
                ApiUrlEntry.Text = _config.ApiBaseUrl;
                RestaurantNameEntry.Text = _config.RestaurantName;
                KitchenLocationEntry.Text = _config.KitchenLocation;
                AutoPrintSwitch.IsToggled = _config.AutoPrint;
                PrintCopiesEntry.Text = _config.PrintCopies.ToString();
                PaperWidthPicker.SelectedIndex = _config.PaperWidth == 80 ? 0 : 1;

                if (PrinterPicker.ItemsSource != null && PrinterPicker.ItemsSource.Cast<object>().Any())
                {
                    PrinterPicker.SelectedIndex = 0;
                }

                StatusLabel.Text = "Settings reset to default";
                StatusLabel.TextColor = Colors.Orange;

                await DisplayAlert("Success", "Settings have been reset to default values", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to reset settings: {ex.Message}", "OK");
        }
    }
}