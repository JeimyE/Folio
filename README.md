# Folio

Double-entry accounting desktop app for small and medium-sized businesses (pymes) in Costa
Rica, built with C#, .NET, WPF and EF Core. By **Folivora Software**.

Full product and technical specification: [`docs/SPEC.md`](docs/SPEC.md).
Milestone progress log: [`docs/PROGRESS.md`](docs/PROGRESS.md).
Architecture decisions: [`docs/adr/`](docs/adr/).

## Status

Work in progress, built one milestone at a time. See `docs/PROGRESS.md` for what's done.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```bash
git clone <this-repo>
cd Folio
dotnet build Folio.sln
dotnet run --project src/Folio.Desktop
```

No database server or secrets are needed — the SQLite database is created automatically on
first launch and filled with demo data (once persistence lands in M2).

## Architecture

Clean Architecture, one solution, dependencies point inward only:

```
Folio.Domain          entities, value objects, accounting rules — no dependencies
    ^
Folio.Application     use cases, DTOs, interfaces, validators
    ^
Folio.Infrastructure  EF Core + SQLite, repositories, audit log, PDF/Excel export
    ^
Folio.Desktop         WPF, MVVM (CommunityToolkit.Mvvm), DI composition root
```

See [`docs/adr/0001-architecture.md`](docs/adr/0001-architecture.md) for the reasoning behind
this structure and other foundational decisions.

## Running tests

```bash
dotnet test Folio.sln
```

## License

See [LICENSE](LICENSE).
