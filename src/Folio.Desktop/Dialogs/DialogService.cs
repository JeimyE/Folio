using Folio.Desktop.Resources;
using Folio.Desktop.ViewModels;
using Folio.Desktop.Views;
using Wpf.Ui.Controls;

namespace Folio.Desktop.Dialogs;

public sealed class DialogService(MainWindow mainWindow) : IDialogService
{
    public async Task<bool> ShowAccountFormAsync(AccountFormViewModel viewModel, string title)
    {
        var dialog = new ContentDialog(ContentDialogHost.GetForWindow(mainWindow))
        {
            Title = title,
            Content = new AccountFormView { DataContext = viewModel },
            PrimaryButtonText = Strings.DialogSave,
            CloseButtonText = Strings.DialogCancel,
        };

        // ContentDialog's Closing event is synchronous, so it can't await a save
        // itself (WinUI's deferral pattern has no equivalent here). Instead: always
        // cancel the Primary-button close, run the save, and only then close the
        // dialog by hand — leaving it open with ErrorMessage set if the save failed.
        //
        // Hide() is also what RAISES Closing in the first place — clicking Primary
        // calls Hide(Primary) internally, which raises Closing and only completes
        // the dialog if nothing cancelled it. So calling Hide() ourselves below, to
        // close the dialog after a successful save, re-raises Closing and re-enters
        // this very handler: it would cancel again and run SaveAsync() a SECOND
        // time (which then fails — the account already exists — and never calls
        // Hide() again), leaving the dialog's underlying task permanently
        // incomplete. `isClosing` breaks that recursion: once we've decided to
        // close, the re-raised Closing is let through instead of re-cancelled.
        var isClosing = false;
        dialog.Closing += async (_, args) =>
        {
            if (args.Result != ContentDialogResult.Primary || isClosing)
            {
                return;
            }

            args.Cancel = true;
            if (await viewModel.SaveAsync())
            {
                isClosing = true;
                dialog.Hide(ContentDialogResult.Primary);
            }
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog(ContentDialogHost.GetForWindow(mainWindow))
        {
            Title = title,
            Content = message,
            PrimaryButtonText = Strings.DialogConfirm,
            CloseButtonText = Strings.DialogCancel,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
