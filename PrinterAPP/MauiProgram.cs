using Microsoft.Extensions.Logging;
using PrinterAPP.Services;

namespace PrinterAPP
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Register services
            builder.Services.AddSingleton<IPrinterService, SimplePrinterService>();
            builder.Services.AddSingleton<IEventStreamingService, EventStreamingService>();
            builder.Services.AddSingleton<OrderPrintService>();

            // Register pages
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
