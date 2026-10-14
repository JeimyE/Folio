using System.Windows;
using System.Windows.Controls;
using Folio.Desktop.ViewModels;

namespace Folio.Desktop.Views;

public partial class ChartOfAccountsView : UserControl
{
    public ChartOfAccountsView()
    {
        InitializeComponent();
    }

    // TreeView.SelectedItem has no setter, so it can't be bound directly — this is
    // the standard WPF workaround, a pure view concern (wiring a non-bindable native
    // control property to the ViewModel), not business logic.
    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is ChartOfAccountsViewModel viewModel)
        {
            viewModel.SelectedNode = e.NewValue as AccountTreeNodeViewModel;
        }
    }
}
