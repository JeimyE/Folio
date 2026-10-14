# ADR-0002: Rounding in multi-currency journal entries

## Context

SPEC §2.4 requires that a journal entry's total debits equal its total credits "in
functional currency, after rounding." Functional currency is always CRC (SPEC §2.2).

A line entered in USD stores its original amount and exchange rate; the functional
amount is `original × rate`, rounded to 2 decimals (SPEC §2.4). This rounding happens
**per line**. When several lines on one side of an entry are, together, a split of a
single original-currency total — e.g. a $10.00 sale broken into $3.33 + $3.33 + $3.34
across three revenue accounts — that split can balance exactly in USD while each
line's independently-rounded CRC conversion does not sum to the same CRC amount as
converting the $10.00 total directly. `Folio.Domain.Tests.Entities.JournalLineTests`
demonstrates a concrete case (rate ₡517.77/USD) where the independently-rounded split
totals ₡5177.69 against a direct conversion of ₡5177.70 — a one-colón gap that has
nothing to do with a mistake in the USD amounts, only with where the rounding happens.

## Decision

`JournalEntry.Post()` validates the functional-currency (CRC) balance of the entry's
lines with **zero tolerance**: `totalDebit == totalCredit`, both already rounded to 2
decimals at the line level. If a one-colón (or any) gap exists, `Post()` throws
`UnbalancedEntryException` — even if the entry's lines balance perfectly in USD.

The Domain does **not** attempt to detect or auto-correct this kind of rounding gap.
It has no notion of "these three lines are a split of one original amount," so it
cannot know whether a mismatch is a real data error or a rounding artifact — and
guessing would hide real unbalanced entries behind a tolerance window.

Responsibility for avoiding the gap belongs to whoever *constructs* the lines:

- Round the **total** once, in the original currency or after conversion, and
  distribute that rounded total across the split lines — typically by computing all
  but the last line normally and assigning the last line the exact remainder
  (`total − sum of the other lines`), rather than rounding each line's conversion
  independently.
- `JournalLineTests.Post_SplitForeignCurrencyLinesWhereLastLineAbsorbsTheCrcRemainder_Succeeds`
  (in `JournalEntryTests`) shows this technique producing a balanced entry from the
  same $10.00/$3.33/$3.33/$3.34 scenario that fails when rounded independently.
- The multi-currency line-splitting UI/helper that will need this (SPEC §2.4's IVA
  helper, and the general multi-currency entry editor) is Application-layer work
  scheduled for M5; M1 only needed the Domain rule and its tests to exist.

## Consequences

- `Post()` can reject entries that "look balanced" to a user glancing at original-
  currency amounts, if the CRC conversion doesn't tie out. This is intentional: it
  surfaces rounding bugs in whatever assembled the entry instead of silently drifting
  the books by a colón at a time.
- Any future Application-layer code that splits an amount across lines must use the
  "remainder on the last line" technique (or an equivalent largest-remainder method),
  never independent per-line rounding, when the split must reconstruct an exact total.
- If a genuine unresolvable remainder ever needs to be posted on its own (e.g. a true
  exchange-rate rounding difference, not a split), it should be posted as its own
  explicit line against an account such as *Diferencial cambiario*, not tolerated away.
