# ADR-0009: The account code is the hierarchy, and is immutable after creation

## Context

M4 needed a way to create/edit chart-of-accounts entries and let the UI show them as a
tree. Two designs were on the table for *where a new account's parent comes from*:

1. A separate "parent picker" (choose an existing account from a list), with the code
   assigned independently (by the system, or free-typed).
2. Type the full dotted code directly (e.g. `1.1.07`); the parent is *derived* from it
   — `AccountCode.Parent()` already existed since M1, stripping the last segment.

Option 1 can disagree with itself: nothing stops someone from picking "Activo
corriente" (`1.1`) as the parent while typing `2.9.03` as the code, producing a tree
that contradicts its own codes. SPEC §2.3 also treats the code as the account's
accounting identity (used on every report, in Costa Rican bookkeeping practice,
exactly the way a chart-of-accounts code is everywhere) — tying it to an independently
chosen parent makes that identity accidental rather than structural.

A related question: can the code (and therefore the parent) ever change after
creation? SPEC doesn't say, but an account's code is referenced everywhere once it has
even a single posted entry, and Costa Rican accounting practice treats renumbering the
chart of accounts as something closer to "deleting and recreating the account" than an
edit.

## Decision

- **The full dotted code is the only thing typed when creating an account.** The parent
  is always `AccountCode.Parent()` of that code — looked up by
  `IAccountRepository.GetByCodeAsync`, never chosen separately. If no account exists
  with that parent code, creation fails with a clear error instead of silently creating
  an orphan or a second root.
- **Code and parent are immutable after creation.** `UpdateAccountRequest` (unlike
  `CreateAccountRequest`) has no `Code` field at all — the type itself makes "rename an
  account" and "move/renumber an account" two different, deliberately unequal
  operations; only the former exists through the UI in M4.
- Ordering throughout the tree, search, and reports uses `AccountCode.CompareTo`
  (segment-wise numeric comparison, e.g. `1.1.2` before `1.1.10`), not a plain string
  sort, which would put `1.1.10` before `1.1.2`.

## Consequences

- Creating a child account requires knowing its full code up front (e.g. typing
  `1.1.07` instead of picking "Activo corriente" from a list and letting the system
  number it `.07`). This is the same workflow Costa Rican bookkeepers already use with
  a printed/Excel chart of accounts, so it costs nothing in practice, but it does mean
  the form does no autocomplete/suggestion of the next free sibling number in M4 — a
  reasonable candidate for a later usability pass, not a correctness gap.
- Renumbering a misclassified account (SPEC doesn't require this for M4) is out of
  scope by design: the only way to "move" an account today is deactivate it and create
  a new one with the right code, which is also the safer choice once the account has
  any posted movements.
- `AccountCode` already implemented `IComparable<AccountCode>` for this ADR's ordering
  requirement before M4 needed it in the UI — the comparer and its unit tests belong to
  the M1 value object, not to the chart-of-accounts feature, keeping the numeric-sort
  rule in one place for every future consumer (reports, exports, etc.).
