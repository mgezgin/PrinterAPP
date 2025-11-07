using PrinterAPP.Models;

namespace PrinterAPP.Services;

public interface IEventStreamingService
{
    event EventHandler<OrderEvent>? OrderReceived;
    event EventHandler<string>? ConnectionStatusChanged;
    Task StartListeningAsync(CancellationToken cancellationToken = default);
    Task StopListeningAsync();
    bool IsListening { get; }
}
