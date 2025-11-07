namespace PrinterAPP.Models;

public class Order
{
    public int Id { get; set; }
    public int TableNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? WaiterName { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public string? Category { get; set; }
}

public class OrderEvent
{
    public string EventType { get; set; } = string.Empty; // "created", "updated", "completed", etc.
    public Order? Order { get; set; }
    public DateTime Timestamp { get; set; }
}
