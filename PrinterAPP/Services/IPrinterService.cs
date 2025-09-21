using PrinterAPP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrinterAPP.Services;
public interface IPrinterService
{
    Task<List<string>> GetAvailablePrintersAsync();
    Task<bool> PrintTestReceiptAsync(string printerName, PrinterConfiguration config);
    Task<bool> TestApiConnectionAsync(string apiUrl);
    Task<PrinterConfiguration> LoadConfigurationAsync();
    Task SaveConfigurationAsync(PrinterConfiguration config);
}
