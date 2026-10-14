# ADR-0013: Guard against `Hide()` re-entering its own `Closing` handler

## Context

After ADR-0011 and ADR-0012 shipped, creating or editing an account saved correctly
to the database (confirmed: re-creating the same code correctly failed with "ya
existe una cuenta con ese código") but the tree never refreshed with the new account
— not even after reopening the dialog, only after restarting the whole app.

Pulling the real `Wpf.Ui.Controls.ContentDialog` source (lepoco/wpfui) showed why.
`Hide(result)` is not just "close the dialog" — it is the **same method** the
Primary button's internal click handler calls, and it is what *raises* `Closing` in
the first place:

```csharp
public void Hide(ContentDialogResult result)
{
    var closingEventArgs = new ContentDialogClosingEventArgs(ClosingEvent, this) { Result = result };
    RaiseEvent(closingEventArgs);
    if (!closingEventArgs.Cancel)
    {
        _ = Tcs?.TrySetResult(result);
    }
}
```

ADR-0011's `DialogService.ShowAccountFormAsync` called `dialog.Hide(ContentDialogResult.Primary)`
itself, from inside its own `Closing` handler, once `SaveAsync()` succeeded — intending
to close the dialog after validating. Because `Hide()` raises `Closing` every time it
runs, that call **re-entered the same handler**: `args.Result` was `Primary` again,
so it cancelled again and called `SaveAsync()` a *second* time — which now failed
("code already exists," since the first call had already committed) and therefore
never called `Hide()` again. Neither of the two `Hide()` calls ever reached
`Tcs.TrySetResult` (the first was cancelled on purpose, awaiting the save; the second
was cancelled by the re-entered handler), so the dialog's underlying `Task` — the one
`ShowAsync()` awaits, and that `DialogService.ShowAccountFormAsync` in turn awaits —
never completed. The code that reloads the tree sits right after that `await` in
`ChartOfAccountsViewModel`, so it never ran either. The save itself was never the
problem; the signal telling the ViewModel the save had finished never arrived.

## Decision

A local `isClosing` flag, captured by the `Closing` lambda, breaks the recursion:

```csharp
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
```

The first `Closing` raise (from the user's click) cancels and awaits the save, as
before. Once the save succeeds, `isClosing` is set *before* calling `Hide()`, so the
`Closing` raise that `Hide()` triggers this time is let through immediately — `Cancel`
stays `false`, and `Hide()`'s own check completes the dialog's task with `Primary`,
exactly once.

## Consequences

- This bug was invisible to `ChartOfAccountsViewModelTests` because those tests mock
  `IDialogService` entirely — the recursion lives one layer below, inside the real
  `ContentDialog`/`DialogService`, which nothing in this codebase unit-tests directly
  (there is no live-WPF-chrome test harness here, consistent with the project's
  existing reliance on manual verification for anything that needs a real `Window`).
  The ViewModel tests were strengthened instead to exercise the *real*
  `AccountFormViewModel.SaveAsync()` through the `IDialogService` mock (the mock's
  `ShowAccountFormAsync` callback now actually calls the real `SaveAsync()` it was
  handed, rather than just returning a canned `true`/`false`), so they at least prove
  the ViewModel's own reload-and-reselect contract holds for every outcome — they just
  can't reproduce the `Hide()` recursion itself.
- Any future dialog that needs to validate asynchronously before closing must use this
  same `isClosing`-style guard, not a bare `Hide()` call — `Hide()` always re-raises
  `Closing`, with no built-in re-entrancy protection of its own.
