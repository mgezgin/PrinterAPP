// MainPage.xaml.cs
using Microsoft.Maui.Controls;
using Microsoft.Extensions.Logging;
using PrinterAPP.Models;
using PrinterAPP.Services;

namespace PrinterAPP;

public partial class MainPage : ContentPage
{
    private readonly IPrinterService _printerService;
    private readonly IEventStreamingService _eventStreamingService;
    private readonly OrderPrintService _orderPrintService;
    private readonly OrderHistoryService _orderHistoryService;
    private readonly ILogger<MainPage> _logger;
    private PrinterConfiguration _config;
    private bool _isServiceRunning = false;

    public MainPage(
        IPrinterService printerService,
        IEventStreamingService eventStreamingService,
        OrderPrintService orderPrintService,
        OrderHistoryService orderHistoryService,
        ILogger<MainPage> logger)
    {
        InitializeComponent();
        _printerService = printerService;
        _eventStreamingService = eventStreamingService;
        _orderPrintService = orderPrintService;
        _orderHistoryService = orderHistoryService;
        _logger = logger;
        _config = new PrinterConfiguration();

        // Subscribe to events
        _eventStreamingService.OrderReceived += OnOrderReceived;
        _eventStreamingService.ConnectionStatusChanged += OnConnectionStatusChanged;

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

            // Debug logging
            _logger.LogInformation("Loaded API URL from config: {ApiUrl}", _config.ApiBaseUrl);
            System.Diagnostics.Debug.WriteLine($"DEBUG: Loaded API URL = {_config.ApiBaseUrl}");

            // Update UI with loaded configuration
            ApiUrlEntry.Text = _config.ApiBaseUrl;

            // Debug logging
            _logger.LogInformation("Set ApiUrlEntry.Text to: {ApiUrl}", ApiUrlEntry.Text);
            System.Diagnostics.Debug.WriteLine($"DEBUG: ApiUrlEntry.Text = {ApiUrlEntry.Text}");
            RestaurantNameEntry.Text = _config.RestaurantName;
            KitchenLocationEntry.Text = _config.KitchenLocation;

            // Kitchen printer settings
            KitchenAutoPrintSwitch.IsToggled = _config.KitchenAutoPrint;
            KitchenPrintCopiesEntry.Text = _config.KitchenPrintCopies.ToString();
            KitchenPaperWidthPicker.SelectedIndex = _config.KitchenPaperWidth == 80 ? 0 : 1;

            // Cashier printer settings
            CashierAutoPrintSwitch.IsToggled = _config.CashierAutoPrint;
            CashierPrintCopiesEntry.Text = _config.CashierPrintCopies.ToString();
            CashierPaperWidthPicker.SelectedIndex = _config.CashierPaperWidth == 80 ? 0 : 1;

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
                // Load both kitchen and cashier printer pickers
                KitchenPrinterPicker.ItemsSource = printers;
                CashierPrinterPicker.ItemsSource = printers;

                // Select saved kitchen printer if it exists
                if (!string.IsNullOrEmpty(_config.KitchenPrinterName))
                {
                    var savedPrinter = printers.FirstOrDefault(p => p.Contains(_config.KitchenPrinterName));
                    if (savedPrinter != null)
                    {
                        KitchenPrinterPicker.SelectedItem = savedPrinter;
                    }
                    else if (printers.Count > 0)
                    {
                        KitchenPrinterPicker.SelectedIndex = 0;
                    }
                }
                else if (printers.Count > 0)
                {
                    KitchenPrinterPicker.SelectedIndex = 0;
                }

                // Select saved cashier printer if it exists
                if (!string.IsNullOrEmpty(_config.CashierPrinterName))
                {
                    var savedPrinter = printers.FirstOrDefault(p => p.Contains(_config.CashierPrinterName));
                    if (savedPrinter != null)
                    {
                        CashierPrinterPicker.SelectedItem = savedPrinter;
                    }
                    else if (printers.Count > 1)
                    {
                        CashierPrinterPicker.SelectedIndex = 1; // Default to second printer if available
                    }
                    else if (printers.Count > 0)
                    {
                        CashierPrinterPicker.SelectedIndex = 0;
                    }
                }
                else if (printers.Count > 1)
                {
                    CashierPrinterPicker.SelectedIndex = 1; // Default to second printer
                }
                else if (printers.Count > 0)
                {
                    CashierPrinterPicker.SelectedIndex = 0;
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
        _isServiceRunning = _eventStreamingService.IsListening;

        if (_isServiceRunning)
        {
            StatusLabel.Text = "SSE Service is running";
            StatusLabel.TextColor = Colors.Green;
            ServiceToggleButton.Text = "Stop Service";
            ServiceToggleButton.BackgroundColor = Colors.Red;
        }
        else
        {
            StatusLabel.Text = "SSE Service is stopped";
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
                // Stop SSE service
                StatusLabel.Text = "Stopping SSE service...";
                StatusLabel.TextColor = Colors.Orange;

                await _eventStreamingService.StopListeningAsync();
                _isServiceRunning = false;
                _config.IsServiceRunning = false;
                await _printerService.SaveConfigurationAsync(_config);

                await DisplayAlert("Success", "SSE service stopped successfully", "OK");
            }
            else
            {
                // Start SSE service
                StatusLabel.Text = "Starting SSE service...";
                StatusLabel.TextColor = Colors.Orange;

                await _eventStreamingService.StartListeningAsync();
                _isServiceRunning = true;
                _config.IsServiceRunning = true;
                await _printerService.SaveConfigurationAsync(_config);

                await DisplayAlert("Success", "SSE service started successfully. Now listening for orders.", "OK");
            }

            UpdateServiceStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle SSE service");
            await DisplayAlert("Error", $"Failed to toggle service: {ex.Message}", "OK");
        }
        finally
        {
            ServiceToggleButton.IsEnabled = true;
        }
#endif
    }

    private void OnOrderReceived(object? sender, OrderEvent orderEvent)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                _logger.LogInformation("Order received: #{OrderId} - {EventType}",
                    orderEvent.Order?.Id, orderEvent.EventType);

                if (orderEvent.Order == null)
                {
                    return;
                }

                // Add order to history
                _orderHistoryService.AddOrder(orderEvent);

                // Print to kitchen
                var kitchenSuccess = await _orderPrintService.PrintOrderAsync(
                    orderEvent.Order,
                    OrderPrintService.PrinterType.Kitchen);

                // Print to cashier
                var cashierSuccess = await _orderPrintService.PrintOrderAsync(
                    orderEvent.Order,
                    OrderPrintService.PrinterType.Cashier);

                // Update print status in history
                _orderHistoryService.UpdatePrintStatus(orderEvent.Order.Id, kitchenSuccess, cashierSuccess);

                StatusLabel.Text = $"Order #{orderEvent.Order.OrderNumber} printed";
                StatusLabel.TextColor = Colors.Green;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order");
                StatusLabel.Text = "Error processing order";
                StatusLabel.TextColor = Colors.Red;
            }
        });
    }

    private void OnConnectionStatusChanged(object? sender, string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = status;
            StatusLabel.TextColor = status.Contains("Error") || status.Contains("Disconnected")
                ? Colors.Red
                : Colors.Green;
        });
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
            // Check if API URL has changed
            var newApiUrl = ApiUrlEntry.Text?.Trim();
            var oldApiUrl = _config.ApiBaseUrl?.Trim();
            bool apiUrlChanged = !string.Equals(newApiUrl, oldApiUrl, StringComparison.OrdinalIgnoreCase);

            // If API URL changed and service is running, stop the service first
            if (apiUrlChanged && _isServiceRunning)
            {
                var confirm = await DisplayAlert(
                    "Service Running",
                    "The API URL has changed. The service must be stopped before saving the new URL. Stop the service now?",
                    "Yes, Stop Service",
                    "Cancel");

                if (!confirm)
                {
                    StatusLabel.Text = "Configuration save cancelled";
                    StatusLabel.TextColor = Colors.Orange;
                    return;
                }

                // Stop the service
                StatusLabel.Text = "Stopping service due to API URL change...";
                StatusLabel.TextColor = Colors.Orange;

                try
                {
                    await _eventStreamingService.StopListeningAsync();
                    _isServiceRunning = false;
                    _config.IsServiceRunning = false;

                    UpdateServiceStatus();

                    await DisplayAlert("Service Stopped", "The service has been stopped. Please start it again after saving to use the new API URL.", "OK");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop service before URL change");
                    await DisplayAlert("Error", $"Failed to stop service: {ex.Message}\nPlease stop the service manually before changing the API URL.", "OK");
                    return;
                }
            }

            // Update configuration from UI
            _config.ApiBaseUrl = newApiUrl;
            _config.RestaurantName = RestaurantNameEntry.Text;
            _config.KitchenLocation = KitchenLocationEntry.Text;

            // Kitchen printer settings
            _config.KitchenAutoPrint = KitchenAutoPrintSwitch.IsToggled;
            _config.KitchenPaperWidth = KitchenPaperWidthPicker.SelectedIndex == 0 ? 80 : 58;
            if (int.TryParse(KitchenPrintCopiesEntry.Text, out int kitchenCopies))
            {
                _config.KitchenPrintCopies = Math.Max(1, Math.Min(kitchenCopies, 5)); // Limit 1-5
            }
            if (KitchenPrinterPicker.SelectedItem != null)
            {
                _config.KitchenPrinterName = KitchenPrinterPicker.SelectedItem.ToString()!.Replace(" (Default)", "").Trim();
            }

            // Cashier printer settings
            _config.CashierAutoPrint = CashierAutoPrintSwitch.IsToggled;
            _config.CashierPaperWidth = CashierPaperWidthPicker.SelectedIndex == 0 ? 80 : 58;
            if (int.TryParse(CashierPrintCopiesEntry.Text, out int cashierCopies))
            {
                _config.CashierPrintCopies = Math.Max(1, Math.Min(cashierCopies, 5)); // Limit 1-5
            }
            if (CashierPrinterPicker.SelectedItem != null)
            {
                _config.CashierPrinterName = CashierPrinterPicker.SelectedItem.ToString()!.Replace(" (Default)", "").Trim();
            }

            // Save configuration
            _logger.LogInformation("Saving configuration with API URL: {ApiUrl}", _config.ApiBaseUrl);
            System.Diagnostics.Debug.WriteLine($"DEBUG SAVE: Saving config with API URL = {_config.ApiBaseUrl}");

            await _printerService.SaveConfigurationAsync(_config);

            _logger.LogInformation("Configuration saved successfully");
            System.Diagnostics.Debug.WriteLine($"DEBUG SAVE: Configuration saved successfully");

            StatusLabel.Text = "Configuration saved";
            StatusLabel.TextColor = Colors.Green;

            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KitchenPrinter",
                "config.json");

            if (apiUrlChanged)
            {
                await DisplayAlert("Success",
                    $"Configuration saved successfully!\n\n" +
                    $"API URL: {_config.ApiBaseUrl}\n" +
                    $"Config file: {configPath}\n\n" +
                    $"Please start the service again to connect to the new API endpoint.",
                    "OK");
            }
            else
            {
                await DisplayAlert("Success",
                    $"Configuration saved successfully!\n\n" +
                    $"Config file: {configPath}",
                    "OK");
            }
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
            if (KitchenPrinterPicker.SelectedItem == null && CashierPrinterPicker.SelectedItem == null)
            {
                await DisplayAlert("Error", "Please select at least one printer first", "OK");
                return;
            }

            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            StatusLabel.Text = "Printing test receipts...";
            StatusLabel.TextColor = Colors.Orange;

            // Update config with current UI values
            _config.RestaurantName = RestaurantNameEntry.Text;
            _config.KitchenLocation = KitchenLocationEntry.Text;
            _config.KitchenPaperWidth = KitchenPaperWidthPicker.SelectedIndex == 0 ? 80 : 58;
            _config.CashierPaperWidth = CashierPaperWidthPicker.SelectedIndex == 0 ? 80 : 58;

            var results = new List<string>();

            // Test kitchen printer
            if (KitchenPrinterPicker.SelectedItem != null)
            {
                var printerName = KitchenPrinterPicker.SelectedItem.ToString();
                var success = await _printerService.PrintTestReceiptAsync(printerName!, _config);
                results.Add($"Kitchen printer: {(success ? "✓ Success" : "✗ Failed")}");
            }

            // Test cashier printer
            if (CashierPrinterPicker.SelectedItem != null)
            {
                var printerName = CashierPrinterPicker.SelectedItem.ToString();
                var success = await _printerService.PrintTestReceiptAsync(printerName!, _config);
                results.Add($"Cashier printer: {(success ? "✓ Success" : "✗ Failed")}");
            }

            StatusLabel.Text = "Test receipts printed";
            StatusLabel.TextColor = Colors.Green;
            await DisplayAlert("Test Results", string.Join("\n", results), "OK");
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

                // Kitchen printer settings
                KitchenAutoPrintSwitch.IsToggled = _config.KitchenAutoPrint;
                KitchenPrintCopiesEntry.Text = _config.KitchenPrintCopies.ToString();
                KitchenPaperWidthPicker.SelectedIndex = _config.KitchenPaperWidth == 80 ? 0 : 1;

                // Cashier printer settings
                CashierAutoPrintSwitch.IsToggled = _config.CashierAutoPrint;
                CashierPrintCopiesEntry.Text = _config.CashierPrintCopies.ToString();
                CashierPaperWidthPicker.SelectedIndex = _config.CashierPaperWidth == 80 ? 0 : 1;

                if (KitchenPrinterPicker.ItemsSource != null && KitchenPrinterPicker.ItemsSource.Cast<object>().Any())
                {
                    KitchenPrinterPicker.SelectedIndex = 0;
                }

                if (CashierPrinterPicker.ItemsSource != null && CashierPrinterPicker.ItemsSource.Cast<object>().Any())
                {
                    CashierPrinterPicker.SelectedIndex = CashierPrinterPicker.ItemsSource.Cast<object>().Count() > 1 ? 1 : 0;
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