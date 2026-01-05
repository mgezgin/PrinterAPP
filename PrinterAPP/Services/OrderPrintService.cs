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
    private const string ESC_CODEPAGE_TURKISH = "\x1B\x74\x09"; // Set PC857 code page (Turkish MS-DOS - supports Turkish + Western European)

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

    /// <summary>
    /// Prints order to all appropriate printers: Cashier + FrontKitchen + BackKitchen (filtered by item KitchenType)
    /// </summary>
    public async Task<(bool Cashier, bool FrontKitchen, bool BackKitchen)> PrintOrderToAllPrintersAsync(
        Order order, 
        bool isManualPrint = false, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🖨️ Printing order {OrderNumber} to all printers", order.OrderNumber);

        var config = await _printerService.LoadConfigurationAsync();
        bool cashierSuccess = false;
        bool frontKitchenSuccess = false;
        bool backKitchenSuccess = false;

        // 1. ALWAYS print to Cashier (full receipt with prices)
        try
        {
            cashierSuccess = await PrintOrderAsync(order, PrinterType.Cashier, isManualPrint, cancellationToken);
            _logger.LogInformation("Cashier print: {Result}", cashierSuccess ? "✓" : "✗");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing to cashier");
        }

        // 2. Check which kitchens have items
        var hasFrontKitchenItems = order.Items?.Any(i => 
            string.Equals(i.KitchenType, "FrontKitchen", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasBackKitchenItems = order.Items?.Any(i => 
            string.Equals(i.KitchenType, "BackKitchen", StringComparison.OrdinalIgnoreCase)) ?? false;

        // 3. Print to FrontKitchen if there are FrontKitchen items
        if (hasFrontKitchenItems)
        {
            try
            {
                // Logic: Use dedicated FrontPrinter if exists, otherwise fallback to CashierPrinter (NOT KitchenPrinter/BackPrinter)
                var frontKitchenPrinter = !string.IsNullOrWhiteSpace(config.FrontKitchenPrinterName) 
                    ? config.FrontKitchenPrinterName 
                    : config.CashierPrinterName; // Fallback to Cashier Printer as requested

                if (!string.IsNullOrWhiteSpace(frontKitchenPrinter) && config.FrontKitchenAutoPrint)
                {
                    // Filter order to only FrontKitchen items
                    var filteredOrder = CreateFilteredOrder(order, "FrontKitchen");
                    var content = FormatKitchenReceipt(filteredOrder, config, config.FrontKitchenPaperWidth, "FRONT KITCHEN");
                    frontKitchenSuccess = await PrintRawContentAsync(frontKitchenPrinter, content);
                    _logger.LogInformation("FrontKitchen print ({ItemCount} items) to {Printer}: {Result}", 
                        filteredOrder.Items?.Count ?? 0, frontKitchenPrinter, frontKitchenSuccess ? "✓" : "✗");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing to FrontKitchen");
            }
        }
        else
        {
            frontKitchenSuccess = true; // No items to print
        }

        // 4. Print to BackKitchen if there are BackKitchen items
        if (hasBackKitchenItems)
        {
            try
            {
                var backKitchenPrinter = !string.IsNullOrWhiteSpace(config.BackKitchenPrinterName) 
                    ? config.BackKitchenPrinterName 
                    : config.KitchenPrinterName;  // Fallback to legacy

                if (!string.IsNullOrWhiteSpace(backKitchenPrinter) && config.BackKitchenAutoPrint)
                {
                    // Filter order to only BackKitchen items
                    var filteredOrder = CreateFilteredOrder(order, "BackKitchen");
                    var content = FormatKitchenReceipt(filteredOrder, config, config.BackKitchenPaperWidth, "Back Kitchen");
                    backKitchenSuccess = await PrintRawContentAsync(backKitchenPrinter, content);
                    _logger.LogInformation("BackKitchen print ({ItemCount} items): {Result}", 
                        filteredOrder.Items?.Count ?? 0, backKitchenSuccess ? "✓" : "✗");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing to BackKitchen");
            }
        }
        else
        {
            backKitchenSuccess = true; // No items to print
        }

        _logger.LogInformation("🖨️ Print complete for order {OrderNumber}: Cashier={C}, Front={F}, Back={B}",
            order.OrderNumber, cashierSuccess, frontKitchenSuccess, backKitchenSuccess);

        return (cashierSuccess, frontKitchenSuccess, backKitchenSuccess);
    }

    /// <summary>
    /// Creates a copy of the order with only items matching the specified KitchenType
    /// </summary>
    private Order CreateFilteredOrder(Order original, string kitchenType)
    {
        return new Order
        {
            Id = original.Id,
            OrderNumber = original.OrderNumber,
            UserId = original.UserId,
            CustomerName = original.CustomerName,
            CustomerEmail = original.CustomerEmail,
            CustomerPhone = original.CustomerPhone,
            Type = original.Type,
            TableNumber = original.TableNumber,
            SubTotal = original.SubTotal,
            Tax = original.Tax,
            DeliveryFee = original.DeliveryFee,
            Discount = original.Discount,
            DiscountPercentage = original.DiscountPercentage,
            Tip = original.Tip,
            Total = original.Total,
            TotalPaid = original.TotalPaid,
            RemainingAmount = original.RemainingAmount,
            IsFullyPaid = original.IsFullyPaid,
            Status = original.Status,
            PaymentStatus = original.PaymentStatus,
            OrderDate = original.OrderDate,
            CreatedAt = original.CreatedAt,
            UpdatedAt = original.UpdatedAt,
            Notes = original.Notes,
            DeliveryAddress = original.DeliveryAddress,
            Payments = original.Payments,
            StatusHistory = original.StatusHistory,
            // Filter items to only those matching the kitchen type
            Items = original.Items?
                .Where(i => string.Equals(i.KitchenType, kitchenType, StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<OrderItem>()
        };
    }

    public async Task<bool> PrintOrderAsync(Order order, PrinterType printerType, bool isManualPrint = false, CancellationToken cancellationToken = default)
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

            // Check auto-print settings (only for automatic printing)
            if (!isManualPrint && !autoPrint)
            {
                _logger.LogInformation("Auto-print disabled for {PrinterType}", printerType);
                return true;
            }

            // Check time restrictions (only for automatic printing)
            if (!isManualPrint && config.EnableTimeRestriction)
            {
                var now = DateTime.Now.TimeOfDay;
                bool isInRestrictedTime = false;

                if (config.RestrictStartTime < config.RestrictEndTime)
                {
                    // Normal case: e.g., 12:00 - 13:00
                    isInRestrictedTime = now >= config.RestrictStartTime && now < config.RestrictEndTime;
                }
                else
                {
                    // Overnight case: e.g., 23:00 - 01:00
                    isInRestrictedTime = now >= config.RestrictStartTime || now < config.RestrictEndTime;
                }

                if (isInRestrictedTime)
                {
                    _logger.LogInformation("Auto-print skipped for {PrinterType} - current time {Time} is within restricted period {Start}-{End}",
                        printerType, now.ToString(@"hh\:mm"), config.RestrictStartTime.ToString(@"hh\:mm"), config.RestrictEndTime.ToString(@"hh\:mm"));
                    return true; // Return true to indicate no error, just skipped
                }
            }

            if (string.IsNullOrWhiteSpace(printerName))
            {
                _logger.LogWarning("No printer configured for {PrinterType}", printerType);
                _requestLogService.LogPrintResponse(printerType.ToString(), orderNumForLog, false, "No printer configured");
                return false;
            }

            // Cashier printer only prints the bill (no duplicate kitchen format)
            // Kitchen tickets are handled by dedicated kitchen printers in PrintOrderToAllPrintersAsync
            bool success = true;
            // Print the appropriate format
            string content = printerType == PrinterType.Kitchen
                ? FormatKitchenReceipt(order, config, paperWidth)
                : FormatCashierReceipt(order, config, paperWidth);

            // Log print request with full content
            _requestLogService.LogPrintRequest(printerType.ToString(), orderNumForLog, printerName, content);

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

    /// <summary>
    /// Applies strikethrough effect to text using Unicode combining character
    /// </summary>
    private string ApplyStrikethrough(string text)
    {
        // Unicode U+0336 is a combining long stroke overlay that creates strikethrough
        const char strikethroughChar = '\u0336';
        var result = new StringBuilder();
        
        foreach (char c in text)
        {
            result.Append(c);
            result.Append(strikethroughChar);
        }
        
        return result.ToString();
    }

    private string FormatKitchenReceipt(Order order, PrinterConfiguration config, int paperWidth, string? kitchenName = null)
    {
        var sb = new StringBuilder();

        // Initialize printer and set Turkish code page for character support
        sb.Append(ESC_INIT);
        sb.Append(ESC_CODEPAGE_TURKISH);

        // KITCHEN NAME HEADER - EXTRA LARGE
        if (!string.IsNullOrEmpty(kitchenName))
        {
            sb.Append(ESC_ALIGN_CENTER);
            sb.Append(ESC_DOUBLE_ON);
            sb.Append(EXTRA_DARK_ON);
            
            // Preserve Turkish characters - don't use ToUpperInvariant as it breaks special characters
            // The PC857 codepage will handle Turkish characters correctly
            sb.AppendLine($"*** {kitchenName} ***");
            sb.Append(EXTRA_DARK_OFF);
            sb.Append(ESC_DOUBLE_OFF);
            sb.AppendLine();
        }

        // Order number + Date/Time on one line - EXTRA DARK for visibility
        sb.Append(ESC_ALIGN_LEFT);
        sb.Append(EXTRA_DARK_ON);
        var localTime = order.OrderDate.Kind == DateTimeKind.Utc
            ? order.OrderDate.ToLocalTime()
            : order.OrderDate;
        sb.AppendLine($"{order.OrderNumber} - {localTime:dd/MM/yyyy HH:mm}");
        sb.Append(EXTRA_DARK_OFF);

        // Type + Table on one line
        sb.Append(EXTRA_DARK_ON);
        if (order.TableNumber.HasValue && order.TableNumber.Value > 0)
        {
            sb.AppendLine($"Type: {order.Type} - Table {order.TableNumber}");
        }
        else
        {
            sb.AppendLine($"Type: {order.Type}");
        }
        sb.Append(EXTRA_DARK_OFF);

        // Customer name only (no phone)
        if (!string.IsNullOrWhiteSpace(order.CustomerName))
        {
            sb.Append(EXTRA_DARK_ON);
            sb.AppendLine($"Customer: {order.CustomerName}");
            sb.Append(EXTRA_DARK_OFF);
        }


        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Log item count for debugging
        _logger.LogInformation("Printing {ItemCount} items for order {OrderNumber}", order.Items?.Count ?? 0, order.OrderNumber);

        if (order.Items != null && order.Items.Any())
        {
            foreach (var item in order.Items)
            {
                // Log each item for debugging
                _logger.LogInformation("Item: {Quantity}x {ProductName}", item.Quantity, item.ProductName);

                // Item name and quantity - DOUBLE size (reduced from LARGE)
                sb.Append(ESC_DOUBLE_ON);
                sb.Append(EXTRA_DARK_ON);
                sb.AppendLine($"{item.Quantity}x {item.ProductName}");
                sb.Append(EXTRA_DARK_OFF);
                sb.Append(ESC_DOUBLE_OFF);

                // Show variation if available (normal size)
                if (!string.IsNullOrWhiteSpace(item.VariationName))
                {
                    sb.Append(EXTRA_DARK_ON);
                    sb.AppendLine($"   - {item.VariationName}");
                    sb.Append(EXTRA_DARK_OFF);
                }

                // Show ingredient customizations (added/removed ingredients)
                if (item.IngredientCustomizations != null && item.IngredientCustomizations.Any())
                {
                    foreach (var ing in item.IngredientCustomizations)
                    {
                        if (ing.IsRemoved)
                        {
                            // Apply strikethrough using Unicode combining character
                            // U+0336 (combining long stroke overlay) adds strikethrough to each character
                            var strikethrough = ApplyStrikethrough(ing.IngredientName);
                            sb.Append(ESC_BOLD_ON); // Turn on bold for emphasis
                            sb.AppendLine($"   {strikethrough}");
                            sb.Append(ESC_BOLD_OFF);
                        }
                        else if (ing.Quantity > 1)
                        {
                            sb.Append(ESC_BOLD_ON);
                            sb.AppendLine($"   + EXTRA {ing.IngredientName}");
                            sb.Append(ESC_BOLD_OFF);
                        }
                        else 
                        {
                            // Selected ingredient (standard included ones usually don't show here unless customized)
                            // But if it's in the list and not removed/extra, it means it's explicitly selected/changed?
                            // Frontend logic: show if isRemoved OR quantity > 1.
                            // If it's just quantity 1 and not removed, it might be a mandatory choice.
                            sb.AppendLine($"   • {ing.IngredientName}");
                        }
                    }
                }

                // Show side items / additionals
                if (item.SideItems != null && item.SideItems.Any())
                {
                    foreach (var side in item.SideItems)
                    {
                        sb.AppendLine($"   + {side.Quantity}x {side.ProductName}");
                    }
                }

                // Show special instructions (normal size)
                if (!string.IsNullOrWhiteSpace(item.SpecialInstructions))
                {
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

        sb.AppendLine();
        sb.AppendLine();

        // Use GS V 0 (Standard Full Cut) instead of FEED_AND_CUT which might be model specific
        sb.Append(ESC_CUT);

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

        // Order number + Date/Time on one line - EXTRA DARK for visibility
        sb.Append(ESC_ALIGN_LEFT);
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine($"{order.OrderNumber} - {localTime:dd/MM/yyyy HH:mm}");
        sb.Append(EXTRA_DARK_OFF);

        // Type + Table on one line
        sb.Append(EXTRA_DARK_ON);
        if (order.TableNumber.HasValue && order.TableNumber.Value > 0)
        {
            sb.AppendLine($"Type: {order.Type} - Table {order.TableNumber}");
        }
        else
        {
            sb.AppendLine($"Type: {order.Type}");
        }
        sb.Append(EXTRA_DARK_OFF);

        // Customer name only (no phone/email)
        if (!string.IsNullOrWhiteSpace(order.CustomerName))
        {
            sb.Append(EXTRA_DARK_ON);
            sb.AppendLine($"Customer: {order.CustomerName}");
            sb.Append(EXTRA_DARK_OFF);
        }
        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Items with prices - simplified (no customizations)
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
                var price = $"CHF {item.ItemTotal:F2}";
                var spacing = paperWidth == 80 ? 48 : 32;
                var dots = spacing - itemLine.Length - price.Length;

                sb.Append(EXTRA_DARK_ON);
                sb.Append(itemLine);
                sb.Append(new string('.', Math.Max(1, dots)));
                sb.AppendLine(price);
                sb.Append(EXTRA_DARK_OFF);
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
        sb.AppendLine($"Subtotal: CHF {order.SubTotal:F2}");

        if (order.Tax > 0)
        {
            sb.AppendLine($"Tax: CHF {order.Tax:F2}");
        }

        if (order.Discount > 0)
        {
            sb.AppendLine($"Discount ({order.DiscountPercentage}%): -CHF {order.Discount:F2}");
        }

        if (order.DeliveryFee > 0)
        {
            sb.AppendLine($"Delivery Fee: CHF {order.DeliveryFee:F2}");
        }

        if (order.Tip > 0)
        {
            sb.AppendLine($"Tip: CHF {order.Tip:F2}");
        }

        sb.Append(EXTRA_DARK_OFF);
        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Total - EXTRA DARK, Bold and larger for maximum visibility
        sb.Append(ESC_DOUBLE_ON);
        sb.Append(EXTRA_DARK_ON);
        sb.AppendLine($"TOTAL: CHF {order.Total:F2}");
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
                sb.AppendLine($"{payment.PaymentMethod}: CHF {payment.Amount:F2}");
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

        sb.Append(ESC_CUT);

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
        // Use PC857 (Turkish MS-DOS) encoding for Turkish + Western European character support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(857); // PC857 (Turkish MS-DOS) - supports Turkish ç,ğ,ı,ö,ş,ü AND Western European è,é,à,ò
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
