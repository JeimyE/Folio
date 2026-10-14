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
