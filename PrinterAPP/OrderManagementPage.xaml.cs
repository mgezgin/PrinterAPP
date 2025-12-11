using Microsoft.Extensions.Logging;
using PrinterAPP.Services;

namespace PrinterAPP;

public partial class OrderManagementPage : ContentPage
{
    private readonly OrderHistoryService _orderHistoryService;
    private readonly OrderPrintService _orderPrintService;
    private readonly IEventStreamingService _eventStreamingService;
    private readonly ILogger<OrderManagementPage> _logger;

    public OrderManagementPage(
        OrderHistoryService orderHistoryService,
        OrderPrintService orderPrintService,
        IEventStreamingService eventStreamingService,
        ILogger<OrderManagementPage> logger)
    {
        InitializeComponent();

        _orderHistoryService = orderHistoryService;
        _orderPrintService = orderPrintService;
        _eventStreamingService = eventStreamingService;
        _logger = logger;

        // Bind to order history
        OrdersCollectionView.ItemsSource = _orderHistoryService.Orders;

        // Subscribe to new orders
        _orderHistoryService.OrderAdded += OnOrderAdded;

        // Update service status
        UpdateServiceStatus();

        // Update status label
        UpdateStatusLabel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateServiceStatus();
        UpdateStatusLabel();
    }

    private void OnOrderAdded(object? sender, OrderHistoryItem e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateStatusLabel();
        });
    }

    private void UpdateServiceStatus()
    {
        var isRunning = _eventStreamingService.IsListening;
        ServiceStatusButton.Text = isRunning ? "Service: Running" : "Service: Stopped";
        ServiceStatusButton.BackgroundColor = isRunning ? Colors.Green : Colors.Orange;
    }

    private void UpdateStatusLabel()
    {
        var count = _orderHistoryService.Orders.Count;
        StatusLabel.Text = count == 0
            ? "No orders received yet"
            : $"Total orders: {count}";
    }

    private async void OnPrintKitchenClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is string orderId)
            {
                button.IsEnabled = false;

                var orderItem = _orderHistoryService.GetOrder(orderId);
                if (orderItem?.Order != null)
                {
                    _logger.LogInformation("Reprinting order #{OrderNumber} to kitchen", orderItem.Order.OrderNumber);

                    var success = await _orderPrintService.PrintOrderAsync(
                        orderItem.Order,
                        OrderPrintService.PrinterType.Kitchen,
                        isManualPrint: true);

                    if (success)
                    {
                        _orderHistoryService.UpdatePrintStatus(orderId, true, orderItem.CashierPrinted);
                        await DisplayAlert("Success", $"Order #{orderItem.Order.OrderNumber} printed to kitchen", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to print to kitchen. Check printer configuration.", "OK");
                    }
                }

                button.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing to kitchen");
            await DisplayAlert("Error", $"Print failed: {ex.Message}", "OK");
        }
    }

    private async void OnPrintCashierClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is string orderId)
            {
                button.IsEnabled = false;

                var orderItem = _orderHistoryService.GetOrder(orderId);
                if (orderItem?.Order != null)
                {
                    _logger.LogInformation("Reprinting order #{OrderNumber} to cashier", orderItem.Order.OrderNumber);

                    var success = await _orderPrintService.PrintOrderAsync(
                        orderItem.Order,
                        OrderPrintService.PrinterType.Cashier,
                        isManualPrint: true);

                    if (success)
                    {
                        _orderHistoryService.UpdatePrintStatus(orderId, orderItem.KitchenPrinted, true);
                        await DisplayAlert("Success", $"Order #{orderItem.Order.OrderNumber} printed to cashier", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to print to cashier. Check printer configuration.", "OK");
                    }
                }

                button.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing to cashier");
            await DisplayAlert("Error", $"Print failed: {ex.Message}", "OK");
        }
    }

    private async void OnPrintBothClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is string orderId)
            {
                button.IsEnabled = false;

                var orderItem = _orderHistoryService.GetOrder(orderId);
                if (orderItem?.Order != null)
                {
                    _logger.LogInformation("Reprinting order #{OrderNumber} to both printers", orderItem.Order.OrderNumber);

                    var kitchenSuccess = await _orderPrintService.PrintOrderAsync(
                        orderItem.Order,
                        OrderPrintService.PrinterType.Kitchen,
                        isManualPrint: true);

                    var cashierSuccess = await _orderPrintService.PrintOrderAsync(
                        orderItem.Order,
                        OrderPrintService.PrinterType.Cashier,
                        isManualPrint: true);

                    if (kitchenSuccess && cashierSuccess)
                    {
                        _orderHistoryService.UpdatePrintStatus(orderId, true, true);
                        await DisplayAlert("Success", $"Order #{orderItem.Order.OrderNumber} printed to both printers", "OK");
                    }
                    else if (kitchenSuccess || cashierSuccess)
                    {
                        _orderHistoryService.UpdatePrintStatus(orderId, kitchenSuccess, cashierSuccess);
                        await DisplayAlert("Partial Success",
                            $"Kitchen: {(kitchenSuccess ? "✓" : "✗")}\nCashier: {(cashierSuccess ? "✓" : "✗")}",
                            "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to print to both printers. Check printer configuration.", "OK");
                    }
                }

                button.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing to both printers");
            await DisplayAlert("Error", $"Print failed: {ex.Message}", "OK");
        }
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        UpdateServiceStatus();
        UpdateStatusLabel();
    }

    private async void OnClearHistoryClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Confirm Clear",
            "Are you sure you want to clear all order history?",
            "Yes",
            "No");

        if (confirm)
        {
            _orderHistoryService.ClearHistory();
            UpdateStatusLabel();
            await DisplayAlert("Success", "Order history cleared", "OK");
        }
    }
}
