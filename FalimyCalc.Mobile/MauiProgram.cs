using FalimyCalc.Mobile.Database;
using FalimyCalc.Mobile.Services;
using FalimyCalc.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace FalimyCalc.Mobile;

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

        // ── Database ───────────────────────────────────────────────────────
        builder.Services.AddSingleton<LocalDatabase>();

        // ── HTTP client for sync ───────────────────────────────────────────
        // Timeout is set here once — HttpClient does not allow changing it
        // after the first request has been sent.
        builder.Services.AddSingleton<HttpClient>(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        });

        // ── Services ───────────────────────────────────────────────────────
        builder.Services.AddSingleton<LocalExpenseService>();
        builder.Services.AddSingleton<SyncClient>();

        // ── Pages ──────────────────────────────────────────────────────────
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<ExpenseListPage>();
        builder.Services.AddTransient<ExpenseFormPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
