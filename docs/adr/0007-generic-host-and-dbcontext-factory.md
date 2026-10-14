# ADR-0007: Generic Host composition root, and `IDbContextFactory` over a scoped `DbContext`

## Context

M3 moves Folio's composition from the ad-hoc, manually-wired startup code in M2's
`App.xaml.cs` to a proper `Microsoft.Extensions.Hosting` Generic Host — the same DI
container, configuration and logging infrastructure ASP.NET Core and worker services
use, just hosting a WPF app instead. Two decisions had to be made before any DI
registration could happen: how to run the M2 database migration/seed as part of this
new startup sequence, and what lifetime to give `FolioDbContext` itself.

A desktop app has no natural "scope" the way a web request does. EF Core's `DbContext`
is not thread-safe and is not meant to stay open indefinitely (its change tracker only
grows), so a single `Singleton` `DbContext` shared by every ViewModel for the app's
whole lifetime is the wrong default, even though it would be the simplest thing to
register.

## Decision

**The Host runs the M2 migration/seed as an `IHostedService`.** `DatabaseInitializerHostedService.StartAsync`
calls the *same* `FolioDbInitializer.InitializeAsync(connectionString)` from M2,
unchanged — it still builds its own interceptor-free `DbContext` internally (ADR-0004's
reasoning: bootstrapping isn't a user action to audit). Only *who calls it* changed:
the Generic Host invokes every registered `IHostedService.StartAsync` automatically
when `host.StartAsync()` runs, so `App.xaml.cs` no longer has bespoke migration code —
awaiting `_host.StartAsync()` is enough.

**`FolioDbContext` is registered via `AddDbContextFactory<FolioDbContext>`, not
`AddDbContext`.** This is Microsoft's own documented recommendation for WPF/WinForms +
EF Core + DI. Repositories (M2) changed from taking a `FolioDbContext` directly to
taking an `IDbContextFactory<FolioDbContext>`, creating (and disposing) a short-lived
context inside each method:

```csharp
await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
```

Every method that mutates data now calls `SaveChangesAsync` itself, inside that same
short-lived context — a repository call is a complete unit of work. This made the M2
`IUnitOfWork` interface dead code (nothing called it once repositories self-contained
their saves), so it was removed rather than kept "for later" (CLAUDE.md: don't design
for hypothetical future requirements).

**Everything cross-cutting is a Singleton**, not Scoped: `IClock`, `IPasswordHasher`,
`AuditSaveChangesInterceptor`, `ICurrentUserSession`/`ICurrentUserService`, and every
repository. A single-user desktop app has exactly one "session" for its whole process
lifetime — there is no request to scope to, so Singleton is the correct lifetime here,
not a shortcut.

**Updating a previously-loaded entity re-attaches it as `Modified`.** Because each
repository call gets its own context, a `User` returned by `GetByUsernameAsync` is
detached by the time `AuthenticationService` mutates it (e.g. `RecordFailedLogin`).
`UserRepository.UpdateAsync` re-attaches it with `context.Users.Update(user)` in a new
context. EF has no prior snapshot to diff against in that case, so it marks every
column "modified," not just the ones that actually changed — the audit log shows the
whole row's old/new values instead of a precise diff. `PasswordHash`/`Salt` are still
redacted either way (ADR-0004). This is a known, accepted trade-off of the
`IDbContextFactory` pattern; a future milestone could tighten it with a per-field
"RecordX" repository method if the audit log's precision becomes a real problem.

## Consequences

- Any future repository added in M4+ must follow the same `IDbContextFactory` + create
  context + save-per-call pattern, for consistency and to avoid a half-scoped,
  half-factory mix of lifetimes.
- `App.xaml.cs` is now purely: hook global error handling → set culture → build and
  start the Host (which migrates/seeds) → apply the saved theme → show the window. All
  the actual wiring lives in `AddFolioInfrastructure`/`AddFolioDesktop`.
- If a future use case genuinely needs several repository calls to commit atomically
  together (not just each call being its own atomic unit), that's the moment to
  reintroduce an explicit unit-of-work abstraction — not before.
