# ADR-0010: Account balances are computed on demand, not stored

## Context

The chart-of-accounts detail panel (M4, adjustment requested by Jeimy) needs to show
each account's current balance: its own net movements for a postable (leaf) account,
or the sum of every descendant leaf's balance for a grouping account — netted, not
just added, so an internal transfer between two child accounts of the same group
nets to zero at the group level instead of double-counting.

Two designs:

1. Maintain a running `Balance` column on `Account`, updated every time a journal
   entry posts.
2. Compute it from the ledger every time it's asked for.

A stored running balance needs careful incremental maintenance (every `Post()` and
every future `Reverse()` touching every ancestor up the tree) and a reconciliation
story for when it inevitably drifts. SPEC has no requirement for balance history as of
a past date yet (that's a reporting feature for a later milestone) — M4 only needs
"what is it right now."

## Decision

`ChartOfAccountsService.GetDetailAsync` computes the balance every time, via
`IAccountRepository.GetMovementTotalsAsync` — a real SQL `SUM()` over the integer-cents
`Debit`/`Credit` columns (ADR-0003) of every `Posted` journal line for the relevant
account id(s), never by loading entries into memory and summing in C#. For a leaf
account that's one id; for a grouping account, the service first walks the in-memory
tree (already loaded for the tree view) to collect every postable descendant's id,
then asks the repository to sum movements across all of them in one query. The result
is signed according to `NormalBalance` (`TotalDebit − TotalCredit` for a debit-normal
account, the mirror for credit-normal) so "positive" always means "more of what this
account normally accumulates," regardless of type.

## Consequences

- No new column, no migration, nothing that can drift out of sync with the ledger —
  the balance shown is always literally what `Posted` journal lines say it is.
- Every detail-panel open costs one aggregation query (two for a grouping account: the
  tree walk is in-memory, but `GetMovementTotalsAsync` still hits SQLite). Fine at this
  scale (SME chart of accounts, hundreds not millions of lines); would need
  revisiting — most likely the stored/incremental design this ADR rejected — if a
  future milestone needs this on a dashboard refreshed continuously rather than on
  selection.
- `tests/Folio.Infrastructure.Tests/ChartOfAccountsBalanceIntegrationTests.cs` posts a
  real internal transfer between two sibling leaf accounts specifically to prove the
  netting behavior end to end (through the real repository and SQLite, not a mock) —
  a bug that summed instead of netted would have passed every other test in the suite.
