# Progress

## M0 — Repository foundation (done)

**Built:**
- Solution `Folio.sln` with the Clean Architecture layout from SPEC §4:
  - `src/Folio.Domain`, `src/Folio.Application`, `src/Folio.Infrastructure`, `src/Folio.Desktop`
  - `tests/Folio.Domain.Tests`, `tests/Folio.Application.Tests`, `tests/Folio.Infrastructure.Tests`
- Target framework: **.NET 10** (`net10.0` for Domain/Application/tests, `net10.0-windows` for Desktop) — the SDK installed is 10.0.400, and .NET 10 is the current LTS.
- `Directory.Build.props`: nullable reference types, warnings-as-errors, .NET analyzers (`AnalysisMode=Recommended`).
- `Directory.Packages.props`: NuGet Central Package Management, one version per package for the whole solution.
- `.editorconfig`: formatting, file-scoped namespaces, `_camelCase` private fields, nullable diagnostics promoted to errors.
- `src/Folio.Desktop`: minimal WPF app (`App.xaml`, `MainWindow.xaml`) that opens an empty window. No DI/Hosting yet — that is M3.
- `.github/workflows/build.yml`: builds and tests on `windows-latest` on every push/PR to `main`.
- `docs/adr/0001-architecture.md`.

**Known limitations / things deferred on purpose:**
- No entities, DbContext, or migrations yet (M1/M2).
- Test projects have no tests yet — there is no domain logic to test. `dotnet test` reports "no tests found", not failures.
- `LiveChartsCore.SkiaSharpView.WPF` pulls transitive packages (`OpenTK`, `SkiaSharp.Views.WPF`) that only publish .NET Framework TFM assets. NuGet's compatible fallback works fine at runtime on Windows but raises `NU1701`; this is suppressed in `Folio.Desktop.csproj` only (not solution-wide). See ADR-0001.
- `dotnet new sln` in the .NET 10 SDK defaults to the new `.slnx` format; `Folio.sln` was generated explicitly with `--format sln` to match the classic format referenced in `CLAUDE.md`/SPEC.

**Verification:**
- `dotnet build Folio.sln -warnaserror` → 0 warnings, 0 errors.
- `dotnet test Folio.sln` → all green (no tests yet to fail).
- App launches via `dotnet run --project src/Folio.Desktop` and shows an empty window titled "Folio".

## M1 — Domain model (done)

**Built (`src/Folio.Domain/`):**
- Value objects: `Money` (decimal + `Currency`, rounds `AwayFromZero` on construction),
  `AccountCode` (validated dotted hierarchy, e.g. `1.1.01`, with `Parent()`/`IsChildOf`).
- Enums: `AccountType`, `NormalBalance`, `Currency`, `JournalEntryStatus`, `PeriodStatus`, `UserRole`.
- `Common/IClock.cs`: the time abstraction. Entities themselves take plain `DateTime`
  parameters, not `IClock` — only Application/Infrastructure (M2+) will hold a clock and
  resolve `UtcNow` once per operation, passing the result into entity methods. This keeps
  every entity a dependency-free POCO and keeps domain tests free of fake clocks.
- Entities: `Account` (with `HasChildren`/`AddChild()`/`MarkAsPostable()` enforcing "an
  account with children cannot be postable"), `FiscalYear` (`GeneratePeriods()`, `Close()`),
  `Period` (`Close`/`Reopen`, `IsOpenFor(date)`), `ExchangeRate`, `User` (lockout after 5
  failed attempts for 5 minutes, never stores a plain-text password), `CompanySettings`,
  `EntryTemplate`/`EntryTemplateLine`, and the core pair: `JournalEntry`/`JournalLine`.
- 12 `DomainException` subclasses for business-rule violations (unbalanced entry, posting
  to a closed period, non-postable/inactive account, reversing a draft or a reversal,
  etc.), separate from the plain `ArgumentException`/`ArgumentOutOfRangeException` used
  for structural parameter guards (blank name, zero amount).

**Key design decisions:**
- **Draft vs. Post()-time validation.** Line-level invariants (exactly one of debit/credit,
  never zero or negative) are enforced unconditionally by `JournalLine`'s factory methods
  (`CreateDebit`/`CreateCredit`/`CreateForeignDebit`/`CreateForeignCredit`) — impossible to
  violate by construction. Entry-level invariants (≥2 lines, debit=credit, every account
  postable/active, date in an open period) are checked **only** inside `JournalEntry.Post()`.
  `AddLine`/`UpdateLine`/`RemoveLine` work freely on a `Draft`, which can be incomplete or
  unbalanced while being edited.
- **`Post()` takes the accounts it needs, not a repository.** Signature:
  `Post(int number, Period period, IReadOnlyDictionary<Guid, Account> accountsById, Guid postedByUserId, DateTime postedAtUtc)`.
  The Domain has zero dependencies, so it cannot look up accounts or periods itself; the
  caller (Application, via a repository, from M2 onward) resolves them first.
- **Rounding in multi-currency entries**: see ADR-0002. `Post()` requires the CRC total to
  balance exactly — it does not tolerate or auto-correct rounding gaps from independently
  converting split lines; that is the caller's responsibility (a future M5 helper).
- **`Reverse()` reuses `Post()`.** It builds a `Draft` mirror entry (lines with debit/credit
  swapped via `JournalLine.Mirror()`) and posts it through the same `Post()` method, so the
  balance/account/period checks are not duplicated. A reversal that is itself a reversal
  (`ReversesEntryId != null`) cannot be reversed again; neither can a draft or an
  already-reversed entry.
- **Authorization is not a Domain concern.** `Period.Reopen()` does not check the caller's
  role; SPEC §2.5 says reopening requires Administrador, but that check belongs to the
  Application layer (`ICurrentUserService`), which decides whether to call the method at all.
- **Password hashing stays out of Domain.** `User` only ever holds a `PasswordHash`/`Salt`
  pair; the PBKDF2 algorithm itself is Infrastructure (M2), behind an `IPasswordHasher`
  interface to be defined in Application.

**Known limitations / deferred on purpose:**
- `AuditLog` has no Domain entity: SPEC §2.9 implements it via an EF Core
  `SaveChangesInterceptor`, which is purely an Infrastructure mechanism (M2).
- Chart-of-accounts-wide consistency (unique codes, full hierarchy validation) needs a
  repository and is Application/Infrastructure work for M2/M4; `Account.AddChild()` only
  protects the postable/has-children invariant for the one relationship it's told about.
- `tests/Directory.Build.props` suppresses analyzer rule `CA1707` (no underscores in
  identifiers) for all test projects — it conflicts with the mandated
  `Method_Scenario_ExpectedResult` test naming convention.

**Verification:**
- `dotnet build Folio.sln -warnaserror` → 0 warnings, 0 errors.
- `dotnet test Folio.sln` → 106/106 Domain tests pass (Application/Infrastructure test
  projects still have nothing to test until M2).

## M2 — Persistence and seed (done)

**Built:**
- `src/Folio.Application/Abstractions/`: `IPasswordHasher`, `ICurrentUserService`,
  `IUnitOfWork`, and one repository interface per aggregate (`IAccountRepository`,
  `IFiscalYearRepository`, `IPeriodRepository`, `IJournalEntryRepository`,
  `IExchangeRateRepository`, `IUserRepository`, `ICompanySettingsRepository`,
  `IEntryTemplateRepository`).
- `src/Folio.Infrastructure/Persistence/`: `FolioDbContext`, one `IEntityTypeConfiguration<T>`
  per entity, `DesignTimeDbContextFactory` (for `dotnet ef`), `FolioDbInitializer`
  (migrate + seed-if-empty), repository implementations, and the initial migration
  (`dotnet ef migrations add InitialCreate`, regenerated three times as mapping issues
  surfaced — see ADR-0006).
- `src/Folio.Infrastructure/Auditing/`: `AuditSaveChangesInterceptor` (redaction, smart
  action labels — ADR-0004), `SystemCurrentUserService`.
- `src/Folio.Infrastructure/Security/Pbkdf2PasswordHasher.cs`, `Common/SystemClock.cs`.
- `src/Folio.Infrastructure/Seed/`: `ChartOfAccountsSeedData`, `ExchangeRateSeedData`,
  `DemoEntriesSeeder`, `FolioSeeder` — the Sleepy Sloth Café & Bakery S.A. demo data.
- `src/Folio.Desktop/App.xaml.cs`: minimal async startup (migrate + seed before showing
  `MainWindow`) — temporary until M3's Generic Host.
- `tests/Folio.Infrastructure.Tests/`: `SqliteInMemoryContextFactory` (real migrations,
  not `EnsureCreated`), `FolioDbContextTests`, `AuditSaveChangesInterceptorTests`,
  `FolioSeederTests` — 15 tests, all green.
- ADR-0003 (money as integer cents), ADR-0004 (audit interceptor design), ADR-0005
  (trailing 12-month seed window), ADR-0006 (explicit EF property mapping lesson).

**Key design decisions:**
- **EF mapping without touching Domain's public API.** Every entity except
  `JournalLine`/`EntryTemplateLine` got a `private` parameterless constructor reserved
  for EF (a comment marks it); EF's default `PropertyAccessMode.PreferFieldDuringConstruction`
  then sets every backing field directly on load, bypassing business-constructor
  validation and `Guid.NewGuid()` calls entirely. `JournalLine`'s existing private
  constructor already matched this shape (pure field assignment, parameter names equal
  to property names), so it needed no change beyond gaining its own `Id`. See ADR-0006
  for the harder lesson: every scalar property still needs an explicit
  `builder.Property(...)` call, or query translation fails at runtime despite compiling
  and migrating successfully.
- **Money as integer cents, not decimal text — ADR-0003.** `JournalLine.Debit/Credit/
  OriginalAmount` use `MoneyToCentsConverter` so SQLite can `SUM()`/`ORDER BY` them
  correctly (needed by M7's reports); exchange rates and VAT rate keep the provider's
  default decimal-as-text storage, since they're never aggregated and need more than 2
  decimal places.
- **Automatic migration on startup.** `FolioDbInitializer.InitializeAsync(connectionString)`
  calls `Database.MigrateAsync()` then seeds only if `Accounts` is empty.
  `App.xaml.cs`'s `OnStartup` awaits it before ever constructing `MainWindow` — no
  `.Result`/`.Wait()`. This required explicitly setting `ShutdownMode =
  ShutdownMode.OnExplicitShutdown` during the await: with no `StartupUri` and zero
  windows open yet, WPF's default `OnLastWindowClose` mode shut the app down before the
  async initialization ever finished. Switched back to `OnLastWindowClose` right after
  `MainWindow.Show()`.
- **Demo seed uses a trailing 12-month window — ADR-0005.** Computed from `IClock`, not
  a fixed calendar year, so "12 months of history, current month open" is true no
  matter when the app is first run.
- **Seeding never floods the audit log — ADR-0004.** `FolioDbInitializer` builds its
  bootstrap `DbContext` without the audit interceptor at all, and writes exactly one
  `AuditLogEntry` (`Action = "Seed"`) itself, summarizing counts.
- **Audit interceptor redacts by property name.** `PasswordHash`/`Salt` never appear in
  `OldValues`/`NewValues` JSON, on any entity, even when other fields on the same
  change are legitimately logged. Status-transition detection (Draft→Posted,
  Posted→Reversed, Open→Closed, Closed→Open) labels those rows `Post`/`Reverse`/
  `Close`/`Reopen` instead of a generic `Update`, matching SPEC §2.9's vocabulary.
- **Demo seed entries all go through the real `JournalEntry.Post()`** — the seeder
  builds Drafts and posts them exactly like the real app would, so a bad amount in the
  demo data fails loudly at first launch instead of silently corrupting the books.

**Known limitations / deferred on purpose:**
- `JournalLine`/`EntryTemplateLine` gained a `Guid Id` purely for EF's benefit (SQLite
  cannot auto-generate the shadow key an owned collection would otherwise need) — see
  ADR-0006. The Domain-level rule that lines have no independent identity is unchanged;
  nothing outside persistence ever uses this id.
- Repository implementations are intentionally minimal (CRUD + the few lookups the
  seeder and tests needed); richer query methods arrive with their use cases in M4+.
- `App.xaml.cs`'s startup is a stop-gap composition root (no DI, no Serilog, a bare
  `MessageBox` on failure) — M3 replaces it with the Generic Host.
- `dotnet-tools.json` (local tool manifest) pins `dotnet-ef` so `dotnet ef migrations
  add` works without a global tool install.

**Verification:**
- `dotnet build Folio.sln -warnaserror` → 0 warnings, 0 errors.
- `dotnet test Folio.sln` → 106 Domain + 15 Infrastructure tests pass (Application
  still has nothing to test).
- App launched via `dotnet run --project src/Folio.Desktop` against a clean
  `%LOCALAPPDATA%` end-to-end: migrated, seeded (confirmed via the SQLite WAL file
  size — EF Core's SQLite provider defaults to WAL journal mode, so the main `.db`
  file alone can look small until checkpointed; reading the database back, as the
  integration tests do, is unaffected), and stayed open without the premature-shutdown
  bug above.

## M3 — Desktop shell (done)

**Built:**
- `src/Folio.Application/Authentication/`: `IAuthenticationService`/`AuthenticationService`
  (SPEC §2.1 login: lockout checked before the password, same `InvalidCredentials`
  reason whether the username doesn't exist or the password is wrong, never returns
  the full `User` entity), `AuthenticationResult`. `Abstractions/ICurrentUserSession.cs`
  (write side of the session; `ICurrentUserService` stays read-only).
- `src/Folio.Infrastructure/`: `Common/FolioPaths.cs` (centralizes every
  `%LOCALAPPDATA%\Folivora\Folio\` path), `Auditing/CurrentUserSession.cs`,
  `Hosting/DatabaseInitializerHostedService.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`
  (`AddFolioInfrastructure`). Every repository switched from a direct `FolioDbContext`
  to `IDbContextFactory<FolioDbContext>` (ADR-0007); `IUserRepository` gained
  `UpdateAsync`; the now-unused `IUnitOfWork` was removed.
- `src/Folio.Desktop/`: Generic Host composition root (`App.xaml.cs`), bootstrap +
  Host-managed Serilog, `ErrorHandling/GlobalExceptionHandler.cs`, `SplashWindow`,
  `Theming/` (`IThemeService`/`ThemeService`, persisted preference), `Navigation/`
  (`INavigationService`/`NavigationService`, ViewModel-first), `Messages/`
  (`UserSignedInMessage`, `UserSignedOutMessage`), `ViewModels/` (`ShellCoordinatorViewModel`,
  `LoginViewModel`, `ShellViewModel`, `WelcomeViewModel`), matching `Views/`,
  `Resources/Strings.resx` (+ a hand-written accessor), `Resources/Theme/Folio.Light.xaml`
  / `Folio.Dark.xaml` (the "Bosque" palette), `MainWindow` (a `FluentWindow`).
- `tests/Folio.Application.Tests/Authentication/AuthenticationServiceTests.cs`: 7 tests
  (missing user, wrong password increments attempts, 5th attempt locks, already-locked
  skips the password check entirely, inactive account, successful login resets
  attempts and signs in, missing-user vs wrong-password return the same reason).
- `tests/Folio.Desktop.Tests/` (new project, targets `net10.0-windows` since it
  references `Folio.Desktop`): 6 `LoginViewModel` tests (error messages per failure
  reason, `UserSignedInMessage` sent on success — captured via a real
  `WeakReferenceMessenger`, not a mock, since `IMessenger.Send<T>` resolves through a
  generic extension method that's awkward to verify with NSubstitute — and the
  password field cleared after every attempt, success or failure).
- `Folio.sln` reordered: `Folio.Desktop` is now the first project, so Visual Studio
  picks it as the default startup project on a fresh clone.
- ADR-0007 (Generic Host + `IDbContextFactory`), ADR-0008 (theme system).

**Key design decisions:**
- **Splash → Login → Shell, one window.** `MainWindow` never changes; its content
  switches between `LoginViewModel` and `ShellViewModel` through
  `ShellCoordinatorViewModel`, which listens for `UserSignedInMessage`/
  `UserSignedOutMessage` on `CommunityToolkit.Mvvm`'s `IMessenger` — Login and Shell
  never reference each other directly. A separate `SplashWindow` (plain colors, no
  theme dependency) covers the Host's startup/migration/seed, closed right before
  `MainWindow` is shown.
- **`ShutdownMode.OnExplicitShutdown` during startup.** With no `StartupUri`, WPF
  briefly has zero windows open while the Host starts; its default `OnLastWindowClose`
  mode would (and, once, did) shut the app down before anything ever appeared. Fixed by
  switching to `OnExplicitShutdown` until `MainWindow.Show()`, then back to
  `OnLastWindowClose` so closing the window still exits normally.
- **Login never stores the password longer than one attempt.** `LoginViewModel` clears
  `Password` in a `finally` block — success or failure — and nothing in
  `AuthenticationService` or `LoginViewModel` ever logs it. Credential verification is
  always via `IPasswordHasher`; the lockout check happens *before* that call, so a
  locked account never even reaches password comparison (and a test asserts
  `IPasswordHasher.Verify` was never called in that case).
- **Side navigation without WPF-UI's `NavigationView` Frame.** `ShellView` uses a
  plain two-column layout (a styled side panel + a `ContentControl`) rather than
  wiring WPF-UI's own `NavigationView`/Frame navigation, to avoid coupling the
  ViewModel-first ViewModel/DataTemplate pattern (SPEC §4) to a second, competing
  navigation system. `INavigationService` is Folio's own, resolving ViewModels from DI;
  WPF-UI is used for styled controls (`FluentWindow`, `Button`, `PasswordBox`,
  `TitleBar`) but not for page navigation itself.
- **Theme and brand palette change together — ADR-0008.** `IThemeService.ApplyTheme`
  calls WPF-UI's `ApplicationThemeManager.Apply` and swaps the Folio brand
  `ResourceDictionary` in the same method, so they can't drift out of sync. Startup
  theme: saved preference, else the system theme; only an explicit toggle persists.

**Known limitations / deferred on purpose:**
- `UserRepository.UpdateAsync` re-attaches a detached `User` as `Modified`, so the
  audit log records every column, not just the ones that actually changed (ADR-0007).
  `PasswordHash`/`Salt` are still redacted regardless.
- The side navigation has exactly one page (`WelcomeViewModel`, a placeholder greeting)
  — real feature pages start in M4.
- The UI could not be visually inspected pixel-by-pixel in this environment (no
  desktop screenshot tool available here); verification relied on the full automated
  test suite (134 tests), the Serilog log confirming a clean Host startup with no
  errors, and the process staying open and responsive after login/shell/logout code
  paths were exercised by the test suite. Jeimy should give the login screen, the
  theme toggle, and sign-out a manual look before considering M3 fully signed off.
- High-contrast Windows themes fall back to the light palette (ADR-0008) — not
  distinguished from regular light/dark.

**Verification:**
- `dotnet build Folio.sln -warnaserror` → 0 warnings, 0 errors.
- `dotnet test Folio.sln` → 106 Domain + 7 Application + 15 Infrastructure + 6 Desktop
  = 134 tests pass.
- App launched end-to-end against the real seeded database: Generic Host started
  cleanly (Serilog log shows no errors), splash screen closed, and the process stayed
  open and responsive.

## M4 — Chart of accounts (done)

**Built:**
- `src/Folio.Domain/ValueObjects/AccountCode.cs`: now `IComparable<AccountCode>`
  (`<`/`<=`/`>`/`>=` too, for `CA1036`), ordering by numeric segment, not string
  comparison — `1.1.2` sorts before `1.1.10` (ADR-0009).
- `src/Folio.Domain/Entities/Account.cs`: `ChangeType(AccountType, NormalBalance?)` —
  M1 had no way to change an account's type/normal balance after construction.
- `src/Folio.Application/Abstractions/Repositories/IAccountRepository.cs` /
  `src/Folio.Infrastructure/Persistence/Repositories/AccountRepository.cs`: added
  `CodeExistsAsync`, `HasMovementsAsync` (posted lines only), `GetMovementTotalsAsync`
  (one SQL `SUM()` over cents for a set of account ids — ADR-0010), `UpdateAsync`,
  `DeleteAsync`.
- `src/Folio.Application/ChartOfAccounts/`: `AccountTreeItemDto`, `AccountDetailDto`,
  `CreateAccountRequest`/`UpdateAccountRequest`, `ChartOfAccountsResult`,
  `IChartOfAccountsService`/`ChartOfAccountsService` — every chart-of-accounts use
  case (get tree, get detail with balance, create, update, activate/deactivate,
  delete), with every validation rule from SPEC §2.3 enforced here, in Spanish error
  messages the ViewModel only ever displays verbatim.
- `src/Folio.Desktop/ViewModels/`: `AccountTreeNodeViewModel` (wraps a tree DTO with
  `IsExpanded`/`IsSearchMatch`/`IsVisible` for the search feature),
  `AccountFormViewModel` (create/edit form, owns the save call and its error),
  `ChartOfAccountsViewModel` (tree + search + detail + every command). `ShellViewModel`
  gained navigation commands to the welcome page and this new one.
- `src/Folio.Desktop/Dialogs/`: `IDialogService`/`DialogService` — modal account
  form and delete confirmation via WPF-UI's `ContentDialog` (ADR-0011).
- `src/Folio.Desktop/Views/`: `ChartOfAccountsView.xaml` (search box, toolbar, a
  native `TreeView` with a `HierarchicalDataTemplate`, and a detail panel) and
  `AccountFormView.xaml`; two side-navigation buttons added to `ShellView.xaml`
  ("Inicio"/"Catálogo de cuentas"); `MainWindow.xaml` wraps its content in a
  `ui:ContentDialogHost` so dialogs have somewhere to attach to.
- `src/Folio.Desktop/Converters/`: `NullToVisibilityConverter`,
  `AccountTypeDisplayConverter`, `NormalBalanceDisplayConverter` (the enums' own names
  are English identifiers; these render the es-CR label the user sees).
- `tests/Folio.Application.Tests/ChartOfAccounts/ChartOfAccountsServiceTests.cs`: 20
  tests covering every validation path (duplicate code, missing/postable/type-mismatched
  parent, movements blocking type/normal-balance/postable changes, hierarchy rules for
  activate/deactivate, delete blocked by children or movements).
- `tests/Folio.Infrastructure.Tests/ChartOfAccountsBalanceIntegrationTests.cs`: posts
  real journal entries (including an internal transfer between sibling leaves) through
  a real seeded SQLite database and asserts leaf and group balances net correctly via
  `ChartOfAccountsService` end to end — not a mock anywhere in the chain.
- `tests/Folio.Desktop.Tests/ViewModels/ChartOfAccountsViewModelTests.cs`: 14 tests —
  tree loading, search matching by code/name case-insensitively, ancestor-path
  preservation and auto-expand on a descendant match, unrelated branches staying
  hidden, detail loading on selection, command `CanExecute` gating on selection, and
  the add/delete command flows (dialog cancelled vs. confirmed, service failure
  surfaced as `ErrorMessage`).
- ADR-0009 (code defines hierarchy, immutable), ADR-0010 (balances computed on
  demand via SQL aggregation), ADR-0011 (`ContentDialog` manual close after async
  save).

**Key design decisions:**
- **The dotted code is the only parent picker — ADR-0009.** Creating `1.1.07` always
  means "child of `1.1`"; there is no separate parent dropdown that could disagree
  with the typed code. Code and parent are immutable after creation —
  `UpdateAccountRequest` has no `Code` field at all.
- **Balances are never stored — ADR-0010.** Every detail-panel open runs a real SQL
  `SUM()` over the account's (or, for a group, every descendant leaf's) posted
  movements, signed by `NormalBalance`. Nothing to keep in sync, nothing that can
  drift from the ledger.
- **Hierarchy rules for activate/deactivate.** `DeactivateAsync` refuses to deactivate
  an account with any still-active child ("Desactive primero las subcuentas.");
  `ActivateAsync` refuses to activate an account whose parent is inactive. Both are
  enforced in the Application layer (needs the whole tree/parent lookup, which the
  Domain's `Account` alone cannot do), not duplicated in the ViewModel.
- **`ContentDialog` closes itself after validating, not on click — ADR-0011.** WPF-UI's
  `Closing` event has no async deferral; the dialog always cancels its own close on
  the Primary button, awaits the save, and only then closes itself if it succeeded,
  leaving the error visible inline otherwise.
- **Search expands ancestors without marking them as matches.** `AccountTreeNodeViewModel.ApplyFilter`
  recurses depth-first; a node is visible if it matches or any descendant does, but
  `IsSearchMatch` (the highlight) is only ever true for the node whose own code or
  name actually matched — the groups above it light up as "reachable," not as hits.

**Known limitations / deferred on purpose:**
- No autocomplete/suggestion for the next free sibling code when creating an account
  (ADR-0009) — the user types the full code, same as working from a printed chart of
  accounts.
- Renumbering/moving an existing account is out of scope; the only supported path for
  a misclassified account is deactivating it and creating a new one with the correct
  code.
- The account form's client-side validation is deliberately thin (only "name is
  required"); every other rule is server-side in `ChartOfAccountsService` and surfaces
  through the same `ErrorMessage` the dialog shows, to avoid duplicating business
  rules in the ViewModel.
- As with M3, the UI could not be visually inspected pixel-by-pixel in this
  environment — verification relied on the full automated test suite, the Serilog log
  confirming a clean run, and the process launching and staying open and responsive
  with the new "Catálogo de cuentas" screen reachable from the sidebar. Jeimy should
  give the tree, search, and the create/edit/delete dialogs a manual look before
  considering M4 fully signed off.

**Verification:**
- `dotnet build Folio.sln -warnaserror` → 0 warnings, 0 errors.
- `dotnet test Folio.sln` → 112 Domain + 27 Application + 20 Infrastructure + 20
  Desktop = 179 tests pass.
- App launched via `dotnet run --project src/Folio.Desktop`: Generic Host started
  cleanly (no errors in the Serilog log), the window opened, and the process stayed
  open and responsive; closed from this session afterward rather than left running.
