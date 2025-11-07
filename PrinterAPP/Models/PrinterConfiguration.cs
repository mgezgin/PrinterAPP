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
    public string ApiBaseUrl { get; set; } = "https://localhost:44386";

    // Kitchen Printer Settings
    public string KitchenPrinterName { get; set; } = "";
    public bool KitchenAutoPrint { get; set; } = true;
    public int KitchenPrintCopies { get; set; } = 1;
    public int KitchenPaperWidth { get; set; } = 80;

    // Cashier Printer Settings
    public string CashierPrinterName { get; set; } = "";
    public bool CashierAutoPrint { get; set; } = true;
    public int CashierPrintCopies { get; set; } = 1;
    public int CashierPaperWidth { get; set; } = 80;

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
    public string RestaurantName { get; set; } = "Your Restaurant";
    public string KitchenLocation { get; set; } = "Main Kitchen";

    // Service Status
    public bool IsServiceRunning { get; set; } = false;
}