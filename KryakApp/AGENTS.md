# AGENTS.md

## Purpose
- This file gives coding agents a reliable working guide for this repository.
- Scope: `KryakApp` (WinUI 3 desktop app, .NET 8, Windows target).
- Priorities: keep changes minimal, preserve app startup behavior, and prefer simple WinUI patterns.

## Repository Snapshot
- Solution: `KryakApp.sln`
- Main project: `KryakApp.csproj`
- App type: WinUI 3 packaged desktop app (`Microsoft.WindowsAppSDK`)
- Target framework: `net8.0-windows10.0.19041.0`
- Nullable context: enabled (`<Nullable>enable</Nullable>`)
- Languages in use: C# and XAML

## Rules Files Check
- Checked for Cursor rules in `.cursor/rules/` and `.cursorrules`: none found.
- Checked for Copilot rules in `.github/copilot-instructions.md`: none found.
- Checked for an existing `AGENTS.md` under `C:\Users\sheud\Documents\GitHub\Kryak`: none found.
- If any of these files are added later, treat them as higher-priority guidance and merge their constraints into this file.

## Setup Commands
- Restore dependencies:
  - `dotnet restore KryakApp.sln`
- Build solution (Debug):
  - `dotnet build KryakApp.sln -c Debug`
- Build solution (Release):
  - `dotnet build KryakApp.sln -c Release`

## Run Commands
- Run app project:
  - `dotnet run --project KryakApp.csproj`
- Run with explicit configuration:
  - `dotnet run --project KryakApp.csproj -c Debug`

## Test Commands
- Run all tests in solution:
  - `dotnet test KryakApp.sln`
- Run tests in one test project:
  - `dotnet test <path-to-test-project.csproj>`
- Run a single test by fully-qualified name:
  - `dotnet test <path-to-test-project.csproj> --filter "FullyQualifiedName=Namespace.ClassName.MethodName"`
- Run tests by class name:
  - `dotnet test <path-to-test-project.csproj> --filter "FullyQualifiedName~Namespace.ClassName"`
- Run tests by trait/category (if traits exist):
  - `dotnet test <path-to-test-project.csproj> --filter "Category=Unit"`
- Note: this repository currently does not include a test project. If you add tests, use the commands above.

## Lint / Formatting Commands
- Preferred formatting check:
  - `dotnet format --verify-no-changes`
- Auto-fix formatting/style/analyzers:
  - `dotnet format`
- Run analyzer-focused formatting only:
  - `dotnet format analyzers`
- Run whitespace/style formatting only:
  - `dotnet format whitespace`
  - `dotnet format style`
- If `dotnet format` is unavailable, install/update .NET SDK tooling and retry.

## Pre-PR Validation
- Recommended local gate before opening a PR:
  1. `dotnet restore KryakApp.sln`
  2. `dotnet build KryakApp.sln -c Debug`
  3. `dotnet format --verify-no-changes`
  4. `dotnet test KryakApp.sln`
- If tests do not yet exist, still run build + format checks.

## C# Style Guidelines
- Use file-scoped namespaces for new/edited C# files when practical.
- Keep `nullable` warnings at zero in edited code.
- Prefer explicit accessibility modifiers on types and members.
- Keep classes focused; avoid large multi-responsibility code-behind files.
- Prefer constructor/property injection when adding services.
- Use `var` when the right-hand side type is obvious; otherwise use explicit types.
- Prefer expression-bodied members only for short, clear members.
- Keep methods small and deterministic where possible.
- Do not introduce static mutable global state unless required by platform lifecycle.

## Imports and Usings
- Remove unused `using` directives.
- Keep `using` directives grouped and consistently ordered.
- Prefer project-wide/global usings only when they reduce repetition without hiding intent.
- Avoid adding broad framework usings not needed by the file.

## Naming Conventions
- Types, methods, properties, events: `PascalCase`.
- Local variables and parameters: `camelCase`.
- Private fields: `_camelCase`.
- Interface names: `I` prefix + `PascalCase`.
- Async methods: suffix with `Async`.
- XAML names (`x:Name`): meaningful `PascalCase` identifiers.
- Avoid abbreviations unless they are standard platform terms.

## Error Handling and Logging
- Validate external inputs at boundaries (UI input, file I/O, network calls).
- Throw specific exceptions; avoid throwing `Exception` directly.
- Do not swallow exceptions silently.
- Catch only where recovery, translation, or user messaging is possible.
- Preserve stack traces when rethrowing (`throw;`, not `throw ex;`).
- Use structured logging if logging infrastructure is introduced.
- Ensure user-facing failures are actionable and non-technical when shown in UI.

## Async and Threading
- Prefer async APIs for I/O-bound operations.
- Avoid blocking calls like `.Result` and `.Wait()` on async tasks.
- Keep UI updates on the UI thread.
- Use cancellation tokens for long-running operations when appropriate.
- Avoid fire-and-forget tasks unless explicitly safe and observed.

## WinUI / XAML Guidelines
- Keep XAML declarative; move non-trivial logic out of code-behind when possible.
- Prefer bindings and commands over direct control manipulation.
- Use `x:Bind` where compile-time binding benefits are desired.
- Keep resources centralized (`App.xaml` dictionaries) when reused.
- Respect existing visual/theme decisions unless task explicitly changes UX.
- Preserve startup path in `App.OnLaunched` unless requested otherwise.

## Project Structure Guidelines
- Keep app bootstrap concerns in `App.xaml` / `App.xaml.cs`.
- Keep window-shell concerns in `MainWindow.xaml` / `MainWindow.xaml.cs`.
- For new features, prefer feature-oriented folders (e.g., `Views/`, `ViewModels/`, `Services/`).
- Add tests in a separate test project (e.g., `KryakApp.Tests`) rather than in app project.

## Dependency and Package Guidance
- Prefer built-in .NET / WinUI capabilities before adding dependencies.
- When adding a NuGet package, justify necessity in PR notes.
- Keep package versions explicit and compatible with the target framework.
- Avoid introducing preview packages unless explicitly requested.

## Agent Workflow Expectations
- Read relevant files before editing; do not make speculative architecture changes.
- Keep diffs focused on requested behavior.
- Update docs when behavior or developer workflow changes.
- Include command examples in PR descriptions for new test/lint flows.
- If tooling/rules files are added later (`.cursor/*`, Copilot instructions), update this file accordingly.

## Definition of Done for Agent Changes
- Code builds locally with `dotnet build`.
- Formatting/lint checks pass (`dotnet format --verify-no-changes` when available).
- Tests pass, or test absence is clearly stated.
- No unused usings, dead code, or obvious nullable warnings introduced.
- Changes are scoped, readable, and aligned with conventions above.
