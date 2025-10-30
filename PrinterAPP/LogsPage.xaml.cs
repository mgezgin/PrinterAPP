using PrinterAPP.Services;

namespace PrinterAPP;

public partial class LogsPage : ContentPage
{
    private readonly RequestLogService _requestLogService;

    public LogsPage(RequestLogService requestLogService)
    {
        InitializeComponent();
        _requestLogService = requestLogService;
        BindingContext = _requestLogService;
    }

    private void OnClearLogsClicked(object sender, EventArgs e)
    {
        _requestLogService.ClearLogs();
    }
}
