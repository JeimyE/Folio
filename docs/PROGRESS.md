# Progress

## M0 — Repository foundation (done)

**Built:**
- Solution `Folio.sln` with the Clean Architecture layout from SPEC §4:
  - `src/Folio.Domain`, `src/Folio.Application`, `src/Folio.Infrastructure`, `src/Folio.Desktop`
  - `tests/Folio.Domain.Tests`, `tests/Folio.Application.Tests`, `tests/Folio.Infrastructure.Tests`
- Target framework: **.NET 10** (`net10.0` for Domain/Application/tests, `net10.0-windows` for Desktop) — the SDK installed is 10.0.400, and .NET 10 is the current LTS.
- `Directory.Build.props`: nullable reference types, warnings-as-errors, .NET analyzers (`AnalysisMode=Recommended`).
- `Directory.Packages.props`: NuGet Central Package Management, one version per package for the whole solution.
- `.editorconfig`: formatting, file-scoped namespaces, `_camelCase` private fields, nullable diagnostics promoted to errors.
- `src/Folio.Desktop`: minimal WPF app (`App.xaml`, `MainWindow.xaml`) that opens an empty window. No DI/Hosting yet — that is M3.
- `.github/workflows/build.yml`: builds and tests on `windows-latest` on every push/PR to `main`.
- `docs/adr/0001-architecture.md`.

**Known limitations / things deferred on purpose:**
- No entities, DbContext, or migrations yet (M1/M2).
- Test projects have no tests yet — there is no domain logic to test. `dotnet test` reports "no tests found", not failures.
- `LiveChartsCore.SkiaSharpView.WPF` pulls transitive packages (`OpenTK`, `SkiaSharp.Views.WPF`) that only publish .NET Framework TFM assets. NuGet's compatible fallback works fine at runtime on Windows but raises `NU1701`; this is suppressed in `Folio.Desktop.csproj` only (not solution-wide). See ADR-0001.
- `dotnet new sln` in the .NET 10 SDK defaults to the new `.slnx` format; `Folio.sln` was generated explicitly with `--format sln` to match the classic format referenced in `CLAUDE.md`/SPEC.

**Verification:**
- `dotnet build Folio.sln -warnaserror` → 0 warnings, 0 errors.
- `dotnet test Folio.sln` → all green (no tests yet to fail).
- App launches via `dotnet run --project src/Folio.Desktop` and shows an empty window titled "Folio".
