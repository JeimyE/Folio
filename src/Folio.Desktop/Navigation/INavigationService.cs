namespace Folio.Desktop.Navigation;

/// <summary>
/// Resolves ViewModels from DI and exposes the current one for a content area to
/// display (the matching View is picked up via a <c>DataTemplate</c> keyed by
/// ViewModel type — see <c>App.xaml</c>). Deliberately not tied to WPF-UI's own
/// <c>NavigationView</c> navigation API, so ViewModels never reference WPF-UI types.
/// </summary>
public interface INavigationService
{
    object? CurrentViewModel { get; }

    /// <summary>Resolves a fresh <typeparamref name="TViewModel"/> from DI, makes it
    /// the current one, and returns it so the caller can initialize it further.</summary>
    TViewModel NavigateTo<TViewModel>() where TViewModel : class;
}
