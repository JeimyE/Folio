# ADR-0003: Store money amounts as integer cents, not decimal text

## Context

EF Core's SQLite provider stores `decimal` columns as `TEXT` by default — this is how
`ExchangeRate.Buy`/`Sell` and `CompanySettings.VatRate` are stored, and it preserves
exact decimal precision on round-trip (verified in `FolioDbContextTests`).

But SQLite compares and sorts `TEXT` values as *strings*, not numbers — `"9.00" <
"10.00"` is false lexicographically, and `SUM()` over a `TEXT` column does not work.
M7's reports (Libro mayor running balances, Balance de comprobación totals, Estado de
resultados) all need the database to `SUM()`, `ORDER BY` and compare `JournalLine`
amounts correctly. Storing `Debit`/`Credit`/`OriginalAmount` as `TEXT` would make those
reports either wrong or impossible to write as plain SQL/LINQ aggregates.

Switching those columns to `REAL` (floating point) would fix aggregation but reintroduce
the exact problem CLAUDE.md's "money is always `decimal`, never `double`/`float`" rule
exists to prevent — binary floating point cannot represent most decimal fractions
exactly, so `SUM()` over many rows can drift by fractions of a cent.

## Decision

Money-amount columns — specifically `JournalLine.Debit`, `JournalLine.Credit` and
`JournalLine.OriginalAmount` — are stored as **integer cents** via a dedicated
`MoneyToCentsConverter` (`decimal` ↔ `long`, ×100 on write, ÷100 on read). SQLite's
`INTEGER` type is a native 64-bit integer: `SUM()`, `ORDER BY` and comparisons on it are
both correct and fast, with no floating-point drift possible.

This conversion is exact, never a source of rounding error itself, because every amount
reaching it was already rounded to exactly 2 decimals by the Domain's `Money` value
object (M1) before it ever reaches Infrastructure — multiplying a 2-decimal `decimal` by
100 and truncating to an integer always lands on a whole number.

The conversion is applied **only** to money-amount columns, not blanket-applied to every
`decimal` in the model:
- `ExchangeRate.Buy`/`Sell` and `JournalLine.ExchangeRate` are rates, not amounts, and
  can carry more than 2 decimal digits (e.g. ₡517.365/USD, used in a M1 rounding test).
  Scaling by 100 would truncate that precision. They are never `SUM()`-ed in a report,
  so the default TEXT storage's exact-round-trip guarantee is all they need.
- `CompanySettings.VatRate` is a fraction (0.13), not a currency amount.

## Consequences

- A `JournalLine` with ₡1,234.56 stores `123456` in SQLite; `FolioDbContextTests`
  verifies both a direct round-trip and a server-side `SumAsync()` over several rows
  returning an exact `decimal`, not a float-blurred approximation.
- Any future money-amount column must use `MoneyToCentsConverter` explicitly (it is not
  a model-wide convention) — a reviewer adding a new amount column needs to remember
  this, the same way they'd remember to use `Money`/`decimal` in the Domain.
- `EntryTemplateLine.DefaultAmount` was deliberately left on default decimal (TEXT)
  storage: it is only ever shown as a suggested value in the entry editor, never
  aggregated in a report.
