using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Desktop.Navigation;

public sealed partial class NavigationService(IServiceProvider serviceProvider) : ObservableObject, INavigationService
{
    [ObservableProperty]
    private object? _currentViewModel;

    public TViewModel NavigateTo<TViewModel>() where TViewModel : class
    {
        var viewModel = serviceProvider.GetRequiredService<TViewModel>();
        CurrentViewModel = viewModel;
        return viewModel;
    }
}
