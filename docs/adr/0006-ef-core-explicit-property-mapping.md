# ADR-0006: Every scalar property is explicitly mapped, never left to pure convention

## Context

While building the M2 entity configurations, two surprising failures showed up only at
runtime (never at compile time, and `dotnet ef migrations add` built a model either
way):

1. `JournalLine`/`EntryTemplateLine` are owned collections (`OwnsMany`) whose only key
   EF could find by pure convention was a shadow `int` composite with the owner's FK.
   SQLite cannot auto-generate that kind of key (it has no sequence/identity mechanism
   outside of a single-column `INTEGER PRIMARY KEY` row-id alias), and in this version
   of EF Core's SQLite provider, that shadow property was never actually assigned a
   value either — every insert failed with `NOT NULL constraint failed: JournalLines.Id`.
2. `Period.StartDate`/`EndDate` round-tripped fine when read back and inspected as a
   plain C# property on a loaded object, but `OrderBy(p => p.StartDate)` threw
   `InvalidOperationException: ... member is unmapped` at query-translation time. The
   same turned out to be true for several other properties across other entities
   (`Account.IsPostable`/`IsActive`/`HasChildren`, `User.IsActive`/`FailedAttempts`,
   `JournalEntry.CreatedByUserId`/`CreatedAtUtc`, etc.) — any property that no Fluent
   API call ever referenced, even though nothing in EF Core's documented conventions
   says an untouched property should be excluded from the model.

## Decision

1. `JournalLine` and `EntryTemplateLine` each get their own real `Guid Id`, generated
   by the Domain itself (`Guid.NewGuid()` in their constructor), exactly like every
   other entity. Their EF configuration uses `HasKey(l => l.Id)` instead of a shadow
   key. This sidesteps SQLite's inability to generate the key entirely — the Domain
   already generates every other entity's id the same way, so this isn't a new pattern,
   just applying the existing one consistently instead of trying to lean on an "owned
   type doesn't need identity" purity argument that doesn't hold up against the
   provider's limitations.
2. **Every scalar property on every entity is given an explicit `builder.Property(x =>
   x.Y)` call** in its `IEntityTypeConfiguration`, even ones with no special conversion,
   max length, or constraint — purely to guarantee EF actually includes them in the
   model for query translation. This was applied retroactively across all nine
   configuration files once the pattern was understood from the `Period.StartDate`
   failure.

## Consequences

- Entity configuration files are longer and more repetitive than the EF Core docs'
  "convention does most of the work" examples suggest — every property is listed once,
  even ones with no extra configuration. This trades brevity for not re-discovering
  this failure mode again in M4–M9 when a screen suddenly needs to filter or sort by a
  field nobody had queried server-side yet.
- Any new entity or new property added from M3 onward must get the same treatment:
  add it to the entity, then explicitly add `builder.Property(x => x.NewThing)` to its
  configuration, even if it "should" just work by convention.
- `JournalLine`/`EntryTemplateLine` having a real `Id` is a small, harmless deviation
  from the Domain-level description in `docs/PROGRESS.md`'s M1 section ("no identity of
  their own") — M1's domain *rules* don't change (a line is still only ever reachable
  through its entry), but M2 gave it an id purely so Infrastructure could persist it.
