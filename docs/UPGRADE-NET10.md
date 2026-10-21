# Upgrade to .NET 10 - build and rollout notes

No .NET SDK was available while this upgrade was written, so **nothing was compiled or run**.
Every C# file was syntax-checked and every script was checked with `node --check`, but you must
build once and fix anything the compiler reports (most likely small typos or missing usings).

## 1. Build

```
dotnet --version            # 10.0.x required
dotnet restore
dotnet build
```

## 2. Database

The app now uses `DatabaseUpgrader` at start-up:

| Situation | What happens |
|---|---|
| New empty database | Applies migrations (or `EnsureCreated` while no migration exists) |
| Existing database created by the old release | Creates only the **new tables** (quotations, invoices, payments, support cases, notifications, audit log); existing tables and data are untouched |
| Database managed by migrations | Applies pending migrations |

Create the first migration once (from the solution folder):

```
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project Infrastructure/Infrastructure --startup-project Presentation/InterlaceUI --context DataContext
```

The connection string comes from `appsettings.json`, or from the `CRM_CONNECTION` environment variable.
After this migration exists, a legacy database is baselined automatically (history table filled, no table re-created).
For later model changes: `dotnet ef migrations add <Name> ...` and restart the app.

Back up the database before the first start with the new version.

## 3. First start after upgrade

- New menu pages create new roles; `RoleSyncSeeder` adds them to the default admin. **Log out and in again** (roles are cached in the browser).
- With `IsDemoVersion: true`, demo quotations, invoices, payments and support cases are added when those tables are empty.

## 4. What is new

- Sales: Quotations (convert to invoice or sales order), Invoices (due date, paid, balance, overdue), Payments.
- Support: cases with priority, channel, SLA due date, assignee and comments.
- CRM: Kanban pipeline board (drag a card between stages, Won and Lost columns), sales forecast (weighted by stage, by month, by team, win rate, Excel export), computed lead score, automatic assignment to the least loaded sales team when no team is given.
- Notifications: bell in the top bar and a list page. A background job (every 30 min) raises alerts for overdue invoices, SLA breaches, leads past their closing date and quotations expiring in 3 days. Alerts without a target user are visible to everyone and are marked read for everyone.
- Audit log (Settings > Audit Log): every create/update/delete done through the app, with old and new values.

## 5. Package decisions

| Package | Version | Why |
|---|---|---|
| Microsoft.* (EF Core, Identity, JwtBearer, Mvc) | 10.0.10 | .NET 10 |
| AutoMapper | 16.1.1 | Fixes GHSA-rvv3-g6hj-g44x. Commercial licence key is optional: `AutoMapper:LicenseKey` |
| MediatR | 12.4.1 | 13+ is commercial dual-licensed |
| Swashbuckle.AspNetCore | 7.1.0 | 10.x needs Microsoft.OpenApi 2.x code changes; do this when you can compile |

## 6. Known risks and things worth fixing next

- Compile errors are possible (see top).
- `appsettings.json` contains a SQL `sa` password and `EnableSensitiveDataLogging()` is on: move the secret to user-secrets or environment variables and turn sensitive logging off outside development.
- Client-sent user ids (`createdById`, `deletedById`) are trusted by older endpoints; new notification endpoints read the user from the token.
- Audit log only records changes made while an HTTP request is active (seeders and background jobs are not audited).
- See `CRM-FEATURE-ROADMAP.md` for the P2/P3 market features not built yet (2FA/passkeys, email sync, web-to-lead forms, calendar, import/export, workflow builder, AI).

## 7. Phase 2: redesign and intelligence (from docs/CRM-MARKET-RESEARCH-2026.md)

New pages (log out and in again so the new menu items and roles appear):

| Page | What it does |
|---|---|
| Dashboards > Insights (now the landing page) | KPI tiles with trends, "What should I do now?" list, deals needing attention, pipeline, receivables ageing, cash by month |
| Third Party > Customer 360 | Lifetime revenue, open balance, health rating with reasons, documents, support cases and a merged timeline. Also opened from search results |
| Pipeline > Pipeline Board | Cards now show an explainable health badge (click it to see the five factors) and a next-best-action line |
| Settings > Import Customers | CSV import with a check step, duplicate detection by name and email, downloadable template |

Shell features on every page: Ctrl+K command palette (pages, actions, customers, leads, documents, recents), light/dark theme toggle (remembers your choice, follows the OS on first visit), and the assistant drawer (wand icon).

Deal health (0-100) = qualification 30 + stage 20 + momentum 20 + timeline 15 + stakeholders 15. The same model drives the board, the lead list score, the Insights page and the assistant.

Assistant: works out of the box from live data with built-in rules (overdue invoices, at-risk deals, forecast, SLA, quotes, "what should I do today?", and "draft reminder for INV-0001" style email drafts). To use a language model, set `Assistant:ApiKey` (and optionally `Assistant:Model`) in user-secrets or an environment variable (`Assistant__ApiKey`). Only an aggregated snapshot of KPIs, priorities and at-risk deals is sent, never full tables. If the call fails the assistant falls back to the built-in rules.

No new database tables in this phase, so no new migration is needed.

## 8. Phase 3: login and app-wide design

- New split-screen sign-in, register, forgot-password, logout and confirmation pages (brand panel, show/hide password, light/dark, mobile layout). All existing form logic is unchanged. The non-working "Sign in with Facebook/Google" buttons and the unused "Remember me" box were removed.
- New public landing page at `/` and a friendly `/Error` page.
- Every list page now has a page title with a breadcrumb (taken from the menu). Pages with their own header (Insights, Customer 360, Import) hide it with `ViewData["HideTitle"] = true`.
- Shared look: `wwwroot/css/crm-modern.css` (tokens, light and dark), `crm-auth.css`, `crm-landing.css`; behaviour in `wwwroot/js/crm-shell.js`. To change the brand colour, edit `--primary` in `crm-modern.css`.
- The licence footer text in `__footer.cshtml` was left untouched on purpose.
