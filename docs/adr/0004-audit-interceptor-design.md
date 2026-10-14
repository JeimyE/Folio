# ADR-0004: Audit interceptor — redaction, action labels, and seed-time silence

## Context

SPEC §2.9 requires every create/update/delete/post/reverse/close/reopen to be recorded
automatically, via an EF Core `SaveChangesInterceptor`, with before/after values as
JSON. Three concrete problems came up implementing `AuditSaveChangesInterceptor`:

1. A generic "dump every changed property to JSON" approach would capture
   `User.PasswordHash` and `User.Salt` whenever a user's password changes — directly
   violating "never log or store plain-text passwords" (even though these are already
   hashes, not plain text, logging them is still an unnecessary exposure of secrets).
2. `ChangeTracker.Entries()` only exposes EF's own vocabulary: `Added`/`Modified`/
   `Deleted`. SPEC's action list is richer (`Post`, `Reverse`, `Close`, `Reopen`), and
   none of those exist as a distinct EF state — a posted entry is just a "Modified"
   `JournalEntry` whose `Status` property happens to have changed.
3. The demo seed (M2) creates ~35 accounts, 3 users, 12 periods, ~365 exchange rates
   and 150+ journal entries in one run. Auditing every single one of those as a
   "Create" row would make the Bitácora (M9) open to a wall of noise that has nothing
   to do with anything a user did.

## Decision

**Redaction by property name, not by entity type.** A fixed `HashSet<string>` of
sensitive property names (`PasswordHash`, `Salt`) is checked before any property is
added to the `OldValues`/`NewValues` dictionaries — on *any* entity, not just `User`.
If every changed property on a `Modified` entry is sensitive, no audit row is written
for it at all, rather than writing an empty one. `AuditSaveChangesInterceptorTests`
asserts the redacted values and property names never appear in the stored JSON, while
non-sensitive fields on the same entity (e.g. `IsActive`) still do.

**Action labels inferred from known status transitions.** The interceptor's default
label is `Create`/`Update`/`Delete` from the EF state. For `JournalEntry` and `Period`
specifically, it additionally checks whether the `Status` property's original/current
values match a known transition (Draft→Posted, Posted→Reversed, Open→Closed,
Closed→Open) and relabels the row `Post`/`Reverse`/`Close`/`Reopen` accordingly. This
stays entirely automatic — no feature calls `Audit("Post", ...)` — while still matching
SPEC's vocabulary. Enum values serialize as their names (`"Posted"`, not `1`) via
`JsonStringEnumConverter`, so the JSON is human-readable for M9's viewer.

**Seeding bypasses the interceptor entirely, and writes one summary row itself.**
`FolioDbInitializer` builds the `DbContext` it uses for migration and seeding *without*
`AddInterceptors(...)`. The interceptor never runs during seed, so there is no flood to
prevent in the first place. After `FolioSeeder.SeedAsync` returns a one-line summary
string, `FolioDbInitializer` inserts exactly one `AuditLogEntry` itself (`Action =
"Seed"`), so the Bitácora still shows that the demo data arrived, without pretending a
user created 500+ rows by hand. `FolioSeederTests` asserts the audit table has exactly
one row after `InitializeAsync` runs against a fresh database.

## Consequences

- Any entity that gains a password-like field in the future must add its property name
  to `SensitivePropertyNames` — this is a manual step, not something structurally
  enforced. Worth revisiting if more secret-like fields appear.
- The status-transition labels are a small, fixed list; a new meaningful transition
  (e.g. a future `User` suspension state) needs its own `if` branch in
  `DetermineAction`, not a generic mechanism.
- Because seeding uses an interceptor-free context, any code path that reuses
  `FolioSeeder.SeedAsync` directly (as some tests do) on a context that *does* have the
  interceptor attached will **not** get the "one row" guarantee — that guarantee is a
  property of `FolioDbInitializer`'s bootstrap context, not of the seeder itself.
