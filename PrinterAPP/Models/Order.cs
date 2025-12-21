namespace PrinterAPP.Models;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string Type { get; set; } = string.Empty; // DineIn, TakeAway, Delivery
    public int? TableNumber { get; set; } // Nullable for Takeaway/Delivery orders
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Discount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal Tip { get; set; }
    public decimal Total { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingAmount { get; set; }
    public bool IsFullyPaid { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, InProgress, Completed, Cancelled
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Notes { get; set; }
    public string? DeliveryAddress { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public List<Payment>? Payments { get; set; }
    public List<OrderStatusHistory>? StatusHistory { get; set; }
}

public class OrderItem
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string? ProductVariationId { get; set; }
    public string? MenuID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariationName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ItemTotal { get; set; }
    public string? SpecialInstructions { get; set; }
}

public class Payment
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty; // Cash, Card, etc.
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? CardLastFourDigits { get; set; }
    public string? CardType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderStatusHistory
{
    public string Id { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
}

public class OrderEvent
{
    public string EventType { get; set; } = string.Empty; // "order-created", "order-updated", etc.
    public Order? Order { get; set; }
    public string? PreviousStatus { get; set; }
    public DateTime Timestamp { get; set; }
}
