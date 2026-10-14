# CLAUDE.md — Folio

Folio (by Folivora Software) is a double-entry accounting desktop app for Costa Rican SMEs, built with C#, .NET, WPF, EF Core and SQLite. The full specification is in `docs/SPEC.md` — **read it before starting any work**, and treat it as the source of truth.

This is a portfolio project. The owner (Jeimy) reviews every milestone and must be able to explain every decision in a job interview. Optimize for code that is clean, idiomatic and easy to read, not clever.

## Commands

```bash
dotnet --list-sdks                         # pick the target framework (see SPEC §4)
dotnet restore
dotnet build Folio.sln -warnaserror        # must be zero warnings
dotnet test Folio.sln                      # must be all green
dotnet run --project src/Folio.Desktop     # launch the app
dotnet ef migrations add <Name> --project src/Folio.Infrastructure --startup-project src/Folio.Desktop
```

## How to work

1. **One milestone at a time** (SPEC §6). Never start the next milestone without being asked.
2. Before writing code for a milestone, post a short plan (files to create, key decisions) and then proceed.
3. **Never run `git commit` or `git push`.** Leave all changes uncommitted. Jeimy commits and pushes herself from GitHub Desktop.
4. When a milestone is done:
   - Build with `-warnaserror` and run all tests. Fix everything before reporting.
   - Update `docs/PROGRESS.md` (milestone status, what was built, known limitations).
   - Write an ADR in `docs/adr/` for each significant decision (format: Context, Decision, Consequences).
   - Finish with a **summary in Spanish** for Jeimy: what was built, the files that matter most, the key concepts to understand (explained simply), and 3 interview questions she could be asked about this milestone, with short answers.
   - Add a **"Commits sugeridos"** section: a list of commits in Conventional Commits format (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`, `ci:`), each with its exact message and the exact files it should include, in the order they should be made.
     - Always write each file/folder's **full path from the repo root** (e.g. `src/Folio.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`), never just a bare name (never `ServiceCollectionExtensions.cs` or `DependencyInjection/`) — multiple projects in this solution have same-named files/folders (e.g. `DependencyInjection/`, `Repositories/`), so a bare name is ambiguous.
     - If a commit's files include a name that also exists elsewhere in the repo under a different path, add a short parenthetical warning on that line noting the other location(s), so Jeimy can double-check she's staging the right one in GitHub Desktop.
5. If something in the spec is ambiguous or seems wrong, ask instead of guessing. Do not add packages that are not in SPEC §4 without asking.

## Code rules

- **Architecture**: Domain ← Application ← Infrastructure / Desktop. Domain has no dependencies. ViewModels talk to Application services only — never to `DbContext`.
- **Accounting rules live in the Domain** and are enforced there (entities protect their invariants; no public setters that could break them). Application re-validates input with FluentValidation.
- **Money is `decimal`**, rounded to 2 decimals with `MidpointRounding.AwayFromZero`. Never `double` or `float` for amounts.
- `DateOnly` for accounting dates, `DateTime` in UTC for timestamps. Use an `IClock` abstraction, not `DateTime.Now`, so tests are deterministic.
- **English** for code, identifiers, comments, commits and docs. **Spanish (es-CR)** for everything the user sees, stored in `.resx` resources.
- MVVM with CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). No logic in code-behind beyond pure view concerns.
- Async all the way for I/O; no `.Result` or `.Wait()`.
- Nullable reference types on. No `!` null-forgiving operator unless justified in a comment.
- Prefer small, focused classes and methods. XML doc comments on public Domain and Application types.
- Never log or store plain-text passwords.

## Testing

- Every domain rule has unit tests, including the failure cases (unbalanced entry, posting to a closed period, editing a posted entry, non-postable account, rounding in multi-currency lines).
- Test names: `Method_Scenario_ExpectedResult`.
- Infrastructure tests use SQLite in-memory with real migrations.
- Use FluentAssertions for assertions and NSubstitute for test doubles.

## Definition of done (every milestone)

- [ ] `dotnet build -warnaserror` passes with zero warnings
- [ ] `dotnet test` passes
- [ ] The app launches and the milestone's features work
- [ ] `docs/PROGRESS.md` updated
- [ ] ADRs written for significant decisions
- [ ] Summary in Spanish delivered
