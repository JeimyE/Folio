using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Folio.Desktop.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Desktop.ViewModels;

/// <summary>
/// The root content of <c>MainWindow</c>: shows <see cref="LoginViewModel"/> until a
/// <see cref="UserSignedInMessage"/> arrives, then swaps to <see cref="ShellViewModel"/>
/// (the side-navigation shell) until a <see cref="UserSignedOutMessage"/> sends it back.
/// Neither Login nor Shell know about each other directly — only about the messenger.
/// </summary>
public sealed partial class ShellCoordinatorViewModel :
    ObservableObject,
    IRecipient<UserSignedInMessage>,
    IRecipient<UserSignedOutMessage>
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private object? _currentViewModel;

    public ShellCoordinatorViewModel(IServiceProvider serviceProvider, IMessenger messenger)
    {
        _serviceProvider = serviceProvider;
        messenger.RegisterAll(this);
        ShowLogin();
    }

    public void Receive(UserSignedInMessage message)
    {
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        shell.Initialize(message.FullName, message.Role);
        CurrentViewModel = shell;
    }

    public void Receive(UserSignedOutMessage message) => ShowLogin();

    private void ShowLogin() => CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
}
