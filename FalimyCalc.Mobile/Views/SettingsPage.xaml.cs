using FalimyCalc.Mobile.Services;

namespace FalimyCalc.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SyncClient _syncClient;
    private readonly LocalExpenseService _expenseService;

    public const string ServerUrlKey = "ServerUrl";

    public SettingsPage(SyncClient syncClient, LocalExpenseService expenseService)
    {
        InitializeComponent();
        _syncClient = syncClient;
        _expenseService = expenseService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load saved URL
        ServerUrlEntry.Text = Preferences.Get(ServerUrlKey, string.Empty);

        // Show device ID
        var deviceId = await _expenseService.GetDeviceIdAsync();
        DeviceIdLabel.Text = $"Device ID: {deviceId}";
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var url = ServerUrlEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            ShowStatus("Please enter a server URL.", isError: true);
            return;
        }

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            ShowStatus("URL must start with https:// or http://", isError: true);
            return;
        }

        // Save and apply
        Preferences.Set(ServerUrlKey, url);
        _syncClient.ConfigureServerUrl(url);

        ShowStatus("Testing connection...", isError: false);

        var reachable = await _syncClient.PingAsync();

        if (reachable)
            ShowStatus("✅ Connected! Server is reachable.", isError: false);
        else
            ShowStatus("❌ Cannot reach server. Check the URL and that the server is running.", isError: true);
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusBorder.BackgroundColor = isError
            ? Color.FromArgb("#FFF0F0")
            : Color.FromArgb("#F0FFF0");
        StatusBorder.Stroke = isError
            ? Color.FromArgb("#E53935")
            : Color.FromArgb("#43A047");
        StatusBorder.IsVisible = true;
    }
}
