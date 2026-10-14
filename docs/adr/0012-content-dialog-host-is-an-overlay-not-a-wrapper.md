# ADR-0012: `ContentDialogHost` is a dialog overlay, not a page-content wrapper

## Context

After M4 shipped, every dialog (create/edit/delete confirmation) left the whole
window blank — just the title bar, a gray background, nothing else — the moment it
closed. It happened for every dialog, including the plain delete confirmation, which
has no custom close logic at all, which ruled out ADR-0011's manual
`Closing`/`Hide()` trick as the cause.

`MainWindow.xaml` had:

```xml
<ui:ContentDialogHost Grid.Row="1">
    <ContentControl Content="{Binding CurrentViewModel}" />
</ui:ContentDialogHost>
```

`ContentDialogHost` derives from `ContentControl`, which reads as "put your page
content here, dialogs render on top." Pulling the actual WPF-UI source
(`ContentDialogHost.cs`/`.xaml` from lepoco/wpfui) showed the opposite: its `Content`
property holds *the dialog currently showing*, not the page. `OnContentChanged`
treats `null → non-null` as "a dialog was just added" and `non-null → null` as "the
last dialog was removed"; its control template is a `Border` ("MaskLayer", the
dimming scrim) wrapping a `ContentPresenter` for that same `Content`, with a trigger
that collapses the whole thing — mask and all — when `Content` is `null`.

So nesting the real page `ContentControl` as the host's `Content` made the host treat
my own page as "the dialog" from the moment the window loaded. When `ContentDialog.
ShowAsync()` later set the host's `Content` to the actual dialog, it silently evicted
my page. When the dialog closed and the host's `Content` went back to `null` (never
restored to my page — there was nothing left to restore it to), the whole content
area collapsed for good, exactly matching the observed symptom.

## Decision

`ContentDialogHost` is a sibling overlay in the same grid cell as the real page
content, never a wrapper around it, and is left with no `Content` of its own at rest:

```xml
<Grid Grid.Row="1">
    <ContentControl Content="{Binding CurrentViewModel}" />
    <ui:ContentDialogHost />
</Grid>
```

The page `ContentControl` and the host now occupy the same `Grid` cell as
independent siblings; the host's `Content` is only ever set internally by whichever
`ContentDialog` is currently showing, and the page underneath is never touched.

## Consequences

- No VS binding-error output is specific to this bug (the symptom is a correct,
  silent `Content` swap, not a failed binding) — the two binding errors seen while
  diagnosing were unrelated/pre-existing noise, not the root cause. The real signal
  was behavioral: *every* dialog broke the window, including one with no custom
  close logic, which is what pointed away from ADR-0011 and toward the host itself.
- Any future screen that wants its own dialogs just needs a `ContentDialogHost`
  somewhere in its own visual tree (one per window is enough here, found via
  `ContentDialogHost.GetForWindow`) — it must never again be the thing a page's
  content lives inside.
- This is unrelated to ADR-0011, which remains correct: the manual `Closing`/`Hide()`
  dance is still necessary because WPF-UI's `ContentDialog` has no WinUI-style async
  deferral, independent of where the host sits in the tree.
