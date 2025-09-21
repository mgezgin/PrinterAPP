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

    public Task<bool> PrintTestReceiptAsync(string printerName, PrinterConfiguration config)
    {
        try
        {
            // Clean printer name (remove " (Default)" if present)
            var cleanPrinterName = printerName.Replace(" (Default)", "").Trim();
            
            // Generate receipt text
            var receiptText = GenerateReceiptText(config, cleanPrinterName);
            
            // Method 1: Try direct printing
            if (SendTextToPrinter(cleanPrinterName, receiptText))
            {
                return Task.FromResult(true);
            }
            
            // Method 2: Fallback to file + shell print
            return PrintViaShell(receiptText, cleanPrinterName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print error: {ex.Message}");
            return Task.FromResult(false);
        }
    }
    
    private bool SendTextToPrinter(string printerName, string text)
    {
        IntPtr hPrinter = IntPtr.Zero;
        var docInfo = new DOC_INFO_1
        {
            pDocName = "Kitchen Test Receipt",
            pDatatype = "RAW"
        };
        
        try
        {
            if (OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, ref docInfo))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(text);
                        int written;
                        bool success = WritePrinter(hPrinter, bytes, bytes.Length, out written);
                        EndPagePrinter(hPrinter);
                        EndDocPrinter(hPrinter);
                        return success;
                    }
                    EndDocPrinter(hPrinter);
                }
            }
            return false;
        }
        finally
        {
            if (hPrinter != IntPtr.Zero)
                ClosePrinter(hPrinter);
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
    
    private string GenerateReceiptText(PrinterConfiguration config, string printerName)
    {
        var sb = new StringBuilder();
        
        // For thermal printers, add ESC/POS commands
        if (IsThermalPrinter(printerName))
        {
            sb.Append("\x1B\x40"); // Initialize
            sb.Append("\x1B\x61\x01"); // Center align
        }
        
        sb.AppendLine("      *** TEST RECEIPT ***");
        sb.AppendLine($"       {config.RestaurantName}");
        sb.AppendLine($"       {config.KitchenLocation}");
        sb.AppendLine("================================");
        sb.AppendLine($"Date: {DateTime.Now:dd/MM/yyyy}");
        sb.AppendLine($"Time: {DateTime.Now:HH:mm:ss}");
        sb.AppendLine($"Printer: {printerName}");
        sb.AppendLine("================================");
        sb.AppendLine("Configuration Test:");
        sb.AppendLine($"  API URL: {config.ApiBaseUrl}");
        sb.AppendLine($"  Paper: {config.PaperWidth}mm");
        sb.AppendLine($"  Auto Print: {config.AutoPrint}");
        sb.AppendLine("================================");
        sb.AppendLine("Sample Order:");
        sb.AppendLine("  2x Burger Deluxe");
        sb.AppendLine("     [Large]");
        sb.AppendLine("     Note: No onions");
        sb.AppendLine("");
        sb.AppendLine("  1x Caesar Salad");
        sb.AppendLine("     Extra dressing");
        sb.AppendLine("================================");
        sb.AppendLine("      *** END OF TEST ***");
        
        if (IsThermalPrinter(printerName))
        {
            sb.AppendLine("\x1B\x64\x05"); // Feed 5 lines
            sb.AppendLine("\x1B\x69"); // Cut
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
                return JsonSerializer.Deserialize<PrinterConfiguration>(json) ?? new PrinterConfiguration();
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