using FalimyCalc.Mobile.Database.Entities;
using FalimyCalc.Mobile.Services;

namespace FalimyCalc.Mobile.Views;

[QueryProperty(nameof(GlobalIdString), "globalId")]
public partial class ExpenseFormPage : ContentPage
{
    private readonly LocalExpenseService _expenseService;
    private List<LocalCategory> _categories = [];
    private LocalExpense? _editingExpense;

    // Received via Shell navigation query parameter
    public string? GlobalIdString { get; set; }

    public ExpenseFormPage(LocalExpenseService expenseService)
    {
        InitializeComponent();
        _expenseService = expenseService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _categories = await _expenseService.GetCategoriesAsync();

        // Populate category picker
        CategoryPicker.ItemsSource = _categories.Select(c => $"{c.Icon} {c.Name}").ToList();

        // Edit mode: load existing expense
        if (!string.IsNullOrEmpty(GlobalIdString) &&
            Guid.TryParse(GlobalIdString, out var globalId))
        {
            _editingExpense = await _expenseService.GetExpenseAsync(globalId);

            if (_editingExpense is not null)
            {
                Title = "Edit Expense";
                AmountEntry.Text = _editingExpense.Amount.ToString("F2");
                DescriptionEntry.Text = _editingExpense.Description;
                TransactionDatePicker.Date = _editingExpense.TransactionDate;
                SaveButton.Text = "Save Changes";

                if (_editingExpense.CategoryGlobalId.HasValue)
                {
                    var idx = _categories.FindIndex(
                        c => c.GlobalId == _editingExpense.CategoryGlobalId.Value);
                    if (idx >= 0) CategoryPicker.SelectedIndex = idx;
                }
            }
        }
        else
        {
            TransactionDatePicker.Date = DateTime.Today;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        if (!decimal.TryParse(AmountEntry.Text, out var amount) || amount <= 0)
        {
            ErrorLabel.Text = "Please enter a valid amount greater than zero.";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(DescriptionEntry.Text))
        {
            ErrorLabel.Text = "Description is required.";
            ErrorLabel.IsVisible = true;
            return;
        }

        SaveButton.IsEnabled = false;

        try
        {
            Guid? categoryGlobalId = null;
            if (CategoryPicker.SelectedIndex >= 0)
                categoryGlobalId = _categories[CategoryPicker.SelectedIndex].GlobalId;

            if (_editingExpense is null)
            {
                // Create new expense
                await _expenseService.CreateExpenseAsync(new LocalExpense
                {
                    Amount = amount,
                    Description = DescriptionEntry.Text.Trim(),
                    TransactionDate = (DateTime)TransactionDatePicker.Date,
                    CategoryGlobalId = categoryGlobalId
                });
            }
            else
            {
                // Update existing expense
                _editingExpense.Amount = amount;
                _editingExpense.Description = DescriptionEntry.Text.Trim();
                _editingExpense.TransactionDate = (DateTime)TransactionDatePicker.Date;
                _editingExpense.CategoryGlobalId = categoryGlobalId;
                await _expenseService.UpdateExpenseAsync(_editingExpense);
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Failed to save: {ex.Message}";
            ErrorLabel.IsVisible = true;
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
