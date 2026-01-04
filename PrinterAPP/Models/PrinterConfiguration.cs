using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PrinterAPP.Models;

public class PrinterConfiguration
{
    public string ApiBaseUrl { get; set; } = "https://www.rumirestaurant.ch";
    public string ApiToken { get; set; } = "";  // JWT token for API authentication

    // Front Kitchen Printer Settings (for drinks, desserts, etc.)
    public string FrontKitchenPrinterName { get; set; } = "";
    public bool FrontKitchenAutoPrint { get; set; } = true;
    public int FrontKitchenPaperWidth { get; set; } = 80;

    // Back Kitchen Printer Settings (for main dishes, grills, etc.)
    public string BackKitchenPrinterName { get; set; } = "";
    public bool BackKitchenAutoPrint { get; set; } = true;
    public int BackKitchenPaperWidth { get; set; } = 80;

    // Legacy Kitchen Printer (fallback if specific kitchens not configured)
    public string KitchenPrinterName { get; set; } = "";
    public bool KitchenAutoPrint { get; set; } = true;
    public int KitchenPrintCopies { get; set; } = 1;
    public int KitchenPaperWidth { get; set; } = 80;

    // Cashier Printer Settings
    public string CashierPrinterName { get; set; } = "";
    public bool CashierAutoPrint { get; set; } = true;
    public int CashierPrintCopies { get; set; } = 1;
    public int CashierPaperWidth { get; set; } = 80;

    // Time-based Auto-Print Restrictions
    public bool EnableTimeRestriction { get; set; } = false;
    public TimeSpan RestrictStartTime { get; set; } = new TimeSpan(12, 0, 0); // 12:00
    public TimeSpan RestrictEndTime { get; set; } = new TimeSpan(13, 0, 0); // 13:00

    // Legacy properties for backward compatibility
    [Obsolete("Use KitchenPrinterName instead")]
    public string PrinterName { get; set; } = "";
    [Obsolete("Use KitchenAutoPrint instead")]
    public bool AutoPrint { get; set; } = true;
    [Obsolete("Use KitchenPrintCopies instead")]
    public int PrintCopies { get; set; } = 1;
    [Obsolete("Use KitchenPaperWidth instead")]
    public int PaperWidth { get; set; } = 80;

    // Restaurant Information
    public string RestaurantName { get; set; } = "Rumi Restaurant";
    public string KitchenLocation { get; set; } = "Main Kitchen";

    // Service Status
    public bool IsServiceRunning { get; set; } = false;
}