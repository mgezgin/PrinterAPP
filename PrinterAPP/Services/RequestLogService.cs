using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

    public void LogSSEConnection(string endpoint, string status, string? url = null, Dictionary<string, string>? headers = null)
    {
        var source = endpoint.ToLower() == "kitchen" ? "Kitchen" : endpoint.ToLower() == "service" ? "Service" : "General";
        var entry = CreateLogEntry(LogType.SSE, $"SSE {endpoint}", status, string.Empty, source);
        entry.RequestUrl = url;
        entry.RequestHeaders = headers;
        AddLogEntry(entry);
    }

    public void LogSSEResponse(string endpoint, int statusCode, Dictionary<string, string>? responseHeaders = null)
    {
        var source = endpoint.ToLower() == "kitchen" ? "Kitchen" : endpoint.ToLower() == "service" ? "Service" : "General";
        var entry = CreateLogEntry(LogType.SSE, $"SSE Response {endpoint}", $"Status: {statusCode}", string.Empty, source);
        entry.ResponseStatusCode = statusCode;
        entry.ResponseHeaders = responseHeaders;
        AddLogEntry(entry);
    }

    public void LogSSEEvent(string eventType, string data, string? rawData = null, string? source = null)
    {
        var entry = CreateLogEntry(LogType.SSE, $"Event: {eventType}", "Received", data, source ?? "General");
        entry.ResponseBody = rawData ?? data;
        AddLogEntry(entry);
    }

    public void LogOrderReceived(int orderId, int? tableNumber, decimal total, string? orderJson = null, string? source = null)
    {
        var entry = CreateLogEntry(LogType.Order, $"Order #{orderId}", $"Table {tableNumber}", $"${total:F2}", source ?? "General");
        entry.ResponseBody = orderJson;
        AddLogEntry(entry);
    }

    public void LogPrintRequest(string printerType, int orderId, string printerName, string? printContent = null)
    {
        var source = printerType.ToLower() == "kitchen" ? "Kitchen" : "Service";
        var entry = CreateLogEntry(LogType.PrintRequest, $"{printerType} Print", $"Order #{orderId}", $"Printer: {printerName}", source);
        entry.RequestBody = printContent;
        AddLogEntry(entry);
    }

    public void LogPrintResponse(string printerType, int orderId, bool success, string? error = null, string? details = null)
    {
        var source = printerType.ToLower() == "kitchen" ? "Kitchen" : "Service";
        var status = success ? "✓ Success" : "✗ Failed";
        var message = error ?? "Printed successfully";
        var entry = CreateLogEntry(success ? LogType.PrintSuccess : LogType.PrintError,
               $"{printerType} Result",
               $"Order #{orderId} - {status}",
               message,
               source);
        entry.ResponseBody = details;
        AddLogEntry(entry);
    }

    public void LogError(string operation, string message, string? details = null)
    {
        var entry = CreateLogEntry(LogType.Error, operation, $"Error: {message}", details ?? string.Empty, "General");
        AddLogEntry(entry);
    }

    private LogEntry CreateLogEntry(LogType type, string operation, string message, string details, string source = "General")
    {
        return new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = type,
            Operation = operation,
            Message = message,
            Details = details,
            Source = source
        };
    }

    private void AddLogEntry(LogEntry entry)
    {
        try
        {
            lock (_lockObject)
            {
                // Insert at the beginning (most recent first)
                _logs.Insert(0, entry);

                // Log to console/debug
                _logger.LogInformation("[{Type}] {Operation}: {Message}", entry.Type, entry.Operation, entry.Message);

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

public class LogEntry : INotifyPropertyChanged
{
    private bool _isRequestExpanded = false;
    private bool _isResponseExpanded = false;
    private bool _isRequestHeadersExpanded = false;
    private bool _isResponseHeadersExpanded = false;
    private bool _isRequestBodyExpanded = false;
    private bool _isResponseBodyExpanded = false;

    public DateTime Timestamp { get; set; }
    public LogType Type { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Source { get; set; } = "General"; // Kitchen, Service, or General

    // HTTP Request Details
    public string? RequestUrl { get; set; }
    public string? RequestMethod { get; set; }
    public Dictionary<string, string>? RequestHeaders { get; set; }
    public string? RequestBody { get; set; }

    // HTTP Response Details
    public int? ResponseStatusCode { get; set; }
    public Dictionary<string, string>? ResponseHeaders { get; set; }
    public string? ResponseBody { get; set; }

    // Expansion State Properties
    public bool IsRequestExpanded
    {
        get => _isRequestExpanded;
        set
        {
            if (_isRequestExpanded != value)
            {
                _isRequestExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequestExpandIcon));
            }
        }
    }

    public bool IsResponseExpanded
    {
        get => _isResponseExpanded;
        set
        {
            if (_isResponseExpanded != value)
            {
                _isResponseExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ResponseExpandIcon));
            }
        }
    }

    public bool IsRequestHeadersExpanded
    {
        get => _isRequestHeadersExpanded;
        set
        {
            if (_isRequestHeadersExpanded != value)
            {
                _isRequestHeadersExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequestHeadersExpandIcon));
            }
        }
    }

    public bool IsResponseHeadersExpanded
    {
        get => _isResponseHeadersExpanded;
        set
        {
            if (_isResponseHeadersExpanded != value)
            {
                _isResponseHeadersExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ResponseHeadersExpandIcon));
            }
        }
    }

    public bool IsRequestBodyExpanded
    {
        get => _isRequestBodyExpanded;
        set
        {
            if (_isRequestBodyExpanded != value)
            {
                _isRequestBodyExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequestBodyExpandIcon));
            }
        }
    }

    public bool IsResponseBodyExpanded
    {
        get => _isResponseBodyExpanded;
        set
        {
            if (_isResponseBodyExpanded != value)
            {
                _isResponseBodyExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ResponseBodyExpandIcon));
            }
        }
    }

    // Expand Icons
    public string RequestExpandIcon => IsRequestExpanded ? "▼" : "▶";
    public string ResponseExpandIcon => IsResponseExpanded ? "▼" : "▶";
    public string RequestHeadersExpandIcon => IsRequestHeadersExpanded ? "▼" : "▶";
    public string ResponseHeadersExpandIcon => IsResponseHeadersExpanded ? "▼" : "▶";
    public string RequestBodyExpandIcon => IsRequestBodyExpanded ? "▼" : "▶";
    public string ResponseBodyExpandIcon => IsResponseBodyExpanded ? "▼" : "▶";

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

    public bool HasRequestDetails => !string.IsNullOrEmpty(RequestUrl) || RequestHeaders?.Any() == true || !string.IsNullOrEmpty(RequestBody);
    public bool HasResponseDetails => ResponseStatusCode.HasValue || ResponseHeaders?.Any() == true || !string.IsNullOrEmpty(ResponseBody);

    public string RequestHeadersText => RequestHeaders != null
        ? string.Join("\n", RequestHeaders.Select(h => $"{h.Key}: {h.Value}"))
        : string.Empty;

    public string ResponseHeadersText => ResponseHeaders != null
        ? string.Join("\n", ResponseHeaders.Select(h => $"{h.Key}: {h.Value}"))
        : string.Empty;

    // Formatted JSON Properties
    public string RequestHeadersJson => FormatAsJson(RequestHeaders);
    public string ResponseHeadersJson => FormatAsJson(ResponseHeaders);
    public string RequestBodyJson => FormatJson(RequestBody);
    public string ResponseBodyJson => FormatJson(ResponseBody);

    private static string FormatAsJson(Dictionary<string, string>? dictionary)
    {
        if (dictionary == null || !dictionary.Any())
            return string.Empty;

        try
        {
            var json = JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { WriteIndented = true });
            return json;
        }
        catch
        {
            return string.Join("\n", dictionary.Select(h => $"{h.Key}: {h.Value}"));
        }
    }

    private static string FormatJson(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
            return string.Empty;

        try
        {
            // Try to parse and format as JSON
            using var document = JsonDocument.Parse(jsonString);
            return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // If not valid JSON, return as-is
            return jsonString;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
