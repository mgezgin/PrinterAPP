using System.Text;
using Microsoft.Extensions.Logging;
using PrinterAPP.Models;

namespace PrinterAPP.Services;

public class OrderPrintService
{
    private readonly IPrinterService _printerService;
    private readonly RequestLogService _requestLogService;
    private readonly ILogger<OrderPrintService> _logger;

    // ESC/POS Commands for MAXIMUM darkness printing
    private const string ESC_INIT = "\x1B\x40"; // Initialize printer
    private const string ESC_BOLD_ON = "\x1B\x45\x01"; // Bold on
    private const string ESC_BOLD_OFF = "\x1B\x45\x00"; // Bold off
    private const string ESC_EMPHASIZED_ON = "\x1B\x47\x01"; // Emphasized/Double-strike on
    private const string ESC_EMPHASIZED_OFF = "\x1B\x47\x00"; // Emphasized off
    private const string ESC_DOUBLE_ON = "\x1D\x21\x11"; // Double width and height
    private const string ESC_DOUBLE_OFF = "\x1D\x21\x00"; // Normal size
    private const string ESC_LARGE_ON = "\x1D\x21\x22"; // 2x width, 3x height (larger size for kitchen)
    private const string ESC_ALIGN_CENTER = "\x1B\x61\x01"; // Center align
    private const string ESC_ALIGN_LEFT = "\x1B\x61\x00"; // Left align
    private const string ESC_CUT = "\x1D\x56\x00"; // Full cut
    private const string ESC_PARTIAL_CUT = "\x1D\x56\x01"; // Partial cut
    private const string ESC_FEED_AND_CUT = "\x1B\x64\x03"; // Feed 3 lines and cut
    private const string ESC_CODEPAGE_TURKISH = "\x1B\x74\x09"; // Set Turkish code page (PC857)

    // Combined commands for MAXIMUM darkness
    private const string EXTRA_DARK_ON = ESC_BOLD_ON + ESC_EMPHASIZED_ON; // Bold + Emphasized for maximum darkness
    private const string EXTRA_DARK_OFF = ESC_BOLD_OFF + ESC_EMPHASIZED_OFF; // Turn off all emphasis

    public OrderPrintService(
        IPrinterService printerService,
        RequestLogService requestLogService,
        ILogger<OrderPrintService> logger)
    {
        _printerService = printerService;
        _requestLogService = requestLogService;
        _logger = logger;
    }

    public async Task<bool> PrintOrderAsync(Order order, PrinterType printerType, CancellationToken cancellationToken = default)
    {
        // Extract order number for logging (parse the numeric part)
        int orderNumForLog = int.TryParse(order.OrderNumber.Split('/').Last(), out var orderNumParsed) ? orderNumParsed : 0;

        try
        {
            var config = await _printerService.LoadConfigurationAsync();

            string printerName;
            int copies;
            bool autoPrint;
            int paperWidth;

            if (printerType == PrinterType.Kitchen)
            {
                printerName = config.KitchenPrinterName;
                copies = config.KitchenPrintCopies;
                autoPrint = config.KitchenAutoPrint;
                paperWidth = config.KitchenPaperWidth;
            }
            else
            {
                printerName = config.CashierPrinterName;
                copies = config.CashierPrintCopies;
                autoPrint = config.CashierAutoPrint;
                paperWidth = config.CashierPaperWidth;
            }

            if (!autoPrint)
            {
                _logger.LogInformation("Auto-print disabled for {PrinterType}", printerType);
                return true;
            }

            if (string.IsNullOrWhiteSpace(printerName))
            {
                _logger.LogWarning("No printer configured for {PrinterType}", printerType);
                _requestLogService.LogPrintResponse(printerType.ToString(), orderNumForLog, false, "No printer configured");
                return false;
            }

            string content = printerType == PrinterType.Kitchen
                ? FormatKitchenReceipt(order, config, paperWidth)
                : FormatCashierReceipt(order, config, paperWidth);

            // Log print request with full content
            _requestLogService.LogPrintRequest(printerType.ToString(), orderNumForLog, printerName, content);

            bool success = true;
            for (int i = 0; i < copies; i++)
            {
                var result = await PrintRawContentAsync(printerName, content);
                success = success && result;

                if (i < copies - 1)
                {
                    await Task.Delay(500, cancellationToken); // Small delay between copies
                }
            }

            // Log print response
            if (success)
            {
                _logger.LogInformation("Successfully printed order #{OrderNumber} to {PrinterType} printer ({Copies} copies)",
                    order.OrderNumber, printerType, copies);
                _requestLogService.LogPrintResponse(printerType.ToString(), orderNumForLog, true, $"Printed {copies} {(copies > 1 ? "copies" : "copy")}");
            }
            else
            {
                _requestLogService.LogPrintResponse(printerType.ToString(), orderNumForLog, false, "Print operation failed");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing order #{OrderNumber} to {PrinterType}", order.OrderNumber, printerType);
            _requestLogService.LogPrintResponse(printerType.ToString(), orderNumForLog, false, $"Exception: {ex.Message}");
            return false;
        }
    }

    private string FormatKitchenReceipt(Order order, PrinterConfiguration config, int paperWidth)
    {
        var sb = new StringBuilder();

        // Initialize printer and set Turkish code page for character support
        sb.Append(ESC_INIT);
        sb.Append(ESC_CODEPAGE_TURKISH);

        // Header - EXTRA DARK (same size as cashier)
        sb.Append(ESC_ALIGN_CENTER);
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine($"=== KITCHEN ORDER ===");
        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine();

        // Convert to local time if needed
        var localTime = order.OrderDate.Kind == DateTimeKind.Utc
            ? order.OrderDate.ToLocalTime()
            : order.OrderDate;

        // Order info - EXTRA DARK
        sb.Append(ESC_ALIGN_LEFT);
        sb.Append(EXTRA_DARK_ON);

        // Order # and Table - LARGER for visibility
        sb.Append(ESC_DOUBLE_ON);
        sb.AppendLine($"Order #: {order.OrderNumber}");
        sb.AppendLine($"Table: {order.TableNumber}");
        sb.Append(ESC_DOUBLE_OFF);

        // Rest of info - normal size
        sb.AppendLine($"Type: {order.Type}");
        sb.AppendLine($"Time: {localTime:HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(order.CustomerName))
        {
            sb.AppendLine($"Customer: {order.CustomerName}");
        }
        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Items header - EXTRA DARK (same size as cashier)
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine("ITEMS:");
        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine();

        // Log item count for debugging
        _logger.LogInformation("Printing {ItemCount} items for order {OrderNumber}", order.Items?.Count ?? 0, order.OrderNumber);

        if (order.Items != null && order.Items.Any())
        {
            foreach (var item in order.Items)
            {
                // Log each item for debugging
                _logger.LogInformation("Item: {Quantity}x {ProductName}", item.Quantity, item.ProductName);

                // Item name and quantity - EXTRA DARK (NO PRICES for kitchen, same size as cashier)
                sb.Append(EXTRA_DARK_ON);
                sb.AppendLine($"{item.Quantity}x {item.ProductName}");
                sb.Append(EXTRA_DARK_OFF);

                // Show variation if available
                if (!string.IsNullOrWhiteSpace(item.VariationName))
                {
                    sb.Append(EXTRA_DARK_ON);
                    sb.AppendLine($"   Variation: {item.VariationName}");
                    sb.Append(EXTRA_DARK_OFF);
                }

                // Show special instructions
                if (!string.IsNullOrWhiteSpace(item.SpecialInstructions))
                {
                    // Item notes - EXTRA DARK for visibility
                    sb.Append(EXTRA_DARK_ON);
                    sb.AppendLine($"   NOTE: {item.SpecialInstructions}");
                    sb.Append(EXTRA_DARK_OFF);
                }
                sb.AppendLine();
            }
        }
        else
        {
            // No items found - log warning
            _logger.LogWarning("No items found in order {OrderNumber}", order.OrderNumber);
            sb.AppendLine("(No items in order)");
        }

        // Order notes - EXTRA DARK for important information (same size as cashier)
        if (!string.IsNullOrWhiteSpace(order.Notes))
        {
            sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));
            sb.Append(EXTRA_DARK_ON);
            sb.AppendLine("ORDER NOTES:");
            sb.AppendLine(order.Notes);
            sb.Append(EXTRA_DARK_OFF);
            sb.AppendLine();
        }

        // Delivery address for delivery orders (same size as cashier)
        if (order.Type == "Delivery" && !string.IsNullOrWhiteSpace(order.DeliveryAddress))
        {
            sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));
            sb.Append(EXTRA_DARK_ON);
            sb.AppendLine("DELIVERY ADDRESS:");
            sb.AppendLine(order.DeliveryAddress);
            sb.Append(EXTRA_DARK_OFF);
            sb.AppendLine();
        }

        // Footer - EXTRA DARK for urgent message (same size as cashier)
        sb.AppendLine(new string('=', paperWidth == 80 ? 48 : 32));
        sb.Append(ESC_ALIGN_CENTER);
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine("PREPARE IMMEDIATELY");
        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        sb.Append(ESC_FEED_AND_CUT);

        return sb.ToString();
    }

    private string FormatCashierReceipt(Order order, PrinterConfiguration config, int paperWidth)
    {
        var sb = new StringBuilder();

        // Initialize printer and set Turkish code page for character support
        sb.Append(ESC_INIT);
        sb.Append(ESC_CODEPAGE_TURKISH);

        // Header - EXTRA DARK, Bold, and Double Size
        sb.Append(ESC_ALIGN_CENTER);
        sb.Append(ESC_DOUBLE_ON);
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine($"{config.RestaurantName}");
        sb.Append(ESC_DOUBLE_OFF);

        sb.AppendLine("RECEIPT");
        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine();

        // Convert to local time if needed
        var localTime = order.OrderDate.Kind == DateTimeKind.Utc
            ? order.OrderDate.ToLocalTime()
            : order.OrderDate;

        // Order info - EXTRA DARK for visibility
        sb.Append(ESC_ALIGN_LEFT);
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine($"Order #: {order.OrderNumber}");
        sb.AppendLine($"Type: {order.Type}");
        sb.AppendLine($"Table: {order.TableNumber}");
        sb.AppendLine($"Date: {localTime:yyyy-MM-dd HH:mm}");
        if (!string.IsNullOrWhiteSpace(order.CustomerName))
        {
            sb.AppendLine($"Customer: {order.CustomerName}");
        }
        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            sb.AppendLine($"Phone: {order.CustomerPhone}");
        }
        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Items with prices - EXTRA DARK
        if (order.Items != null && order.Items.Any())
        {
            foreach (var item in order.Items)
            {
                var itemName = item.ProductName;
                if (!string.IsNullOrWhiteSpace(item.VariationName))
                {
                    itemName += $" ({item.VariationName})";
                }

                var itemLine = $"{item.Quantity}x {itemName}";
                var price = $"${item.ItemTotal:F2}";
                var spacing = paperWidth == 80 ? 48 : 32;
                var dots = spacing - itemLine.Length - price.Length;

                sb.Append(EXTRA_DARK_ON);
                sb.Append(itemLine);
                sb.Append(new string('.', Math.Max(1, dots)));
                sb.AppendLine(price);
                sb.Append(EXTRA_DARK_OFF);

                if (!string.IsNullOrWhiteSpace(item.SpecialInstructions))
                {
                    sb.AppendLine($"  * {item.SpecialInstructions}");
                }
            }
        }
        else
        {
            _logger.LogWarning("No items found in cashier receipt for order {OrderNumber}", order.OrderNumber);
            sb.AppendLine("(No items in order)");
        }

        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Subtotal, Tax, Discount, Delivery Fee, Tip - EXTRA DARK
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine($"Subtotal: ${order.SubTotal:F2}");

        if (order.Tax > 0)
        {
            sb.AppendLine($"Tax: ${order.Tax:F2}");
        }

        if (order.Discount > 0)
        {
            sb.AppendLine($"Discount ({order.DiscountPercentage}%): -${order.Discount:F2}");
        }

        if (order.DeliveryFee > 0)
        {
            sb.AppendLine($"Delivery Fee: ${order.DeliveryFee:F2}");
        }

        if (order.Tip > 0)
        {
            sb.AppendLine($"Tip: ${order.Tip:F2}");
        }

        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Total - EXTRA DARK, Bold and larger for maximum visibility
        sb.Append(ESC_DOUBLE_ON);
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine($"TOTAL: ${order.Total:F2}");
        sb.Append(EXTRA_DARK_OFF);
        sb.Append(ESC_DOUBLE_OFF);
        sb.AppendLine();

        // Payment information
        if (order.Payments != null && order.Payments.Any())
        {
            sb.Append(EXTRA_DARK_ON);
            sb.AppendLine("PAYMENT:");
            foreach (var payment in order.Payments)
            {
                sb.AppendLine($"{payment.PaymentMethod}: ${payment.Amount:F2}");
            }
            sb.Append(EXTRA_DARK_OFF);
            sb.AppendLine();
        }

        // Delivery address for delivery orders
        if (order.Type == "Delivery" && !string.IsNullOrWhiteSpace(order.DeliveryAddress))
        {
            sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));
            sb.Append(EXTRA_DARK_ON);
            sb.AppendLine("DELIVERY TO:");
            sb.AppendLine(order.DeliveryAddress);
            sb.Append(EXTRA_DARK_OFF);
            sb.AppendLine();
        }

        // Footer
        sb.AppendLine(new string('=', paperWidth == 80 ? 48 : 32));
        sb.Append(ESC_ALIGN_CENTER);
        sb.AppendLine("Thank you for your visit!");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        sb.Append(ESC_FEED_AND_CUT);

        return sb.ToString();
    }

    private async Task<bool> PrintRawContentAsync(string printerName, string content)
    {
        try
        {
            // Use the Windows printer service to print raw content
            var result = await Task.Run(() =>
            {
                return PrintToWindowsPrinter(printerName, content);
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print to {PrinterName}", printerName);
            return false;
        }
    }

    private bool PrintToWindowsPrinter(string printerName, string content)
    {
#if WINDOWS
        // Use Windows-1254 (Turkish) encoding for proper Turkish character support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1254); // Windows-1254 Turkish encoding
        var bytes = encoding.GetBytes(content);

        var docInfo = new DOCINFOA
        {
            pDocName = "Restaurant Order",
            pDataType = "RAW"
        };

        if (OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
        {
            try
            {
                if (StartDocPrinter(hPrinter, 1, ref docInfo))
                {
                    try
                    {
                        if (StartPagePrinter(hPrinter))
                        {
                            try
                            {
                                WritePrinter(hPrinter, bytes, bytes.Length, out int bytesWritten);
                                return bytesWritten == bytes.Length;
                            }
                            finally
                            {
                                EndPagePrinter(hPrinter);
                            }
                        }
                    }
                    finally
                    {
                        EndDocPrinter(hPrinter);
                    }
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }
#endif
        return false;
    }

#if WINDOWS
    // P/Invoke declarations for Windows printing
    [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [System.Runtime.InteropServices.DllImport("winspool.drv")]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOA pDocInfo);

    [System.Runtime.InteropServices.DllImport("winspool.drv")]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv")]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv")]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv")]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private struct DOCINFOA
    {
        public string pDocName;
        public string? pOutputFile;
        public string pDataType;
    }
#endif

    public enum PrinterType
    {
        Kitchen,
        Cashier
    }
}
