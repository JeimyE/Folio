# ADR-0011: Account form dialog closes itself after an async save, instead of on click

## Context

M4 needed a modal create/edit form for accounts. WPF-UI's `ContentDialog` (used here
instead of a second, competing dialog framework) raises a `Closing` event before it
actually closes, with a `Cancel` flag the handler can set to keep it open — but that
event is a plain synchronous `TypedEventHandler`, unlike WinUI's `ContentDialog`, which
has an async deferral (`args.GetDeferral()`) made for exactly this case. There is no
way to `await` inside WPF-UI's `Closing` handler and have the dialog actually wait for
that awaited work before deciding whether to close.

The form's "Guardar" button has to run real validation that can only happen
asynchronously (`IChartOfAccountsService.CreateAsync`/`UpdateAsync` — duplicate code,
missing parent, type mismatch, movements blocking a change, etc., all round-trip
through the repository). A failed save must keep the dialog open and show the error
inline, not close and lose the user's input.

## Decision

`DialogService.ShowAccountFormAsync` always sets `args.Cancel = true` the moment the
Primary button's `Closing` event fires — synchronously, before anything async
happens — so the framework never closes the dialog on its own for that click. It then
awaits `AccountFormViewModel.SaveAsync()` and, only if that returns `true`, closes the
dialog itself with `dialog.Hide(ContentDialogResult.Primary)`. If the save fails,
`SaveAsync` has already set `ErrorMessage` on the form's ViewModel (bound and visible
in `AccountFormView`), and the dialog simply stays open — canceling the close was the
only thing that already happened, so there's nothing further to undo.

## Consequences

- The Primary button's label ("Guardar") promises a click will close the dialog, but
  it never actually does directly — closing only ever happens from the `Hide()` call
  inside the `Closing` handler's continuation. Anyone extending this dialog needs to
  know that rule, so it's documented at the `Closing` subscription itself, not just
  here.
- `AccountFormViewModel.SaveAsync()` is the single place that decides success/failure
  for both create and edit — `DialogService` has no business logic of its own, it only
  orchestrates *when* the dialog is allowed to disappear.
- The equivalent WinUI deferral pattern (`args.GetDeferral()`) does not exist on this
  control; a future WPF-UI upgrade that adds it would let this collapse to a more
  conventional single `Closing` handler, but there's no behavioral reason to revisit
  it before then.
