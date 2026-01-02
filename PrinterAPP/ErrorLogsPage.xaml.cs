using System.Collections.ObjectModel;
using System.Linq;
using PrinterAPP.Services;

namespace PrinterAPP;

public partial class ErrorLogsPage : ContentPage
{
    private readonly RequestLogService _requestLogService;
    private readonly ObservableCollection<LogEntry> _errorLogs;

    public ErrorLogsPage(RequestLogService requestLogService)
    {
        InitializeComponent();
        _requestLogService = requestLogService;

        // Create filtered collection for errors only
        _errorLogs = new ObservableCollection<LogEntry>();

        // Bind to filtered collection
        ErrorLogsCollectionView.ItemsSource = _errorLogs;

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
                    if (newLog.Type == LogType.Error || newLog.Type == LogType.PrintError)
                    {
                        _errorLogs.Insert(0, newLog);
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
        _errorLogs.Clear();

        foreach (var log in _requestLogService.Logs)
        {
            if (log.Type == LogType.Error || log.Type == LogType.PrintError)
            {
                _errorLogs.Add(log);
            }
        }
    }

    private void OnClearLogsClicked(object sender, EventArgs e)
    {
        _requestLogService.ClearLogs();
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
