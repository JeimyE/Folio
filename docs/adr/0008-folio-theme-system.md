# ADR-0008: "Bosque" brand palette, layered on top of WPF-UI's own theming

## Context

SPEC §3 asks for a modern Fluent look via WPF-UI, in both light and dark theme. On top
of that, Folio needed its own brand identity — the "Bosque" (forest) palette — rather
than shipping with WPF-UI's default blue accent. The two theming systems (WPF-UI's own
control colors, and Folio's brand colors) needed to change together, consistently, and
the user's choice needed to survive restarting the app.

## Decision

**Two parallel `ResourceDictionary` files, same keys, swapped together.**
`Resources/Theme/Folio.Light.xaml` and `Folio.Dark.xaml` each define the same six brush
keys (`FolioPrimaryBrush`, `FolioAccentBrush`, `FolioBackgroundBrush`,
`FolioSurfaceBrush`, `FolioTextBrush`, `FolioErrorBrush`) with the light/dark hex values
from the brand spec. Every view references these keys via `DynamicResource` (not
`StaticResource`), so swapping which dictionary is merged into
`Application.Current.Resources.MergedDictionaries` re-colors every view immediately,
without rebuilding any control.

**`IThemeService.ApplyTheme` changes both systems in the same call**: it calls WPF-UI's
own `ApplicationThemeManager.Apply(ApplicationTheme.Light/Dark)` (recolors WPF-UI's
built-in controls — buttons, window chrome, etc.) and then removes/re-adds the Folio
brand dictionary. Doing both together, in one method, is what keeps them from drifting
out of sync — there's exactly one place that "changes the theme," and it always does
both halves.

**Startup theme resolution order**: a saved preference (if one exists) wins; otherwise
the current Windows theme (via `ApplicationThemeManager.GetSystemTheme()`) is used. The
preference is a small JSON file (`%LOCALAPPDATA%\Folivora\Folio\settings.json`,
`{ "Theme": "Dark" }`), read once at startup (`ApplyStartupTheme`, not persisted) and
written only when the user explicitly toggles the theme (`ToggleTheme`, persisted).
This file lives alongside the database and logs (same `FolioPaths` root) rather than in
the registry or `ApplicationData`, keeping every piece of Folio's local state under one
folder a user (or an uninstaller) can find.

## Consequences

- A view that uses `StaticResource` instead of `DynamicResource` for a Folio brush
  would freeze at whichever theme was active when it was first rendered — this is a
  real footgun to watch for in M4+ views built on top of this shell.
- The settings file has exactly one key for now (`Theme`). If more user preferences
  need to persist later, they can be added to the same `FolioSettings` class/file
  rather than inventing a second settings mechanism.
- `GetSystemTheme()` can return Windows' special accent themes (high-contrast variants,
  etc.), not just plain light/dark; anything other than an explicit "Dark" report maps
  to Light. This is a deliberate simplification for M3, not a bug — accessible
  high-contrast theming is out of scope for now.
