using System.Collections.ObjectModel;
using System.Linq;
using PrinterAPP.Services;

namespace PrinterAPP;

public partial class LogsPage : ContentPage
{
    private readonly RequestLogService _requestLogService;
    private readonly ObservableCollection<LogEntry> _kitchenLogs;
    private readonly ObservableCollection<LogEntry> _serviceLogs;

    public LogsPage(RequestLogService requestLogService)
    {
        InitializeComponent();
        _requestLogService = requestLogService;

        // Create filtered collections
        _kitchenLogs = new ObservableCollection<LogEntry>();
        _serviceLogs = new ObservableCollection<LogEntry>();

        // Bind to filtered collections
        KitchenLogsCollectionView.ItemsSource = _kitchenLogs;
        ServiceLogsCollectionView.ItemsSource = _serviceLogs;

        // Subscribe to log changes
        _requestLogService.Logs.CollectionChanged += OnLogsCollectionChanged;

        // Initial population
        RefreshLogs();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unsubscribe to prevent memory leaks
        _requestLogService.Logs.CollectionChanged -= OnLogsCollectionChanged;
    }

    private void OnLogsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (LogEntry newLog in e.NewItems)
                {
                    if (newLog.Source == "Kitchen")
                    {
                        _kitchenLogs.Insert(0, newLog);
                    }
                    else if (newLog.Source == "Service")
                    {
                        _serviceLogs.Insert(0, newLog);
                    }
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                RefreshLogs();
            }
        });
    }

    private void RefreshLogs()
    {
        _kitchenLogs.Clear();
        _serviceLogs.Clear();

        foreach (var log in _requestLogService.Logs)
        {
            if (log.Source == "Kitchen")
            {
                _kitchenLogs.Add(log);
            }
            else if (log.Source == "Service")
            {
                _serviceLogs.Add(log);
            }
        }
    }

    private void OnClearLogsClicked(object sender, EventArgs e)
    {
        _requestLogService.ClearLogs();
    }

    private void OnKitchenTabClicked(object sender, EventArgs e)
    {
        // Switch to Kitchen tab
        KitchenLogsSection.IsVisible = true;
        ServiceLogsSection.IsVisible = false;

        // Update button styles
        KitchenTabButton.BackgroundColor = Color.FromArgb("#4CAF50");
        KitchenTabButton.TextColor = Colors.White;
        KitchenTabButton.FontAttributes = FontAttributes.Bold;

        ServiceTabButton.BackgroundColor = Color.FromArgb("#E0E0E0");
        ServiceTabButton.TextColor = Color.FromArgb("#666");
        ServiceTabButton.FontAttributes = FontAttributes.None;
    }

    private void OnServiceTabClicked(object sender, EventArgs e)
    {
        // Switch to Service tab
        KitchenLogsSection.IsVisible = false;
        ServiceLogsSection.IsVisible = true;

        // Update button styles
        ServiceTabButton.BackgroundColor = Color.FromArgb("#2196F3");
        ServiceTabButton.TextColor = Colors.White;
        ServiceTabButton.FontAttributes = FontAttributes.Bold;

        KitchenTabButton.BackgroundColor = Color.FromArgb("#E0E0E0");
        KitchenTabButton.TextColor = Color.FromArgb("#666");
        KitchenTabButton.FontAttributes = FontAttributes.None;
    }

    // Toggle methods for expandable sections
    private void OnToggleRequestSection(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is LogEntry logEntry)
        {
            logEntry.IsRequestExpanded = !logEntry.IsRequestExpanded;
        }
    }

    private void OnToggleResponseSection(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is LogEntry logEntry)
        {
            logEntry.IsResponseExpanded = !logEntry.IsResponseExpanded;
        }
    }

    private void OnToggleRequestHeaders(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is LogEntry logEntry)
        {
            logEntry.IsRequestHeadersExpanded = !logEntry.IsRequestHeadersExpanded;
        }
    }

    private void OnToggleResponseHeaders(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is LogEntry logEntry)
        {
            logEntry.IsResponseHeadersExpanded = !logEntry.IsResponseHeadersExpanded;
        }
    }

    private void OnToggleRequestBody(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is LogEntry logEntry)
        {
            logEntry.IsRequestBodyExpanded = !logEntry.IsRequestBodyExpanded;
        }
    }

    private void OnToggleResponseBody(object sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is LogEntry logEntry)
        {
            logEntry.IsResponseBodyExpanded = !logEntry.IsResponseBodyExpanded;
        }
    }
}
