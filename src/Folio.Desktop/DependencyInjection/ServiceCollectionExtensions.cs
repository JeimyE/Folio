using CommunityToolkit.Mvvm.Messaging;
using Folio.Desktop.Navigation;
using Folio.Desktop.Theming;
using Folio.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Desktop.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFolioDesktop(this IServiceCollection services)
    {
        services.AddSingleton(WeakReferenceMessenger.Default as IMessenger);
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<ShellCoordinatorViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<WelcomeViewModel>();

        services.AddSingleton<MainWindow>();

        return services;
    }
}
