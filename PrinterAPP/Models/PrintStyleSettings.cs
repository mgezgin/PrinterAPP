namespace PrinterAPP.Models;

/// <summary>
/// Defines the styling options for a specific section of a receipt
/// </summary>
public class SectionStyle
{
    public string SectionName { get; set; } = string.Empty;
    public FontSize Size { get; set; } = FontSize.Normal;
    public bool IsBold { get; set; } = false;
    public bool IsEmphasized { get; set; } = false;
    public PrintAlignment Alignment { get; set; } = PrintAlignment.Left;
}

/// <summary>
/// Font size options aligned with ESC/POS commands
/// </summary>
public enum FontSize
{
    Normal = 0,      // 1x width, 1x height
    Tall = 1,        // 1x width, 2x height
    Wide = 2,        // 2x width, 1x height
    Double = 3,      // 2x width, 2x height
    Large = 4        // 2x width, 3x height (for kitchen headers)
}

/// <summary>
/// Text alignment options for print sections
/// </summary>
public enum PrintAlignment
{
    Left = 0,
    Center = 1,
    Right = 2
}

/// <summary>
/// Complete print style settings for all receipt types
/// </summary>
public class PrintStyleSettings
{
    // Kitchen Receipt Sections
    public SectionStyle KitchenHeader { get; set; } = new() 
    { 
        SectionName = "Kitchen Header",
        Size = FontSize.Normal,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Center
    };
    
    public SectionStyle KitchenOrderInfo { get; set; } = new()
    {
        SectionName = "Order Info (ID, Date, Type)",
        Size = FontSize.Normal,
        IsBold = true,
        IsEmphasized = false,
        Alignment = PrintAlignment.Left
    };
    
    public SectionStyle KitchenOrderType { get; set; } = new()
    {
        SectionName = "Order Type",
        Size = FontSize.Tall,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Left
    };
    
    public SectionStyle KitchenItemName { get; set; } = new()
    {
        SectionName = "Item Names",
        Size = FontSize.Wide,
        IsBold = true,
        IsEmphasized = false,
        Alignment = PrintAlignment.Left
    };
    
    public SectionStyle KitchenItemQuantity { get; set; } = new()
    {
        SectionName = "Item Quantities",
        Size = FontSize.Wide,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Left
    };
    
    public SectionStyle KitchenIngredients { get; set; } = new()
    {
        SectionName = "Ingredients/Notes",
        Size = FontSize.Tall,
        IsBold = false,
        IsEmphasized = false,
        Alignment = PrintAlignment.Left
    };
    
    // Cashier Receipt Sections
    public SectionStyle CashierHeader { get; set; } = new()
    {
        SectionName = "Cashier Header",
        Size = FontSize.Double,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Center
    };
    
    public SectionStyle CashierOrderInfo { get; set; } = new()
    {
        SectionName = "Order Info",
        Size = FontSize.Normal,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Left
    };
    
    public SectionStyle CashierItemLine { get; set; } = new()
    {
        SectionName = "Item Lines",
        Size = FontSize.Normal,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Left
    };
    
    public SectionStyle CashierTotals { get; set; } = new()
    {
        SectionName = "Totals",
        Size = FontSize.Normal,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Left
    };
    
    public SectionStyle CashierGrandTotal { get; set; } = new()
    {
        SectionName = "Grand Total",
        Size = FontSize.Double,
        IsBold = true,
        IsEmphasized = true,
        Alignment = PrintAlignment.Left
    };

    /// <summary>
    /// Returns default/factory settings
    /// </summary>
    public static PrintStyleSettings GetDefaults()
    {
        return new PrintStyleSettings();
    }
}
