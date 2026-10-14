# Folio — Product & Technical Specification

**Folio** by **Folivora Software** is a double-entry accounting desktop application for small and medium-sized businesses (pymes) in Costa Rica.

Project goals:
1. Professional, portfolio-quality code that a reviewer can read, build and run.
2. **Clone → open `Folio.sln` → F5 → it runs.** No database server, no setup, no secrets. The database is created automatically on first launch and filled with realistic demo data.
3. Every accounting rule enforced in the domain and covered by tests.

---

## 1. Users and roles

| Role | Can do |
|---|---|
| **Administrador** | Everything, including users, company settings, closing periods and restoring backups |
| **Contador** | Chart of accounts, journal entries, exchange rates, reports, closing periods |
| **Consulta** | Read-only access to entries, reports and the dashboard |

Demo credentials (seeded, documented in README): `admin` / `Folio2026!`, `contador` / `Folio2026!`, `consulta` / `Folio2026!`.

---

## 2. Functional requirements

### 2.1 Authentication and users
- Login screen. Passwords hashed with PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`, SHA-256, ≥100,000 iterations, per-user salt). Never store or log plain-text passwords.
- Lock the account for 5 minutes after 5 failed attempts.
- Admin can create, edit, deactivate users and reset passwords. Users can change their own password.
- The current user is available application-wide (`ICurrentUserService`) and is used by the audit log.

### 2.2 Company settings
- Company name, cédula jurídica, address, phone, email, logo (optional, used in PDF reports).
- Functional currency: CRC (₡). Secondary currency: USD ($).
- Fiscal year: January 1 – December 31 (configurable start month).

### 2.3 Chart of accounts (Catálogo de cuentas)
- Hierarchical tree with dotted codes: `1`, `1.1`, `1.1.01`, …
- Each account has: code (unique), name, type, normal balance, parent, `IsPostable`, `IsActive`, optional description.
- Types: `Asset`, `Liability`, `Equity`, `Revenue`, `Cost`, `Expense`. Normal balance is derived from the type (Asset/Cost/Expense → Debit; Liability/Equity/Revenue → Credit) but can be overridden for contra-accounts (e.g. *Depreciación acumulada*).
- Only **postable (leaf) accounts** accept journal lines. An account with children cannot be postable.
- An account with movements cannot be deleted, only deactivated. Its type cannot change once it has movements.
- Seed a standard Costa Rican SME chart, at minimum:
  - **1 Activo** → 1.1 Activo corriente (Caja, Bancos colones, Bancos dólares, Cuentas por cobrar, Inventario, IVA acreditable) · 1.2 Activo no corriente (Mobiliario y equipo, Equipo de cómputo, Vehículos, Depreciación acumulada)
  - **2 Pasivo** → 2.1 Pasivo corriente (Cuentas por pagar, IVA por pagar, Cargas sociales por pagar, Impuesto sobre la renta por pagar, Salarios por pagar) · 2.2 Pasivo no corriente (Préstamos bancarios a largo plazo)
  - **3 Patrimonio** → Capital social, Utilidades acumuladas, Utilidad o pérdida del período
  - **4 Ingresos** → Ventas, Servicios, Diferencial cambiario ganado, Otros ingresos
  - **5 Costos** → Costo de ventas
  - **6 Gastos** → Salarios, Cargas sociales, Alquiler, Servicios públicos, Telecomunicaciones, Depreciación, Gastos bancarios, Diferencial cambiario perdido, Intereses

### 2.4 Journal entries (Asientos contables)
- Header: sequential number (per fiscal year, assigned when posted), date, description, reference (invoice number, etc.), status, created by, posted by.
- Lines: account, description, debit, credit, currency, exchange rate, amount in original currency.
- Status lifecycle: `Draft` → `Posted` → (`Reversed`). Drafts can be edited or deleted. **Posted entries are immutable.** Corrections are made with a reversal entry that mirrors the original and references it.
- Validation rules (enforced in the domain, re-checked in the application layer):
  - At least 2 lines.
  - Each line has either a debit or a credit, never both, never zero, never negative.
  - Total debits = total credits (in functional currency, after rounding).
  - Every account is postable and active.
  - The entry date falls in an **open** period.
- Multi-currency: lines in USD store the original amount and the exchange rate; the functional (CRC) amount is `original × rate`, rounded to 2 decimals. The rate defaults to the stored rate for that date.
- **IVA helper (13%)**: when entering a sale or purchase, the user can enable "Calcular IVA" and the editor adds the IVA line automatically (IVA por pagar on sales, IVA acreditable on purchases). The rate is a setting, defaulting to 13%.
- **Entry templates**: save a frequent entry (rent, payroll) as a template and create new drafts from it.
- Search and filter by date range, number, status, account, text and amount.

### 2.5 Accounting periods
- Monthly periods generated per fiscal year. States: `Open`, `Closed`.
- Closing a period blocks any posting dated inside it. Reopening requires Administrador and is recorded in the audit log.
- **Year-end close**: generates the closing entry that zeroes Revenue, Cost and Expense accounts against *Utilidad o pérdida del período* and transfers the result to *Utilidades acumuladas*. It must be idempotent and refuse to run if any period of the year is still open.

### 2.6 Exchange rates
- Table of daily USD→CRC rates (buy/sell), entered manually or imported from CSV.
- The reference used for entries is the sell rate, configurable.
- Seed realistic demo rates for the demo data range.

### 2.7 Reports
All reports filter by date range (or cut-off date), show the company header, and export to **PDF** (QuestPDF) and **Excel** (ClosedXML).
1. **Libro diario**: posted entries in chronological order with their lines.
2. **Libro mayor**: per account, opening balance, movements and running balance.
3. **Balance de comprobación**: per account, debits, credits and balance; totals must match.
4. **Estado de situación financiera (Balance general)**: Assets = Liabilities + Equity, grouped by hierarchy, with the current period result included in equity.
5. **Estado de resultados**: Revenue − Costs = Gross profit; − Expenses = Net result. Optional comparison with the previous period.

### 2.8 Dashboard
- KPI cards: cash and banks balance, revenue this month, expenses this month, net result year-to-date, current ratio (current assets / current liabilities).
- Charts (LiveCharts2): revenue vs expenses over the last 12 months (bars); expenses by account (donut); cash balance trend (line).
- Recent entries list with quick navigation.

### 2.9 Audit log (Bitácora)
- Every create, update, delete, post, reverse, close and reopen is recorded automatically: timestamp (UTC), user, entity, entity id, action, and before/after values as JSON.
- Implemented with an EF Core `SaveChangesInterceptor`, so no feature has to remember to log.
- Read-only screen with filters (user, entity, action, date). Only Administrador can see it.

### 2.10 Backup and restore
- Create a backup copy of the SQLite database (using the SQLite online backup API, safe while the app is open) to a user-chosen location.
- Restore from a backup (Administrador only, confirmation required, the app restarts afterwards).

---

## 3. Non-functional requirements

- **Money**: always `decimal`, never `double`/`float`. Round to 2 decimals with `MidpointRounding.AwayFromZero`. Use a `Money` value object (amount + currency) in the domain.
- **Dates**: `DateOnly` for accounting dates; `DateTime` UTC for timestamps.
- **Language**: code, identifiers, comments, commits and docs in **English**. The **UI is in Spanish** (Costa Rica), with culture `es-CR` for number, date and currency formatting (₡1 234 567,89). Keep UI strings in `.resx` resources, not hard-coded in XAML.
- **Async**: all I/O is async, the UI thread never blocks.
- **Errors**: global exception handling that logs and shows a friendly message. Validation errors are shown next to the field.
- **Logging**: Serilog to a rolling file in `%LOCALAPPDATA%\Folivora\Folio\logs`.
- **Data location**: database at `%LOCALAPPDATA%\Folivora\Folio\folio.db`, created and migrated automatically on startup.
- **UX**: modern Fluent look (WPF-UI), light and dark theme, full keyboard navigation in the entry editor (Tab/Enter moves through the grid, Ctrl+S saves, Ctrl+Enter posts), confirmation before destructive actions.
- **Code quality**: nullable reference types enabled, warnings as errors, .NET analyzers on, `.editorconfig` enforced. Zero warnings in the build.

---

## 4. Architecture

Clean Architecture, one solution (`Folio.sln`), dependencies point inward only.

```
Folio.sln
├── src/
│   ├── Folio.Domain/          Entities, value objects (Money, AccountCode), enums,
│   │                          domain rules and exceptions. No external dependencies.
│   ├── Folio.Application/     Use cases (services), DTOs, interfaces (repositories,
│   │                          exporters, clock, current user), FluentValidation validators.
│   ├── Folio.Infrastructure/  EF Core + SQLite, configurations, migrations, seed data,
│   │                          repositories, audit interceptor, password hasher,
│   │                          PDF (QuestPDF) and Excel (ClosedXML) exporters, backup.
│   └── Folio.Desktop/         WPF app: Views, ViewModels (CommunityToolkit.Mvvm),
│                              navigation, dialogs, converters, resources, DI composition root.
├── tests/
│   ├── Folio.Domain.Tests/
│   ├── Folio.Application.Tests/
│   └── Folio.Infrastructure.Tests/   (SQLite in-memory)
└── docs/
    ├── SPEC.md
    ├── PROGRESS.md
    └── adr/                   Architecture Decision Records
```

- **Composition root**: `Microsoft.Extensions.Hosting` Generic Host in `App.xaml.cs`, with DI, configuration (`appsettings.json`) and logging.
- **MVVM**: no business logic in code-behind. ViewModels call Application services only, never `DbContext` directly.
- **Navigation**: a navigation service that resolves ViewModels from DI; views are mapped via `DataTemplate`s.

### Packages (use latest stable versions compatible with the target framework)
| Purpose | Package |
|---|---|
| MVVM | CommunityToolkit.Mvvm |
| UI theme | WPF-UI (Wpf.Ui) |
| Charts | LiveChartsCore.SkiaSharpView.WPF |
| ORM | Microsoft.EntityFrameworkCore.Sqlite, .Design |
| Hosting/DI | Microsoft.Extensions.Hosting |
| Validation | FluentValidation |
| Logging | Serilog.Extensions.Hosting, Serilog.Sinks.File |
| PDF | QuestPDF (Community license) |
| Excel | ClosedXML |
| Tests | xUnit, FluentAssertions, NSubstitute |

Any package not listed here needs to be justified in an ADR.

### Target framework
Use the latest **LTS** .NET SDK installed on the machine (check `dotnet --list-sdks`): prefer .NET 10, fall back to .NET 8. Desktop project targets `netX.0-windows`; Domain, Application and their tests target `netX.0` so they can run on any OS.

---

## 5. Data model (main tables)

| Table | Key fields |
|---|---|
| `Users` | Id, Username (unique), FullName, Email, PasswordHash, Salt, Role, IsActive, FailedAttempts, LockedUntil, CreatedAt |
| `CompanySettings` | Id, Name, LegalId, Address, Phone, Email, Logo, FunctionalCurrency, FiscalYearStartMonth, VatRate |
| `Accounts` | Id, Code (unique), Name, Type, NormalBalance, ParentId, IsPostable, IsActive, Description |
| `FiscalYears` | Id, Year, StartDate, EndDate, IsClosed |
| `Periods` | Id, FiscalYearId, Year, Month, StartDate, EndDate, Status, ClosedAt, ClosedBy |
| `JournalEntries` | Id, Number, Date, Description, Reference, Status, ReversesEntryId, PeriodId, CreatedBy, CreatedAt, PostedBy, PostedAt |
| `JournalLines` | Id, JournalEntryId, LineNumber, AccountId, Description, Debit, Credit, Currency, ExchangeRate, OriginalAmount |
| `ExchangeRates` | Id, Date (unique), Buy, Sell, Source |
| `EntryTemplates` / `EntryTemplateLines` | Same shape as entries, without amounts required |
| `AuditLogs` | Id, Timestamp, UserId, Entity, EntityId, Action, OldValues (JSON), NewValues (JSON) |

`Debit`/`Credit` stored in functional currency. Indexes on `JournalEntries(Date)`, `JournalLines(AccountId)`, `Accounts(Code)`, `AuditLogs(Timestamp)`.

### Demo data
Seed one demo company (*Sleepy Sloth Café & Bakery S.A.*, a café and bakery in a Costa Rican tourist area that sells in both colones and dollars), the full chart of accounts, and 12 months of realistic posted entries: opening capital, a bank loan, rent, payroll for baristas and bakers with cargas sociales, sales of coffee and baked goods with IVA (some to tourists in USD), purchases of supplies (coffee beans, flour, dairy) with IVA, utilities, depreciation, and a few other USD transactions. Also seed exchange rates for that range, and closed periods for every month except the current one. Seeding runs only when the database is empty.

---

## 6. Milestones

Work one milestone at a time. Each one ends with: build with zero warnings, all tests green, `docs/PROGRESS.md` updated, and a summary for review.

| # | Milestone | Definition of done |
|---|---|---|
| **M0** | Repository foundation | Solution and all projects created with the structure above; `Directory.Build.props` (nullable, warnings as errors, analyzers, common versions); `.editorconfig`; GitHub Actions workflow that builds and tests on `windows-latest` on every push and PR; README skeleton; `docs/PROGRESS.md`; ADR-0001 (architecture). The WPF app opens an empty main window. |
| **M1** | Domain model | Entities, `Money`, `AccountCode`, enums, all journal entry rules (balance, posting, reversal, postable accounts, open period) with thorough unit tests, including edge cases (rounding, multi-currency). |
| **M2** | Persistence and seed | `FolioDbContext`, entity configurations, initial migration, automatic migrate on startup, repositories, audit interceptor, full demo seed. Integration tests on SQLite in-memory. |
| **M3** | Desktop shell | Generic Host + DI, Serilog, global error handling, WPF-UI theme (light/dark), main window with side navigation, login screen, current user service, `es-CR` culture and resource files. |
| **M4** | Chart of accounts | Tree view with search, create/edit/deactivate dialogs with validation, all rules from 2.3 enforced. |
| **M5** | Journal entries | Entry list with filters; entry editor with editable lines grid, live debit/credit totals and difference indicator, account picker with search, IVA helper, templates, keyboard shortcuts; post, delete draft, reverse. |
| **M6** | Periods and exchange rates | Periods screen (close/reopen), year-end close, exchange rates screen with CSV import. |
| **M7** | Reports | All five reports on screen with PDF and Excel export. |
| **M8** | Dashboard | KPI cards and the three charts from 2.8 with real data. |
| **M9** | Administration | Users and roles, permission checks across screens, audit log viewer, company settings, backup and restore. |
| **M10** | Polish and release | UX pass, empty states, loading indicators; README with screenshots, GIF demo, architecture diagram, features, how to run, tech decisions; release workflow that publishes a self-contained single-file `Folio.exe` as a ZIP to GitHub Releases when a `v*` tag is pushed. |
