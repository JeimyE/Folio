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
computed from `IClock.UtcNow`, not a fixed January–December calendar year:
`windowStart = firstDayOf(currentMonth) - 11 months`, `windowEnd = lastDayOf(currentMonth)`.
A `FiscalYear` and its 12 `Period`s are generated over exactly this span. Every period
except the last (current) one is closed; entries and exchange rates are seeded up to
`IClock`'s "today," never into the future.

This way, cloning and running Folio for the first time always shows a full year of
history leading up to "now," regardless of the calendar date — the portfolio demo looks
equally alive whether a reviewer opens it in January or in October.

This only affects the **seed data's** window. The general fiscal-year feature
(`CompanySettings.FiscalYearStartMonth`, `FiscalYear`/`Period` entities themselves) is
unchanged and still supports a real calendar-aligned fiscal year for actual use.

## Consequences

- `DemoEntriesSeeder` and `FolioSeeder` take `IClock` as a parameter and never call
  `DateTime.UtcNow` directly, so `FolioSeederTests` can inject a fixed clock and assert
  exact window boundaries deterministically.
- The seeded `FiscalYear.Year` label is just `today.Year` — a cosmetic simplification,
  since the trailing window can span two calendar years (e.g. July 2025–June 2026). The
  `Period.Year`/`Month` values are the real, correct calendar values for each month.
- If SPEC's fiscal-year-always-calendar-aligned assumption matters elsewhere later,
  this demo-only window should not be confused with it — it is explicitly a seeding
  convenience, documented here and in `docs/PROGRESS.md`.
