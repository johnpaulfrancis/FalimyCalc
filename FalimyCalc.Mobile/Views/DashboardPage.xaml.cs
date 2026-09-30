using FalimyCalc.Mobile.Database.Entities;
using FalimyCalc.Mobile.Services;

namespace FalimyCalc.Mobile.Views;

public partial class DashboardPage : ContentPage
{
    private readonly LocalExpenseService _expenseService;

    public DashboardPage(LocalExpenseService expenseService)
    {
        InitializeComponent();
        _expenseService = expenseService;
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

    private async void OnAddExpenseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ExpenseFormPage));
    }

    private async void OnViewAllClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ExpenseListPage");
    }

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
