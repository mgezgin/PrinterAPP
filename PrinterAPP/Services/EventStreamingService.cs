using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrinterAPP.Models;

namespace PrinterAPP.Services;

public class EventStreamingService : IEventStreamingService
{
    private readonly IPrinterService _printerService;
    private readonly RequestLogService _requestLogService;
    private readonly ILogger<EventStreamingService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _kitchenListeningTask;
    private bool _isListening;
    
    // Track processed order IDs to prevent duplicate display/print (with timestamp for cleanup)
    private readonly Dictionary<string, DateTime> _processedOrders = new();
    private readonly object _processedOrdersLock = new();
    private const int MaxProcessedOrdersAge = 3600; // 1 hour in seconds

    public event EventHandler<OrderEvent>? OrderReceived;
    public event EventHandler<string>? ConnectionStatusChanged;

    public bool IsListening => _isListening;

    public EventStreamingService(
        IPrinterService printerService,
        RequestLogService requestLogService,
        ILogger<EventStreamingService> logger)
    {
        _printerService = printerService;
        _requestLogService = requestLogService;
        _logger = logger;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            _logger.LogWarning("EventStreamingService is already listening");
            return;
        }

        var config = await _printerService.LoadConfigurationAsync();

        // Debug logging
        _logger.LogInformation("SERVICE: Loaded API Base URL from config: {ApiUrl}", config.ApiBaseUrl);
        System.Diagnostics.Debug.WriteLine($"DEBUG SERVICE: API Base URL = {config.ApiBaseUrl}");

        if (string.IsNullOrWhiteSpace(config.ApiBaseUrl))
        {
            OnConnectionStatusChanged("Error: API Base URL not configured");
            return;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isListening = true;

        // Start listening to service endpoint (both printers will receive from this endpoint)
        _kitchenListeningTask = ListenToStreamAsync(config.ApiBaseUrl, "service", _cancellationTokenSource.Token);

        OnConnectionStatusChanged("Connected to Service SSE stream");
        _logger.LogInformation("Started listening to Service SSE stream");
    }

    public async Task StopListeningAsync()
    {
        if (!_isListening)
        {
            return;
        }

        _logger.LogInformation("Stopping SSE listener");
        _isListening = false;

        _cancellationTokenSource?.Cancel();

        try
        {
            if (_kitchenListeningTask != null)
            {
                await _kitchenListeningTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping SSE listener");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _kitchenListeningTask = null;

            OnConnectionStatusChanged("Disconnected");
        }
    }

    private async Task ListenToStreamAsync(string apiBaseUrl, string endpoint, CancellationToken cancellationToken)
    {
        var url = $"{apiBaseUrl.TrimEnd('/')}/api/events/{endpoint}";
        var retryDelay = TimeSpan.FromSeconds(5);
        const int maxRetryDelay = 60;

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpClient? httpClient = null;
            DateTime lastMessageReceived = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("Connecting to SSE stream: {Url}", url);
                OnConnectionStatusChanged($"Connecting to {endpoint}...");

                // Create new HttpClient for each connection attempt
                httpClient = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
                request.Headers.Connection.Add("keep-alive");
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

                // Capture request details
                var requestHeaders = new Dictionary<string, string>();
                foreach (var header in request.Headers)
                {
                    requestHeaders[header.Key] = string.Join(", ", header.Value);
                }

                // Log SSE connection with full request details
                _requestLogService.LogSSEConnection(endpoint, "Connecting...", url, requestHeaders);

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Capture response details
                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                // Log SSE response with full details
                _requestLogService.LogSSEResponse(endpoint, (int)response.StatusCode, responseHeaders);

                OnConnectionStatusChanged($"Connected to {endpoint} stream");
                _logger.LogInformation("Connected to SSE stream: {Endpoint}", endpoint);

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 256, leaveOpen: true);
                
                string? eventType = null;
                var dataBuilder = new StringBuilder();

                // Reset retry delay and last message time on successful connection
                retryDelay = TimeSpan.FromSeconds(5);
                lastMessageReceived = DateTime.UtcNow;

                // Start background task to check for connection timeout
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _ = Task.Run(async () => 
                {
                    while (!timeoutCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(5000, timeoutCts.Token).ConfigureAwait(false);
                        var timeSinceLastMessage = DateTime.UtcNow - lastMessageReceived;
                        if (timeSinceLastMessage.TotalSeconds > 45)
                        {
                            _logger.LogWarning("Connection timeout - no messages for {Seconds}s, cancelling...", timeSinceLastMessage.TotalSeconds);
                            timeoutCts.Cancel();
                        }
                    }
                }, timeoutCts.Token);

                try
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync(timeoutCts.Token)) != null)
                    {
                        // Update last message time for any data received
                        lastMessageReceived = DateTime.UtcNow;

                        if (line.StartsWith("event:"))
                        {
                            eventType = line.Substring(6).Trim();

                            // Log heartbeat events but don't process them further
                            if (eventType == "heartbeat")
                            {
                                _logger.LogDebug("Heartbeat received from {Endpoint}", endpoint);
                            }
                        }
                        else if (line.StartsWith("data:"))
                        {
                            // Only collect data if it's not a heartbeat
                            if (eventType != "heartbeat")
                            {
                                dataBuilder.AppendLine(line.Substring(5).Trim());
                            }
                        }
                        else if (line.StartsWith(":"))
                        {
                            // SSE comment line - also a form of heartbeat
                            _logger.LogDebug("Comment/heartbeat received from {Endpoint}", endpoint);
                        }
                        else if (string.IsNullOrEmpty(line))
                        {
                            // Empty line indicates end of message - process event
                            if (dataBuilder.Length > 0 && eventType != "heartbeat")
                            {
                                var data = dataBuilder.ToString().Trim();
                                _logger.LogInformation("Processing SSE event: {EventType}", eventType);
                                await ProcessEventAsync(eventType ?? "message", data, endpoint, cancellationToken);
                            }

                            // Reset for next message
                            dataBuilder.Clear();
                            eventType = null;
                        }
                    }
                }
                finally
                {
                    timeoutCts.Cancel();
                    timeoutCts.Dispose();
                }

                _logger.LogWarning("SSE stream ended for {Endpoint}", endpoint);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("SSE listener cancelled for {Endpoint}", endpoint);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SSE stream for {Endpoint}", endpoint);
                OnConnectionStatusChanged($"Error: {ex.Message}");

                if (!cancellationToken.IsCancellationRequested)
                {
                    // Exponential backoff with max delay
                    _logger.LogInformation("Retrying connection in {Delay} seconds...", retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay, cancellationToken);

                    retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, maxRetryDelay));
                }
            }
            finally
            {
                httpClient?.Dispose();
            }
        }
    }

    private async Task ProcessEventAsync(string eventType, string data, string sourceEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing SSE event - Type: {EventType}, Source: {Source}, Data: {Data}",
                eventType, sourceEndpoint, data);

            // Handle connection event
            if (eventType == "connected")
            {
                _logger.LogInformation("Received connection confirmation from {Source}", sourceEndpoint);
                _requestLogService.LogSSEEvent("connected", $"Connection confirmed from {sourceEndpoint}", data, "Service");
                return;
            }

            // Parse order event - handle both old format and new format
            if (eventType == "order-created" || eventType == "order-updated" || eventType == "order_created" ||
                eventType == "order_updated" || eventType == "order" || eventType == "message" ||
                eventType == "order-status-changed" || eventType == "order-ready" || eventType == "order-completed")
            {
                // Log raw event with truncated data for display
                var truncatedData = data.Length > 100 ? data.Substring(0, 100) + "..." : data;

                try
                {
                    // Try to parse as OrderEvent wrapper first (new format)
                    var orderEvent = JsonSerializer.Deserialize<OrderEvent>(data, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (orderEvent?.Order != null)
                    {
                        var order = orderEvent.Order;
                        
                        // FILTER: Only process orders with Confirmed status
                        if (!string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("Skipping order {OrderNumber} - status is {Status}, not Confirmed", 
                                order.OrderNumber, order.Status);
                            return;
                        }
                        
                        // DEDUPLICATION: Check if we've already processed this order
                        var orderKey = order.OrderNumber;
                        if (IsOrderAlreadyProcessed(orderKey))
                        {
                            _logger.LogInformation("Skipping duplicate order {OrderNumber}", order.OrderNumber);
                            return;
                        }
                        
                        // Mark order as processed
                        MarkOrderAsProcessed(orderKey);
                        
                        // Log order details for debugging
                        _logger.LogInformation("Received order {OrderNumber} with {ItemCount} items (Status: {Status})",
                            order.OrderNumber,
                            order.Items?.Count ?? 0,
                            order.Status);

                        if (order.Items != null && order.Items.Any())
                        {
                            foreach (var item in order.Items)
                            {
                                _logger.LogInformation("  - Item: {Quantity}x {ProductName}",
                                    item.Quantity, item.ProductName);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Order {OrderNumber} has no items!", order.OrderNumber);
                        }

                        // Log parsed order with full JSON data
                        _requestLogService.LogOrderReceived(
                            int.TryParse(order.OrderNumber.Split('/').Last(), out var orderNum) ? orderNum : 0,
                            order.TableNumber,
                            order.Total,
                            data,
                            "Service");

                        // Notify subscribers
                        OnOrderReceived(orderEvent);
                        return;
                    }
                }
                catch
                {
                    // If that fails, try to parse as Order directly (old format fallback)
                    var order = JsonSerializer.Deserialize<Order>(data, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (order != null)
                    {
                        // FILTER: Only process orders with Confirmed status
                        if (!string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("Skipping order {OrderNumber} - status is {Status}, not Confirmed", 
                                order.OrderNumber, order.Status);
                            return;
                        }
                        
                        // DEDUPLICATION: Check if we've already processed this order
                        var orderKey = order.OrderNumber;
                        if (IsOrderAlreadyProcessed(orderKey))
                        {
                            _logger.LogInformation("Skipping duplicate order {OrderNumber}", order.OrderNumber);
                            return;
                        }
                        
                        // Mark order as processed
                        MarkOrderAsProcessed(orderKey);
                        
                        // Log parsed order with full JSON data
                        _requestLogService.LogOrderReceived(
                            int.TryParse(order.OrderNumber.Split('/').Last(), out var orderNum) ? orderNum : 0,
                            order.TableNumber,
                            order.Total,
                            data,
                            "Service");

                        var orderEvent = new OrderEvent
                        {
                            EventType = eventType,
                            Order = order,
                            Timestamp = DateTime.UtcNow
                        };

                        // Notify subscribers
                        OnOrderReceived(orderEvent);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse order data: {Data}", data);
            _requestLogService.LogError("JSON Parse Error", $"Failed to parse event data: {ex.Message}", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SSE event");
            _requestLogService.LogError("Event Processing Error", ex.Message, ex.StackTrace);
        }
    }

    protected virtual void OnOrderReceived(OrderEvent orderEvent)
    {
        OrderReceived?.Invoke(this, orderEvent);
    }

    protected virtual void OnConnectionStatusChanged(string status)
    {
        ConnectionStatusChanged?.Invoke(this, status);
    }
    
    /// <summary>
    /// Check if an order has already been processed (to prevent duplicates)
    /// </summary>
    private bool IsOrderAlreadyProcessed(string orderNumber)
    {
        lock (_processedOrdersLock)
        {
            // Clean up old entries first
            CleanupOldProcessedOrders();
            
            return _processedOrders.ContainsKey(orderNumber);
        }
    }
    
    /// <summary>
    /// Mark an order as processed
    /// </summary>
    private void MarkOrderAsProcessed(string orderNumber)
    {
        lock (_processedOrdersLock)
        {
            _processedOrders[orderNumber] = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Clean up processed orders older than MaxProcessedOrdersAge
    /// </summary>
    private void CleanupOldProcessedOrders()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-MaxProcessedOrdersAge);
        var oldOrders = _processedOrders
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var orderNumber in oldOrders)
        {
            _processedOrders.Remove(orderNumber);
        }
        
        if (oldOrders.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old processed order records", oldOrders.Count);
        }
    }
}
