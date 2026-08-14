# AGENTS.md

ASP.NET Core MVC app on **net10.0** (`Microsoft.NET.Sdk.Web`), scaffolded from the default template and largely unmodified. Not a git repo. No test project, no EF/DB, no npm/node toolchain.

## Build & verify

- Build: `dotnet build 20262.csproj` (only verification available — no lint, typecheck, or test commands exist).
- Run dev: `dotnet run` → http://localhost:5167 (https: localhost:7247, see `Properties/launchSettings.json`).

## Project layout

- Root namespace is `_20262` (project name starts with a digit), so C# code uses `_20262.Controllers`, `_20262.Models`, etc.
- `Program.cs` uses the .NET 10 static-assets pipeline (`MapStaticAssets()` + `.WithStaticAssets()`) instead of UseStaticFiles/bundler. Assets live in `wwwroot/` and are fingerprinted via `asp-append-version`.
- `Views/_ViewImports.cshtml` already injects `@using _20262` and `@using _20262.Models` + MVC tag helpers globally — new views don't need `@using` lines.
- Layout resolved from `Views/_ViewStart.cshtml` → `Views/Shared/_Layout.cshtml`.
- Client libraries (jquery, bootstrap, jquery-validation, jquery-validation-unobtrusive) are **committed under `wwwroot/lib/`**; do not install anything via npm/cdn.

## Conventions (current state)

- User-facing UI strings are in **Spanish** (e.g. Contacto form, validation messages), even though template default text (Privacy, Index) is English.
- Server-side validation uses DataAnnotations on `Models/*ViewModel`; client-side validation is wired by including `@section Scripts { <partial name="_ValidationScriptsPartial" /> }` in the view and marking forms `novalidate`.
- POST actions use `[HttpPost]` + `[ValidateAntiForgeryToken]`; tag helpers auto-emit the antiforgery token.
- No persistence: the contact form uses TempData + POST-redirect-GET for a success message. Adding a DB or mail integration would be new work.
