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
