using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace PrinterAPP.Services;

public class RequestLogService
{
    private readonly ILogger<RequestLogService> _logger;
    private readonly ObservableCollection<LogEntry> _logs;
    private readonly object _lockObject = new();

    public ObservableCollection<LogEntry> Logs => _logs;

    public event EventHandler<LogEntry>? LogAdded;

    public RequestLogService(ILogger<RequestLogService> logger)
    {
        _logger = logger;
        _logs = new ObservableCollection<LogEntry>();
    }

    public void LogSSEConnection(string endpoint, string status)
    {
        AddLog(LogType.SSE, $"SSE {endpoint}", status, string.Empty);
    }

    public void LogSSEEvent(string eventType, string data)
    {
        AddLog(LogType.SSE, $"Event: {eventType}", "Received", data);
    }

    public void LogOrderReceived(int orderId, int tableNumber, decimal total)
    {
        AddLog(LogType.Order, $"Order #{orderId}", $"Table {tableNumber}", $"${total:F2}");
    }

    public void LogPrintRequest(string printerType, int orderId, string printerName)
    {
        AddLog(LogType.PrintRequest, $"{printerType} Print", $"Order #{orderId}", $"Printer: {printerName}");
    }

    public void LogPrintResponse(string printerType, int orderId, bool success, string? error = null)
    {
        var status = success ? "✓ Success" : "✗ Failed";
        var details = error ?? "Printed successfully";
        AddLog(success ? LogType.PrintSuccess : LogType.PrintError,
               $"{printerType} Result",
               $"Order #{orderId} - {status}",
               details);
    }

    public void LogError(string operation, string message, string? details = null)
    {
        AddLog(LogType.Error, operation, $"Error: {message}", details ?? string.Empty);
    }

    private void AddLog(LogType type, string operation, string message, string details)
    {
        try
        {
            lock (_lockObject)
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Type = type,
                    Operation = operation,
                    Message = message,
                    Details = details
                };

                // Insert at the beginning (most recent first)
                _logs.Insert(0, entry);

                // Log to console/debug
                _logger.LogInformation("[{Type}] {Operation}: {Message}", type, operation, message);

                // Notify subscribers
                LogAdded?.Invoke(this, entry);

                // Keep only last 200 logs to prevent memory issues
                if (_logs.Count > 200)
                {
                    _logs.RemoveAt(_logs.Count - 1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding log entry");
        }
    }

    public void ClearLogs()
    {
        lock (_lockObject)
        {
            _logs.Clear();
            _logger.LogInformation("Request logs cleared");
        }
    }
}

public enum LogType
{
    SSE,
    Order,
    PrintRequest,
    PrintSuccess,
    PrintError,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogType Type { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    public string TypeIcon => Type switch
    {
        LogType.SSE => "🌐",
        LogType.Order => "📋",
        LogType.PrintRequest => "🖨️",
        LogType.PrintSuccess => "✅",
        LogType.PrintError => "❌",
        LogType.Error => "⚠️",
        _ => "📝"
    };

    public Color TypeColor => Type switch
    {
        LogType.SSE => Colors.Blue,
        LogType.Order => Colors.Purple,
        LogType.PrintRequest => Colors.Orange,
        LogType.PrintSuccess => Colors.Green,
        LogType.PrintError => Colors.Red,
        LogType.Error => Colors.DarkRed,
        _ => Colors.Gray
    };

    public string DisplayText => $"[{TimestampText}] {TypeIcon} {Operation}: {Message}";
}
