using FalimyCalc.Mobile.Services;
using FalimyCalc.Mobile.Views;

namespace FalimyCalc.Mobile;

public partial class App : Application
{
    public App(SyncClient syncClient)
    {
        InitializeComponent();

        // Restore the saved server URL so SyncClient is ready before first use
        var savedUrl = Preferences.Get(SettingsPage.ServerUrlKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(savedUrl))
            syncClient.ConfigureServerUrl(savedUrl);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}