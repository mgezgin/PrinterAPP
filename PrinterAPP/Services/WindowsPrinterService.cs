// Platforms/Windows/SimplePrinterService.cs
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text;
using PrinterAPP.Models;

namespace PrinterAPP.Services;

/// <summary>
/// Simple printer service that works without any special NuGet packages
/// Uses only Windows APIs via P/Invoke
/// </summary>
public class SimplePrinterService : IPrinterService
{
    private readonly string _configPath;

    public SimplePrinterService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "KitchenPrinter");
        _configPath = Path.Combine(configDir, "config.json");
        
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    #region Windows API Declarations
    
    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetDefaultPrinter(StringBuilder buffer, ref int bufferSize);
    
    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool EnumPrinters(PrinterEnumFlags flags, string? name, uint level, IntPtr pPrinterEnum, uint cbBuf, ref uint pcbNeeded, ref uint pcReturned);
    
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool OpenPrinter(string printerName, out IntPtr phPrinter, IntPtr pDefault);
    
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);
    
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 pDocInfo);
    
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);
    
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);
    
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);
    
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBuf, int cbBuf, out int pcWritten);
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DOC_INFO_1
    {
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pDocName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pDatatype;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PRINTER_INFO_2
    {
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pServerName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pPrinterName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pShareName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pPortName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pDriverName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pComment;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pLocation;
        public IntPtr pDevMode;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pSepFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pPrintProcessor;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pDatatype;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }
    
    [Flags]
    private enum PrinterEnumFlags
    {
        PRINTER_ENUM_LOCAL = 0x00000002,
        PRINTER_ENUM_CONNECTIONS = 0x00000004,
        PRINTER_ENUM_NAME = 0x00000008,
        PRINTER_ENUM_NETWORK = 0x00000040,
    }
    
    #endregion

    public Task<List<string>> GetAvailablePrintersAsync()
    {
        var printers = new List<string>();
        
        try
        {
            // Method 1: Get default printer
            var defaultPrinter = GetDefaultPrinterName();
            if (!string.IsNullOrEmpty(defaultPrinter))
            {
                printers.Add($"{defaultPrinter} (Default)");
            }
            
            // Method 2: Enumerate all local printers
            var localPrinters = EnumerateLocalPrinters();
            foreach (var printer in localPrinters)
            {
                if (!printers.Any(p => p.Contains(printer)))
                {
                    printers.Add(printer);
                }
            }
            
            // Method 3: Try WMI as fallback (requires System.Management)
            try
            {
                var wmiPrinters = GetPrintersViaWMI();
                foreach (var printer in wmiPrinters)
                {
                    if (!printers.Any(p => p.Contains(printer)))
                    {
                        printers.Add(printer);
                    }
                }
            }
            catch
            {
                // WMI might not be available, that's okay
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting printers: {ex.Message}");
        }
        
        if (printers.Count == 0)
        {
            printers.Add("Default Printer");
        }
        
        return Task.FromResult(printers);
    }
    
    private string GetDefaultPrinterName()
    {
        const int ERROR_INSUFFICIENT_BUFFER = 122;
        int size = 0;
        
        // First call to get size
        GetDefaultPrinter(null!, ref size);
        
        if (Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
        {
            var buffer = new StringBuilder(size);
            if (GetDefaultPrinter(buffer, ref size))
            {
                return buffer.ToString();
            }
        }
        
        return string.Empty;
    }
    
    private List<string> EnumerateLocalPrinters()
    {
        var printers = new List<string>();
        uint cbNeeded = 0;
        uint cReturned = 0;
        
        // First call to get size
        EnumPrinters(PrinterEnumFlags.PRINTER_ENUM_LOCAL | PrinterEnumFlags.PRINTER_ENUM_CONNECTIONS, 
                    null, 2, IntPtr.Zero, 0, ref cbNeeded, ref cReturned);
        
        if (cbNeeded > 0)
        {
            IntPtr pAddr = Marshal.AllocHGlobal((int)cbNeeded);
            try
            {
                if (EnumPrinters(PrinterEnumFlags.PRINTER_ENUM_LOCAL | PrinterEnumFlags.PRINTER_ENUM_CONNECTIONS,
                                null, 2, pAddr, cbNeeded, ref cbNeeded, ref cReturned))
                {
                    IntPtr offset = pAddr;
                    int size = Marshal.SizeOf(typeof(PRINTER_INFO_2));
                    
                    for (int i = 0; i < cReturned; i++)
                    {
                        var printerInfo = Marshal.PtrToStructure<PRINTER_INFO_2>(offset);
                        if (!string.IsNullOrEmpty(printerInfo.pPrinterName))
                        {
                            printers.Add(printerInfo.pPrinterName);
                        }
                        offset = IntPtr.Add(offset, size);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pAddr);
            }
        }
        
        return printers;
    }
    
    private List<string> GetPrintersViaWMI()
    {
        var printers = new List<string>();
        
        // Only try if System.Management is available
#if WINDOWS
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Printer");
            foreach (System.Management.ManagementObject printer in searcher.Get())
            {
                var name = printer["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    printers.Add(name);
                }
            }
        }
        catch
        {
            // System.Management might not be available
        }
#endif
        
        return printers;
    }

    public async Task<bool> PrintTestReceiptAsync(string printerName, PrinterConfiguration config)
    {
        try
        {
            // Clean printer name (remove " (Default)" if present)
            var cleanPrinterName = printerName.Replace(" (Default)", "").Trim();

            var isThermal = IsThermalPrinter(cleanPrinterName);
            System.Diagnostics.Debug.WriteLine($"======================================");
            System.Diagnostics.Debug.WriteLine($"Printer: {cleanPrinterName}");
            System.Diagnostics.Debug.WriteLine($"Is Thermal: {isThermal}");
            System.Diagnostics.Debug.WriteLine($"======================================");

            // Generate receipt text with ESC/POS commands
            var receiptText = GenerateReceiptText(config, cleanPrinterName);
            System.Diagnostics.Debug.WriteLine($"Receipt text length: {receiptText.Length} characters");

            // For thermal printers (EPSON), use direct RAW printing
            if (isThermal)
            {
                System.Diagnostics.Debug.WriteLine("THERMAL PRINTER: Using direct RAW mode");

                // Try multiple times with different approaches
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    System.Diagnostics.Debug.WriteLine($"--- Attempt {attempt} ---");

                    if (SendTextToPrinter(cleanPrinterName, receiptText))
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Direct RAW printing succeeded on attempt {attempt}");
                        return true;
                    }

                    System.Diagnostics.Debug.WriteLine($"✗ Attempt {attempt} failed");
                    await Task.Delay(500); // Small delay between attempts
                }

                // If all RAW attempts failed, try writing directly to printer port
                System.Diagnostics.Debug.WriteLine("All RAW attempts failed, trying port-based printing...");
                if (await TryPrintToPort(cleanPrinterName, receiptText))
                {
                    System.Diagnostics.Debug.WriteLine("✓ Port-based printing succeeded");
                    return true;
                }
            }
            else
            {
                // Non-thermal printer - use HTML
                System.Diagnostics.Debug.WriteLine("NON-THERMAL PRINTER: Using HTML bold formatting");
                return await PrintViaHtmlBold(receiptText, cleanPrinterName, config);
            }

            System.Diagnostics.Debug.WriteLine("✗ All printing methods failed");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
    
    private bool SendTextToPrinter(string printerName, string text)
    {
        IntPtr hPrinter = IntPtr.Zero;
        var docInfo = new DOC_INFO_1
        {
            pDocName = "Restaurant Order",
            pDatatype = "RAW"
        };

        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to open printer: {printerName}");

            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"OpenPrinter failed with error: {error}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine("Printer opened successfully");

            if (!StartDocPrinter(hPrinter, 1, ref docInfo))
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"StartDocPrinter failed with error: {error}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine("Document started");

            if (!StartPagePrinter(hPrinter))
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"StartPagePrinter failed with error: {error}");
                EndDocPrinter(hPrinter);
                return false;
            }

            System.Diagnostics.Debug.WriteLine("Page started");

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            System.Diagnostics.Debug.WriteLine($"Sending {bytes.Length} bytes to printer");

            int written;
            bool success = WritePrinter(hPrinter, bytes, bytes.Length, out written);

            System.Diagnostics.Debug.WriteLine($"WritePrinter result: {success}, Bytes written: {written}");

            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);

            return success && written == bytes.Length;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendTextToPrinter exception: {ex.Message}");
            return false;
        }
        finally
        {
            if (hPrinter != IntPtr.Zero)
                ClosePrinter(hPrinter);
        }
    }
    
    private async Task<bool> PrintViaHtmlBold(string text, string printerName, PrinterConfiguration config)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Creating HTML with bold formatting");

            // Create HTML with bold text and darker font
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><meta charset='utf-8'>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: 'Courier New', monospace; font-size: 14pt; font-weight: bold; }");
            html.AppendLine(".header { font-size: 18pt; font-weight: 900; text-align: center; }");
            html.AppendLine(".bold { font-weight: 900; }");
            html.AppendLine(".section { font-weight: 900; margin-top: 10px; }");
            html.AppendLine("</style></head><body>");

            // Parse the text and wrap headers in bold tags
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var cleanLine = System.Text.RegularExpressions.Regex.Replace(line, @"[\x00-\x1F\x7F-\x9F]", "");

                if (cleanLine.Contains("***") || cleanLine.Contains("===") ||
                    cleanLine.Contains("Date:") || cleanLine.Contains("Time:") ||
                    cleanLine.Contains("Configuration Test:") || cleanLine.Contains("Sample Order:") ||
                    cleanLine.Contains("Printer:") || cleanLine.StartsWith("  "))
                {
                    html.AppendLine($"<div class='bold'>{System.Net.WebUtility.HtmlEncode(cleanLine)}</div>");
                }
                else if (cleanLine.Contains(config.RestaurantName) || cleanLine.Contains(config.KitchenLocation))
                {
                    html.AppendLine($"<div class='header'>{System.Net.WebUtility.HtmlEncode(cleanLine)}</div>");
                }
                else
                {
                    html.AppendLine($"<div>{System.Net.WebUtility.HtmlEncode(cleanLine)}</div>");
                }
            }

            html.AppendLine("</body></html>");

            // Save to temp HTML file
            var tempFile = Path.Combine(Path.GetTempPath(), $"receipt_{Guid.NewGuid()}.html");
            await File.WriteAllTextAsync(tempFile, html.ToString());

            System.Diagnostics.Debug.WriteLine($"HTML file created: {tempFile}");

            // Print HTML using default browser's print functionality
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start /min \"\" \"{tempFile}\" && timeout /t 3 && powershell -Command \"(New-Object -ComObject WScript.Shell).SendKeys('^p')\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };

            var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                // Cleanup after delay
                await Task.Delay(5000);
                try { File.Delete(tempFile); } catch { }

                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PrintViaHtmlBold exception: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PrintViaShell(string text, string printerName)
    {
        try
        {
            // Save to temp file
            var tempFile = Path.Combine(Path.GetTempPath(), $"receipt_{Guid.NewGuid()}.txt");
            await File.WriteAllTextAsync(tempFile, text);
            
            // Use PowerShell to print (more reliable than notepad)
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Get-Content '{tempFile}' | Out-Printer '{printerName}'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            
            var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                // Cleanup
                await Task.Delay(2000);
                try { File.Delete(tempFile); } catch { }
                
                return process.ExitCode == 0;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> TryPrintToPort(string printerName, string text)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Attempting port-based printing...");

            // Get the printer port using WMI
            string? portName = null;

#if WINDOWS
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT * FROM Win32_Printer WHERE Name = '{printerName.Replace("'", "''")}'");

                foreach (System.Management.ManagementObject printer in searcher.Get())
                {
                    portName = printer["PortName"]?.ToString();
                    System.Diagnostics.Debug.WriteLine($"Found port: {portName}");
                    break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI query failed: {ex.Message}");
            }
#endif

            if (string.IsNullOrEmpty(portName))
            {
                System.Diagnostics.Debug.WriteLine("Could not determine printer port");
                return false;
            }

            // For network printers, port might be an IP address
            // For local printers, it might be USB001, LPT1, etc.
            System.Diagnostics.Debug.WriteLine($"Printer port: {portName}");

            // Try to write directly to the port
            if (portName.StartsWith("USB") || portName.StartsWith("LPT"))
            {
                System.Diagnostics.Debug.WriteLine($"Writing to local port: {portName}");

                // For local ports, use the printer name with OpenPrinter
                // This is actually what we already tried, so skip
                return false;
            }
            else if (System.Net.IPAddress.TryParse(portName.Split(':')[0], out _))
            {
                // Network printer - try direct TCP/IP
                System.Diagnostics.Debug.WriteLine($"Network printer detected: {portName}");
                return await PrintToNetworkPrinter(portName, text);
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TryPrintToPort exception: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PrintToNetworkPrinter(string ipAddress, string text)
    {
        try
        {
            var parts = ipAddress.Split(':');
            var ip = parts[0];
            var port = parts.Length > 1 ? int.Parse(parts[1]) : 9100; // Default ESC/POS port

            System.Diagnostics.Debug.WriteLine($"Connecting to {ip}:{port}");

            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(ip, port);

            using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(text);

            System.Diagnostics.Debug.WriteLine($"Sending {bytes.Length} bytes to network printer");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();

            System.Diagnostics.Debug.WriteLine("Data sent successfully to network printer");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PrintToNetworkPrinter exception: {ex.Message}");
            return false;
        }
    }

    private string GenerateReceiptText(PrinterConfiguration config, string printerName)
    {
        var sb = new StringBuilder();
        var isThermal = IsThermalPrinter(printerName);

        // ESC/POS commands for MAXIMUM darkness
        const string ESC_INIT = "\x1B\x40";
        const string ESC_BOLD_ON = "\x1B\x45\x01";
        const string ESC_BOLD_OFF = "\x1B\x45\x00";
        const string ESC_EMPHASIZED_ON = "\x1B\x47\x01";
        const string ESC_EMPHASIZED_OFF = "\x1B\x47\x00";
        const string ESC_DOUBLE_ON = "\x1D\x21\x11";
        const string ESC_DOUBLE_OFF = "\x1D\x21\x00";
        const string ESC_ALIGN_CENTER = "\x1B\x61\x01";
        const string ESC_ALIGN_LEFT = "\x1B\x61\x00";
        const string EXTRA_DARK_ON = ESC_BOLD_ON + ESC_EMPHASIZED_ON;
        const string EXTRA_DARK_OFF = ESC_BOLD_OFF + ESC_EMPHASIZED_OFF;

        // For thermal printers, add ESC/POS commands for MAXIMUM darkness
        if (isThermal)
        {
            sb.Append(ESC_INIT); // Initialize
            sb.Append(ESC_ALIGN_CENTER); // Center align
            sb.Append(ESC_DOUBLE_ON); // Double size
            sb.Append(EXTRA_DARK_ON); // EXTRA DARK for header
        }

        sb.AppendLine("*** TEST RECEIPT ***");

        if (isThermal)
        {
            sb.Append(ESC_DOUBLE_OFF); // Normal size
        }

        sb.AppendLine($"{config.RestaurantName}");
        sb.AppendLine($"{config.KitchenLocation}");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_OFF); // Turn off extra dark
            sb.Append(ESC_ALIGN_LEFT); // Left align
        }

        sb.AppendLine("================================");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_ON); // EXTRA DARK for headers
        }

        sb.AppendLine($"Date: {DateTime.Now:dd/MM/yyyy}");
        sb.AppendLine($"Time: {DateTime.Now:HH:mm:ss}");
        sb.AppendLine($"Printer: {printerName}");
        sb.AppendLine("================================");
        sb.AppendLine("Configuration Test:");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_OFF); // Turn off for details
        }

        sb.AppendLine($"  API URL: {config.ApiBaseUrl}");
        sb.AppendLine($"  Paper: {config.KitchenPaperWidth}mm");
        sb.AppendLine($"  Auto Print: {config.KitchenAutoPrint}");
        sb.AppendLine("================================");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_ON); // EXTRA DARK for sample order header
        }

        sb.AppendLine("Sample Order:");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_OFF); // Turn off for details
        }

        sb.AppendLine("  2x Burger Deluxe");
        sb.AppendLine("     [Large]");
        sb.AppendLine("     Note: No onions");
        sb.AppendLine("");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_ON); // EXTRA DARK for item
        }

        sb.AppendLine("  1x Caesar Salad");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_OFF); // Turn off
        }

        sb.AppendLine("     Extra dressing");
        sb.AppendLine("================================");

        if (isThermal)
        {
            sb.Append(ESC_ALIGN_CENTER); // Center align
            sb.Append(EXTRA_DARK_ON); // EXTRA DARK for footer
        }

        sb.AppendLine("*** END OF TEST ***");

        if (isThermal)
        {
            sb.Append(EXTRA_DARK_OFF); // Turn off
            sb.Append("\x1B\x64\x05"); // Feed 5 lines
            sb.Append("\x1D\x56\x00"); // Full cut
        }
        else
        {
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("");
        }

        return sb.ToString();
    }
    
    private bool IsThermalPrinter(string printerName)
    {
        var thermalKeywords = new[] { "EPSON", "TM-", "TSP", "POS", "Receipt", "Thermal", "Star" };
        return thermalKeywords.Any(keyword => printerName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> TestApiConnectionAsync(string apiUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{apiUrl}/api/events/kitchen");
            return response.IsSuccessStatusCode || 
                   response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PrinterConfiguration> LoadConfigurationAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<PrinterConfiguration>(json) ?? new PrinterConfiguration();

                // Migration: Update old localhost:5221 URLs to https://localhost:44386
                if (config.ApiBaseUrl == "http://localhost:5221" ||
                    config.ApiBaseUrl == "https://localhost:5221")
                {
                    config.ApiBaseUrl = "https://localhost:44386";
                    // Save the updated configuration
                    await SaveConfigurationAsync(config);
                    System.Diagnostics.Debug.WriteLine("Migrated API URL from localhost:5221 to localhost:44386");
                }

                return config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
        }

        return new PrinterConfiguration();
    }

    public async Task SaveConfigurationAsync(PrinterConfiguration config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            throw;
        }
    }
}