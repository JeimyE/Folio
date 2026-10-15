# ADR-0014: "Libro mayor" design system

## Context

Until M4 every screen picked its own font sizes, spacing and colors from a small
"Bosque" palette (ADR-0008). Before M5 adds journal entries — the screen users will
spend the most time in — Jeimy wanted one fixed visual language that every new
screen inherits instead of improvising: paper tones, a forest green for titles, a
café accent, serif titles and monospaced numbers, like a printed ledger. The
reference is the HTML mockup `docs/design/catalogo-libro-mayor.html`. It is
translated to WPF, not copied: several CSS features it relies on have no WPF
equivalent.

The detail panel in the mockup also shows data the app did not have yet (Debe/Haber
split, a balance per month, recent entries, an accounting-equation check), so this
milestone adds real queries for it, not placeholder numbers.

## Decision

**Tokens.** `Folio.Light.xaml` / `Folio.Dark.xaml` were rewritten with the new token
set (`FolioPaperBrush`, `FolioTitleBrush`, `FolioActionBrush`, `FolioAccentBrush`,
`FolioLineDottedBrush`, `FolioPositiveBrush`/`FolioNegativeBrush`, …). The old
"Bosque" keys were renamed without aliases: they are internal resource keys with no
outside consumers, and two names for one color is how palettes drift.
`FolioErrorBrush` is gone; `FolioNegativeBrush` covers the same role. This replaces
the palette part of ADR-0008; the theme-swap mechanism from ADR-0008 is unchanged.

**One accent green.** WPF-UI's accent (`ThemeService`), the primary button and
`SystemColors.Highlight*` all use the "títulos" green (`#2E4A36` / `#9CC096`), exactly
like the mockup. In dark mode that green is light, so text on it must be dark; a new
token, `FolioOnTitleBrush`, carries that role. It happens to equal `FolioPaperBrush`
in both themes, but it is its own token because "text on an accent surface" and
"page background" are different roles that may diverge. The "acciones" green
(`FolioActionBrush`) is kept only where the mockup uses it apart from the button: the
current-month bar of the 6-month chart.

**The ledger tree is the one exception to the accent.** A selected tree row uses
`FolioRowSelectedBrush` ("papel oscuro") plus a 3px green left edge, not a solid
green fill. The default `TreeViewItem` template also puts the row header in an
`Auto`-width column, so a row could never stretch — which the dotted leader line and
the right-aligned amounts both need. So `FolioLedgerTreeViewItemStyle` retemplates
`TreeViewItem` (full-width row, chevron expander, children indented by the items
host margin) and sets the selected and search-match colors directly. That also made
the originally planned local re-shadowing of `SystemColors.Highlight*` unnecessary.

**Fonts.** Fraunces (titles), Source Sans 3 (body) and IBM Plex Mono (every code and
every amount) are embedded as `Resource` items from the OFL mirror of
`google/fonts`. Fraunces and Source Sans 3 are only published there as variable
fonts, so one `.ttf` per family (plus Fraunces Italic) is embedded and DirectWrite
resolves the named weights from it. IBM Plex Mono ships static cuts (Regular,
SemiBold, Bold). **Each family's `OFL.txt` license is shipped next to its fonts in
`src/Folio.Desktop/Resources/Fonts/<Family>/OFL.txt`**, and the README has a
"Fonts" section pointing to them, as the SIL Open Font License requires when the
fonts are redistributed.

**WPF limitations, handled openly:**
- *No letter-spacing.* `TextBlock` has no tracking property. The small café caps
  labels ("FOLIVORA SOFTWARE", "CUENTA T") are stored already upper-case in
  `Strings.resx`; the one dynamic case, the company name, goes through
  `UpperCaseConverter`. The look is approximated with caps + small size + semibold +
  café, without real tracking.
- *No stretchable dotted border.* The leader line is a `Rectangle` with
  `Height="1"` and a dashed `Stroke` (`FolioDottedLineStyle`).
- *No `border-style: double`.* The accounting double underline under "Saldo" is two
  1px `Border`s stacked 2px apart.
- *No offset `box-shadow`.* `OffsetShadowCard` is a small `ContentControl` whose
  template stacks two `Border`s (the shadow, offset 4px, behind the real card). It is
  a control and not just a `Style` so every page uses the same element with one name.
  It is used for the login card and the detail panel, but not inside dialogs, which
  already have their own chrome.

**Charts by hand.** The 6-month chart is six `Rectangle`s in a `UniformGrid`, with
heights computed in the ViewModel. LiveCharts would pull SkiaSharp/OpenTK into the
app for six static bars with no interactivity.

**Balance per month is the closing balance, not the month's movement.** Each bar is
the account's balance at the end of that month, accumulated from the very first
posted entry, signed by the account's normal balance. The repository returns every
month with activity up to the end of the window (no lower bound); the service keeps
a running total and carries it forward, so a month with no activity keeps the
previous month's balance, and the first month of the window already includes
everything posted before it. A month's net movement would make a steady account
look like it is empty most of the time, which is not what a balance chart is for.

**The equation check includes the period's result.** The footnote compares Assets
against every other postable account (credit − debit): Liabilities, Equity *and*
Revenue/Cost/Expense. Until a fiscal year is closed, the income-statement accounts
are the period's result, which is part of Patrimonio:
`A = L + E + (Ingresos − Costos − Gastos)`. Comparing only Liabilities + Equity would
show ✗ on every correctly balanced book with sales or expenses.

**Company name per page, role in the sidebar.** The company name in each page header
comes from a small `ICompanyContextService` that each page ViewModel loads in its own
`InitializeAsync`, not threaded through `ShellViewModel.Initialize` (which would have
changed a signature used by 20 tests for no gain). The user's role now flows from
`AuthenticationResult` through `UserSignedInMessage` to `ShellViewModel` and is shown
through `UserRoleDisplayConverter`.

**`ChartOfAccountsService` takes an `IClock`.** "The last 6 months" depends on today;
`IClock` keeps that deterministic in tests (CLAUDE.md: no `DateTime.Now`).

**No flash of the wrong theme at startup.** `ThemeService` has no injected
dependencies, so `App.xaml.cs` creates a throwaway instance and calls
`ApplyStartupTheme()` (which reads `settings.json`) *before* constructing
`SplashWindow`, whose colors are now `DynamicResource`s. The Host-resolved
`IThemeService` applies the same theme again later — redundant but harmless.

**Dialogs.** `ContentDialog` already exposes `PrimaryButtonAppearance` /
`CloseButtonAppearance`; `DialogService` sets those (Danger for delete) instead of
retemplating the dialog.

## Consequences

- New screens (M5 onwards) use `PageHeaderControl`, `OffsetShadowCard` and the
  styles in `Folio.TextStyles.xaml` / `Folio.ButtonStyles.xaml` instead of local
  font sizes and colors.
- Any XAML still using an old "Bosque" key fails at runtime with a missing
  resource, not at compile time (`DynamicResource`). All existing views were
  migrated in this milestone.
- Letter-spacing is approximate; if it ever matters, it needs a custom text control,
  not a style.
- Opening an account's detail now runs three extra aggregate queries (totals, per
  month, recent entries). They are single `GROUP BY` / `TOP N` queries with no N+1,
  and fine at SME scale.
- `FolioRowSelectedBrush` in dark mode (`#2A3129`) was estimated, not taken from the
  mockup, and needs a visual check.
- The six-month window is calendar months ending at the current month (from
  `IClock`), not fiscal periods. Once M5 adds period closing, "current month" may
  need to mean "current open period".
