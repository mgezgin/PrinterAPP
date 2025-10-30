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
    private HttpClient? _httpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _kitchenListeningTask;
    private Task? _cashierListeningTask;
    private bool _isListening;

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
        if (string.IsNullOrWhiteSpace(config.ApiBaseUrl))
        {
            OnConnectionStatusChanged("Error: API Base URL not configured");
            return;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        _isListening = true;

        // Start listening to both kitchen and cashier endpoints
        _kitchenListeningTask = ListenToStreamAsync(config.ApiBaseUrl, "kitchen", _cancellationTokenSource.Token);
        _cashierListeningTask = ListenToStreamAsync(config.ApiBaseUrl, "service", _cancellationTokenSource.Token); // Using 'service' for cashier

        OnConnectionStatusChanged("Connected to SSE streams");
        _logger.LogInformation("Started listening to SSE streams");
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
            if (_cashierListeningTask != null)
            {
                await _cashierListeningTask;
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
            _httpClient?.Dispose();
            _httpClient = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _kitchenListeningTask = null;
            _cashierListeningTask = null;

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
            try
            {
                _logger.LogInformation("Connecting to SSE stream: {Url}", url);
                OnConnectionStatusChanged($"Connecting to {endpoint}...");

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                // Capture request details
                var requestHeaders = new Dictionary<string, string>();
                foreach (var header in request.Headers)
                {
                    requestHeaders[header.Key] = string.Join(", ", header.Value);
                }

                // Log SSE connection with full request details
                _requestLogService.LogSSEConnection(endpoint, "Connecting...", url, requestHeaders);

                using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
                using var reader = new StreamReader(stream, Encoding.UTF8);

                // Reset retry delay on successful connection
                retryDelay = TimeSpan.FromSeconds(5);

                string? line;
                string? eventType = null;
                var dataBuilder = new StringBuilder();

                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // SSE format: lines starting with "event:", "data:", or empty line (message delimiter)
                    if (line.StartsWith("event:"))
                    {
                        eventType = line.Substring(6).Trim();
                    }
                    else if (line.StartsWith("data:"))
                    {
                        dataBuilder.AppendLine(line.Substring(5).Trim());
                    }
                    else if (line.StartsWith(":"))
                    {
                        // Comment line (heartbeat) - ignore
                        continue;
                    }
                    else if (string.IsNullOrWhiteSpace(line))
                    {
                        // Empty line indicates end of message
                        if (dataBuilder.Length > 0)
                        {
                            var data = dataBuilder.ToString().Trim();
                            await ProcessEventAsync(eventType ?? "message", data, endpoint, cancellationToken);
                            dataBuilder.Clear();
                            eventType = null;
                        }
                    }
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
                _requestLogService.LogSSEEvent("connected", $"Connection confirmed from {sourceEndpoint}");
                return;
            }

            // Parse order event
            if (eventType == "order_created" || eventType == "order_updated" || eventType == "order")
            {
                // Log raw event with truncated data for display
                _requestLogService.LogSSEEvent(eventType, data.Length > 100 ? data.Substring(0, 100) + "..." : data, data);

                var order = JsonSerializer.Deserialize<Order>(data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (order != null)
                {
                    // Log parsed order with full JSON data
                    _requestLogService.LogOrderReceived(order.Id, order.TableNumber, order.Total, data);

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
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse order data: {Data}", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SSE event");
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
}
