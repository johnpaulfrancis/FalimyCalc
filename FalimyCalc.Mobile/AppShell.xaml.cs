using FalimyCalc.Mobile.Views;

namespace FalimyCalc.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes that are navigated to programmatically
        // (not declared as tabs in the Shell XAML)
        Routing.RegisterRoute(nameof(ExpenseFormPage), typeof(ExpenseFormPage));
    }
}
