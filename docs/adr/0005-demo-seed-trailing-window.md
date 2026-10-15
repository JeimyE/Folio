# ADR-0005: Demo seed uses a trailing 12-month window, not the calendar year

## Context

SPEC §5 asks the demo seed to have "12 months of realistic posted entries ... and
closed periods for every month except the current one." Read literally against a
calendar fiscal year (SPEC §2.2: January 1 – December 31), those two requirements
conflict depending on when the app is first run: if today is March, a calendar-year
fiscal year only has two closed months (Jan, Feb) and one open (Mar) behind it — nowhere
near "12 months of entries." Only someone running the app for the first time in
December would see the full 12 months SPEC describes.

## Decision

The demo seed's window is **the trailing 12 months ending in the current month**,
computed from today's date **in Costa Rica** (`IClock.TodayInCostaRica()`, see the
update below), not a fixed January–December calendar year:
`windowStart = firstDayOf(currentMonth) - 11 months`, `windowEnd = lastDayOf(currentMonth)`.
A `FiscalYear` and its 12 `Period`s are generated over exactly this span. Every period
except the last (current) one is closed; entries and exchange rates are seeded up to
that local "today," never into the future.

This way, cloning and running Folio for the first time always shows a full year of
history leading up to "now," regardless of the calendar date — the portfolio demo looks
equally alive whether a reviewer opens it in January or in October.

This only affects the **seed data's** window. The general fiscal-year feature
(`CompanySettings.FiscalYearStartMonth`, `FiscalYear`/`Period` entities themselves) is
unchanged and still supports a real calendar-aligned fiscal year for actual use.

## Consequences

- `FolioSeeder` takes `IClock` as a parameter (and passes the resulting local date on
  to `DemoEntriesSeeder` and `ExchangeRateSeedData`) and never calls
  `DateTime.UtcNow` directly, so `FolioSeederTests` can inject a fixed clock and assert
  exact window boundaries deterministically.
- The seeded `FiscalYear.Year` label is just `today.Year` — a cosmetic simplification,
  since the trailing window can span two calendar years (e.g. July 2025–June 2026). The
  `Period.Year`/`Month` values are the real, correct calendar values for each month.
- If SPEC's fiscal-year-always-calendar-aligned assumption matters elsewhere later,
  this demo-only window should not be confused with it — it is explicitly a seeding
  convenience, documented here and in `docs/PROGRESS.md`.

## Update (M4.5): "today" is the Costa Rica date, not the UTC date

The window was originally computed as `DateOnly.FromDateTime(IClock.UtcNow)`. Costa
Rica is UTC−6, so from 6 p.m. local time the UTC date is already tomorrow: a database
created at 22:47 on October 14 had its last entry dated October 15, in the future for
the user. At the end of a month it was worse: the whole window shifted one month
ahead, with an extra "current" period that hadn't started locally.

**Decision:** accounting dates (`DateOnly`) are always the calendar date in
`America/Costa_Rica`. There is exactly one place that computes it:
`Folio.Domain.Common.ClockExtensions.TodayInCostaRica(this IClock)`, which converts
`IClock.UtcNow` to that time zone. The seed window and the chart-of-accounts
six-month window both use it. Timestamps (`*AtUtc`, audit, login lockout) stay in UTC.

It is an extension method, not a new `IClock` member, on purpose: a member would let
every test substitute return its own unrelated date (or `default(DateOnly)` if
forgotten). An extension always derives the date from `UtcNow`, so the conversion
itself is what the tests exercise.

**Consequences:**
- `ClockExtensionsTests` pins the conversion (late evening, local midnight, month and
  year boundaries). `FolioSeederTests` and `ChartOfAccountsServiceTests` each have a
  case at 03:00 UTC on July 1 (still June 30 locally) and assert the window ends in June.
- The time zone is fixed to Costa Rica, matching SPEC's scope. If Folio ever serves
  companies elsewhere, the zone becomes a `CompanySettings` field read in this same
  single place.
- Databases seeded before this fix may contain an entry dated one day ahead. Delete
  `folio.db` to reseed.

