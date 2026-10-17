using FalimyCalc.Mobile.Database.Entities;
using FalimyCalc.Mobile.Services;

namespace FalimyCalc.Mobile.Views;

public partial class DashboardPage : ContentPage
{
    private readonly LocalExpenseService _expenseService;
    private readonly SyncClient _syncClient;

    public DashboardPage(LocalExpenseService expenseService, SyncClient syncClient)
    {
        InitializeComponent();
        _expenseService = expenseService;
        _syncClient = syncClient;

        // Listen for sync state changes
        _syncClient.SyncStateChanged += OnSyncStateChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        var now = DateTime.Now;
        MonthLabel.Text = now.ToString("MMMM yyyy");

        var total = await _expenseService.GetMonthlyTotalAsync(now.Year, now.Month);
        MonthlyTotalLabel.Text = $"₹ {total:N2}";

        var recent = await _expenseService.GetRecentExpensesAsync(5);
        RecentExpensesCollection.ItemsSource = recent;

        RefreshView.IsRefreshing = false;
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadDashboardAsync();
    }

    // ── Sync ───────────────────────────────────────────────────────────────

    private async void OnSyncClicked(object sender, EventArgs e)
    {
        var serverUrl = Preferences.Get(SettingsPage.ServerUrlKey, string.Empty);

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            await DisplayAlert("Setup Required",
                "Please set your server URL in Settings (⚙) first.",
                "OK");
            await Shell.Current.GoToAsync(nameof(SettingsPage));
            return;
        }

        _syncClient.ConfigureServerUrl(serverUrl);
        await _syncClient.SyncAsync();

        // Refresh dashboard after sync
        await LoadDashboardAsync();
    }

    private void OnSyncStateChanged(SyncResult result)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (result.State)
            {
                case SyncState.Syncing:
                    SyncButton.IsEnabled = false;
                    SyncButton.Text = "Syncing…";
                    SyncStatusLabel.Text = "Syncing with server…";
                    SyncStatusLabel.TextColor = Colors.Gray;
                    break;

                case SyncState.Success:
                    SyncButton.IsEnabled = true;
                    SyncButton.Text = "↕ Sync";
                    SyncStatusLabel.Text = $"✅ {result.Message} · {result.LastSyncTime:HH:mm}";
                    SyncStatusLabel.TextColor = Color.FromArgb("#43A047");
                    break;

                case SyncState.ServerUnreachable:
                    SyncButton.IsEnabled = true;
                    SyncButton.Text = "↕ Sync";
                    SyncStatusLabel.Text = "⚠️ Server unreachable — working offline";
                    SyncStatusLabel.TextColor = Color.FromArgb("#F57C00");
                    break;

                case SyncState.Failed:
                    SyncButton.IsEnabled = true;
                    SyncButton.Text = "↕ Sync";
                    SyncStatusLabel.Text = $"❌ {result.Message}";
                    SyncStatusLabel.TextColor = Color.FromArgb("#E53935");
                    break;
            }
        });
    }

    // ── Navigation ─────────────────────────────────────────────────────────

    private async void OnAddExpenseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ExpenseFormPage));
    }

    private async void OnViewAllClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ExpenseListPage");
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: LocalExpense expense })
        {
            var confirm = await DisplayAlert(
                "Delete Expense",
                $"Delete \"{expense.Description}\"?",
                "Delete", "Cancel");

            if (confirm)
            {
                await _expenseService.DeleteExpenseAsync(expense);
                await LoadDashboardAsync();
            }
        }
    }
}
