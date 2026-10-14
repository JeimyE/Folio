using Folio.Desktop.ViewModels;

namespace Folio.Desktop.Dialogs;

/// <summary>
/// Shows modal dialogs over the main window's content. Kept separate from
/// <c>INavigationService</c> (which swaps the whole content area) because a dialog is
/// a short-lived overlay the caller waits on, not a navigation destination.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows the account create/edit form and keeps it open — showing
    /// whatever error the save attempt returned — until it is saved successfully or
    /// the user cancels. Returns whether it was saved.</summary>
    Task<bool> ShowAccountFormAsync(AccountFormViewModel viewModel, string title);

    /// <summary>Shows a yes/no confirmation and returns whether the user confirmed.</summary>
    Task<bool> ShowConfirmationAsync(string title, string message);
}
