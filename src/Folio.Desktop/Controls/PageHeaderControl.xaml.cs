using System.Windows;
using System.Windows.Controls;

namespace Folio.Desktop.Controls;

/// <summary>
/// The "Libro mayor" page header (ADR-0014): company name in small café caps, a
/// big serif page title, a 2px green underline, and a slot for whatever
/// page-specific actions (a search box, a primary button) sit on the right —
/// used by <c>WelcomeView</c> and <c>ChartOfAccountsView</c>, ready for M5+ pages
/// to reuse.
/// </summary>
public partial class PageHeaderControl : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeaderControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CompanyNameProperty = DependencyProperty.Register(
        nameof(CompanyName), typeof(string), typeof(PageHeaderControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeaderActionsProperty = DependencyProperty.Register(
        nameof(HeaderActions), typeof(object), typeof(PageHeaderControl));

    public PageHeaderControl()
    {
        // No "DataContext = this": that would make the hosting page's own bindings
        // on this control (Title="{Binding Greeting}") and everything placed in
        // HeaderActions (a search box bound to SearchText) resolve against this
        // control instead of the page's ViewModel. The control's XAML reaches its
        // own properties through ElementName="Root" instead.
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string CompanyName
    {
        get => (string)GetValue(CompanyNameProperty);
        set => SetValue(CompanyNameProperty, value);
    }

    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }
}
