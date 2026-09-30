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
        // Singleton: one SQLite connection for the app lifetime
        builder.Services.AddSingleton<LocalDatabase>();

        // ── Services ───────────────────────────────────────────────────────
        builder.Services.AddSingleton<LocalExpenseService>();

        // ── Pages ──────────────────────────────────────────────────────────
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<ExpenseListPage>();
        builder.Services.AddTransient<ExpenseFormPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
