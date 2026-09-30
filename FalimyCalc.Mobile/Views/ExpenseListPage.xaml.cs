using FalimyCalc.Mobile.Database.Entities;
using FalimyCalc.Mobile.Services;

namespace FalimyCalc.Mobile.Views;

public partial class ExpenseListPage : ContentPage
{
    private readonly LocalExpenseService _expenseService;
    private string? _searchTerm;

    public ExpenseListPage(LocalExpenseService expenseService)
    {
        InitializeComponent();
        _expenseService = expenseService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExpensesAsync();
    }

    private async Task LoadExpensesAsync()
    {
        var expenses = await _expenseService.GetExpensesAsync(searchTerm: _searchTerm);
        ExpensesCollection.ItemsSource = expenses;
        RefreshView.IsRefreshing = false;
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadExpensesAsync();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchTerm = e.NewTextValue;
        await LoadExpensesAsync();
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ExpenseFormPage));
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: LocalExpense expense })
        {
            await Shell.Current.GoToAsync(
                $"{nameof(ExpenseFormPage)}?globalId={expense.GlobalId}");
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: LocalExpense expense })
        {
            var confirm = await DisplayAlert(
                "Delete Expense",
                $"Delete \"{expense.Description}\" (₹ {expense.Amount:N2})?",
                "Delete", "Cancel");

            if (confirm)
            {
                await _expenseService.DeleteExpenseAsync(expense);
                await LoadExpensesAsync();
            }
        }
    }
}
