using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using PrinterAPP.Models;

namespace PrinterAPP.Services;

public class OrderHistoryService
{
    private readonly ILogger<OrderHistoryService> _logger;
    private readonly ObservableCollection<OrderHistoryItem> _orders;
    private readonly object _lockObject = new();

    public ObservableCollection<OrderHistoryItem> Orders => _orders;

    public event EventHandler<OrderHistoryItem>? OrderAdded;

    public OrderHistoryService(ILogger<OrderHistoryService> logger)
    {
        _logger = logger;
        _orders = new ObservableCollection<OrderHistoryItem>();
    }

    public void AddOrder(OrderEvent orderEvent)
    {
        try
        {
            if (orderEvent.Order == null)
                return;

            lock (_lockObject)
            {
                var historyItem = new OrderHistoryItem
                {
                    Order = orderEvent.Order,
                    EventType = orderEvent.EventType,
                    ReceivedAt = DateTime.UtcNow,
                    KitchenPrinted = false,
                    CashierPrinted = false,
                    Status = orderEvent.Order.Status
                };

                // Insert at the beginning (most recent first)
                _orders.Insert(0, historyItem);

                _logger.LogInformation("Order #{OrderNumber} added to history", orderEvent.Order.OrderNumber);

                // Notify subscribers
                OrderAdded?.Invoke(this, historyItem);

                // Keep only last 100 orders to prevent memory issues
                if (_orders.Count > 100)
                {
                    _orders.RemoveAt(_orders.Count - 1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding order to history");
        }
    }

    public void UpdatePrintStatus(string orderId, bool kitchenPrinted, bool cashierPrinted)
    {
        lock (_lockObject)
        {
            var order = _orders.FirstOrDefault(o => o.Order.Id == orderId);
            if (order != null)
            {
                order.KitchenPrinted = kitchenPrinted;
                order.CashierPrinted = cashierPrinted;
                order.LastPrintedAt = DateTime.UtcNow;
            }
        }
    }

    public void ClearHistory()
    {
        lock (_lockObject)
        {
            _orders.Clear();
            _logger.LogInformation("Order history cleared");
        }
    }

    public OrderHistoryItem? GetOrder(string orderId)
    {
        lock (_lockObject)
        {
            return _orders.FirstOrDefault(o => o.Order.Id == orderId);
        }
    }
}

public class OrderHistoryItem
{
    public Order Order { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool KitchenPrinted { get; set; }
    public bool CashierPrinted { get; set; }
    public DateTime? LastPrintedAt { get; set; }
    public string Status { get; set; } = string.Empty;

    public string DisplayText => $"Order #{Order.OrderNumber} - {Order.Type} - Table {Order.TableNumber} - {Order.Items.Count} items - ${Order.Total:F2}";
    public string ReceivedAtText => ReceivedAt.ToLocalTime().ToString("HH:mm:ss");
    public string StatusColor => KitchenPrinted && CashierPrinted ? "Green" : CashierPrinted || KitchenPrinted ? "Orange" : "Red";
}
