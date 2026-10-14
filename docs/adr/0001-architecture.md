# ADR-0001: Solution architecture and foundational choices

## Context

Folio needs a structure that (a) keeps accounting rules testable and free of UI/database
concerns, (b) is easy for a reviewer to clone and run with no setup, and (c) demonstrates
Clean Architecture in a way that holds up in a job-interview conversation.

Several foundational choices had to be made before any feature code could be written:
the target framework, how NuGet package versions are managed, the solution file format,
and how to handle a packaging quirk in one of the mandated UI packages.

## Decision

- **Clean Architecture, four layers**: `Folio.Domain` ← `Folio.Application` ← `Folio.Infrastructure`
  / `Folio.Desktop`. Dependencies point inward only; `Domain` has zero package references.
  ViewModels (added from M3 onward) will call `Folio.Application` services, never `DbContext`
  directly — this is enforced by convention and reviewed per PR, not by a build rule, since a
  four-project solution doesn't need an architecture test to keep it honest.
- **Target framework: .NET 10.** SPEC §4 says "prefer .NET 10, fall back to .NET 8"; the
  installed SDK is 10.0.400 and .NET 10 is the current LTS, so there was no need to fall back.
  `Folio.Domain`, `Folio.Application` and all three test projects target plain `net10.0` so they
  can run on any OS (useful for CI and for Jeimy if she ever reviews from outside Windows).
  `Folio.Desktop` targets `net10.0-windows` because WPF is Windows-only.
- **NuGet Central Package Management** (`Directory.Packages.props` + `ManagePackageVersionsCentrally`)
  instead of pinning versions per `.csproj`. One file lists every package version for the whole
  solution, which avoids silent version drift between a library and its test project and makes
  dependency upgrades a one-line change.
- **Classic `.sln` format, not `.slnx`.** The .NET 10 SDK's `dotnet new sln` now defaults to the
  new XML-based `.slnx` format. `CLAUDE.md` and SPEC §4 both reference `Folio.sln` explicitly, so
  the solution was generated with `dotnet new sln --format sln` to match.
- **`NU1701` suppressed only in `Folio.Desktop.csproj`.** `LiveChartsCore.SkiaSharpView.WPF` (the
  chart package required by SPEC §4) depends transitively on `OpenTK`, `OpenTK.GLWpfControl` and
  `SkiaSharp.Views.WPF`, none of which publish a `net*-windows` asset — only .NET Framework ones
  (`net462`–`net481`). NuGet's "compatible framework" fallback resolves this correctly and the
  package works at runtime, but with `-warnaserror` the fallback is reported as error `NU1701`.
  The warning is suppressed with a `<NoWarn>` in that one project only, with a comment explaining
  why, rather than disabling `TreatWarningsAsErrors` solution-wide.
- **Empty WPF shell for M0.** `App.xaml`/`MainWindow.xaml` just open a blank window. Generic
  Host, DI, Serilog and theming are explicitly scoped to M3 per SPEC §6, so M0 does not anticipate
  them.

## Consequences

- Adding a new project later means adding one `<PackageVersion>` row (if it uses a new package)
  instead of touching every `.csproj` that references it.
- If a future LiveCharts2 release ships proper `net*-windows` assets, the `NoWarn` entry and this
  ADR's explanation should be removed together.
- Anyone running `dotnet new sln` again on this repo must remember to pass `--format sln`, or they
  will regenerate a `.slnx` that doesn't match the documented commands.
- Because `Domain` and `Application` are OS-agnostic (`net10.0`, no WPF), their unit tests can run
  in CI on any runner; only the full solution build needs `windows-latest` (for `Folio.Desktop`).
