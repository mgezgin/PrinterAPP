using System.Text;
using PrinterAPP.Models;

namespace PrinterAPP.Services;

public class OrderPrintService
{
    private readonly IPrinterService _printerService;
    private readonly ILogger<OrderPrintService> _logger;

    // ESC/POS Commands for darker printing
    private const string ESC_INIT = "\x1B\x40"; // Initialize printer
    private const string ESC_BOLD_ON = "\x1B\x45\x01"; // Bold on
    private const string ESC_BOLD_OFF = "\x1B\x45\x00"; // Bold off
    private const string ESC_DOUBLE_ON = "\x1D\x21\x11"; // Double width and height
    private const string ESC_DOUBLE_OFF = "\x1D\x21\x00"; // Normal size
    private const string ESC_ALIGN_CENTER = "\x1B\x61\x01"; // Center align
    private const string ESC_ALIGN_LEFT = "\x1B\x61\x00"; // Left align
    private const string ESC_CUT = "\x1D\x56\x00"; // Full cut
    private const string ESC_PARTIAL_CUT = "\x1D\x56\x01"; // Partial cut
    private const string ESC_FEED_AND_CUT = "\x1B\x64\x03"; // Feed 3 lines and cut

    public OrderPrintService(IPrinterService printerService, ILogger<OrderPrintService> logger)
    {
        _printerService = printerService;
        _logger = logger;
    }

    public async Task<bool> PrintOrderAsync(Order order, PrinterType printerType, CancellationToken cancellationToken = default)
    {
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
                return false;
            }

            string content = printerType == PrinterType.Kitchen
                ? FormatKitchenReceipt(order, config, paperWidth)
                : FormatCashierReceipt(order, config, paperWidth);

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

            if (success)
            {
                _logger.LogInformation("Successfully printed order #{OrderId} to {PrinterType} printer ({Copies} copies)",
                    order.Id, printerType, copies);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing order #{OrderId} to {PrinterType}", order.Id, printerType);
            return false;
        }
    }

    private string FormatKitchenReceipt(Order order, PrinterConfiguration config, int paperWidth)
    {
        var sb = new StringBuilder();

        // Initialize and set bold for better darkness
        sb.Append(ESC_INIT);

        // Header - Bold and Double Size
        sb.Append(ESC_ALIGN_CENTER);
        sb.Append(ESC_DOUBLE_ON);
        sb.Append(ESC_BOLD_ON);
        sb.AppendLine($"{config.RestaurantName}");
        sb.Append(ESC_DOUBLE_OFF);
        sb.Append(ESC_BOLD_OFF);

        sb.Append(ESC_BOLD_ON);
        sb.AppendLine($"=== KITCHEN ORDER ===");
        sb.Append(ESC_BOLD_OFF);
        sb.AppendLine();

        // Order info - Bold for visibility
        sb.Append(ESC_ALIGN_LEFT);
        sb.Append(ESC_BOLD_ON);
        sb.AppendLine($"Order #: {order.Id}");
        sb.AppendLine($"Table: {order.TableNumber}");
        sb.AppendLine($"Time: {order.CreatedAt:HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(order.WaiterName))
        {
            sb.AppendLine($"Waiter: {order.WaiterName}");
        }
        sb.Append(ESC_BOLD_OFF);
        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Items - Make them bold and clear
        sb.Append(ESC_BOLD_ON);
        sb.AppendLine("ITEMS:");
        sb.Append(ESC_BOLD_OFF);
        sb.AppendLine();

        foreach (var item in order.Items)
        {
            sb.Append(ESC_BOLD_ON);
            sb.AppendLine($"{item.Quantity}x {item.Name}");
            sb.Append(ESC_BOLD_OFF);

            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                sb.Append(ESC_BOLD_ON);
                sb.AppendLine($"   NOTE: {item.Notes}");
                sb.Append(ESC_BOLD_OFF);
            }
            sb.AppendLine();
        }

        // Order notes
        if (!string.IsNullOrWhiteSpace(order.Notes))
        {
            sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));
            sb.Append(ESC_BOLD_ON);
            sb.AppendLine("ORDER NOTES:");
            sb.AppendLine(order.Notes);
            sb.Append(ESC_BOLD_OFF);
            sb.AppendLine();
        }

        // Footer
        sb.AppendLine(new string('=', paperWidth == 80 ? 48 : 32));
        sb.Append(ESC_ALIGN_CENTER);
        sb.Append(ESC_BOLD_ON);
        sb.AppendLine("PREPARE IMMEDIATELY");
        sb.Append(ESC_BOLD_OFF);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        sb.Append(ESC_FEED_AND_CUT);

        return sb.ToString();
    }

    private string FormatCashierReceipt(Order order, PrinterConfiguration config, int paperWidth)
    {
        var sb = new StringBuilder();

        // Initialize
        sb.Append(ESC_INIT);

        // Header - Bold and Double Size
        sb.Append(ESC_ALIGN_CENTER);
        sb.Append(ESC_DOUBLE_ON);
        sb.Append(ESC_BOLD_ON);
        sb.AppendLine($"{config.RestaurantName}");
        sb.Append(ESC_DOUBLE_OFF);
        sb.Append(ESC_BOLD_OFF);

        sb.Append(ESC_BOLD_ON);
        sb.AppendLine("RECEIPT");
        sb.Append(ESC_BOLD_OFF);
        sb.AppendLine();

        // Order info
        sb.Append(ESC_ALIGN_LEFT);
        sb.Append(ESC_BOLD_ON);
        sb.AppendLine($"Order #: {order.Id}");
        sb.AppendLine($"Table: {order.TableNumber}");
        sb.AppendLine($"Date: {order.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.Append(ESC_BOLD_OFF);
        if (!string.IsNullOrWhiteSpace(order.WaiterName))
        {
            sb.AppendLine($"Server: {order.WaiterName}");
        }
        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Items with prices
        foreach (var item in order.Items)
        {
            var itemLine = $"{item.Quantity}x {item.Name}";
            var price = $"${item.Price * item.Quantity:F2}";
            var spacing = paperWidth == 80 ? 48 : 32;
            var dots = spacing - itemLine.Length - price.Length;

            sb.Append(ESC_BOLD_ON);
            sb.Append(itemLine);
            sb.Append(new string('.', Math.Max(1, dots)));
            sb.AppendLine(price);
            sb.Append(ESC_BOLD_OFF);

            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                sb.AppendLine($"  * {item.Notes}");
            }
        }

        sb.AppendLine(new string('-', paperWidth == 80 ? 48 : 32));

        // Total - Bold and larger
        sb.Append(ESC_DOUBLE_ON);
        sb.Append(ESC_BOLD_ON);
        sb.AppendLine($"TOTAL: ${order.Total:F2}");
        sb.Append(ESC_BOLD_OFF);
        sb.Append(ESC_DOUBLE_OFF);
        sb.AppendLine();

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
        var bytes = Encoding.UTF8.GetBytes(content);

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
