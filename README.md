# Interlace CRM

A self-hosted CRM for small and mid-size teams, built on .NET 10 with Clean Architecture. One company per install; sales, service and customer success in one place.

## What is in it

**Sales** – leads with pipeline stages and conversion to customers, weighted forecast, sales targets and leaderboard, quotations with discount approval and revisions, sales orders, invoices and payments, products, tax and units.

**Service and success** – support cases with SLA targets, first-response tracking and escalation, categories (queues), knowledge base with a public help centre, customer satisfaction surveys, contracts and renewals, customer health scores and churn alerts, and a customer portal (e-mail sign-in link; cases, invoices, contracts).

**Work and insight** – activities and tasks, My Work dashboard, saved grid views, bulk actions, custom fields, report builder with scheduled e-mail delivery, data quality and duplicate checks, insights and alerts, audit log.

**Automation and integration** – workflow rules (event triggered), webhooks and API keys, web-to-lead endpoint, pipeline stage settings.

**AI (optional)** – record summaries, reply drafts and confirmed follow-up tasks, plus a built-in assistant (Ctrl+K). Works without a model (plain facts); with a key it uses the Anthropic API. AI Governance sets a switch, per-user and company limits, and keeps a usage log (sizes only, never the text).

## Architecture

Monolith, Clean Architecture, CQRS with MediatR, FluentValidation, AutoMapper, EF Core (SQL Server), ASP.NET Identity with JWT, Serilog. Pages are Razor with Vue 3 and Syncfusion grids.

```
Core/Domain            entities, enums
Core/Application       features (commands, queries, rules), interfaces
Infrastructure         EF Core, identity, e-mail, background services
Presentation/InterlaceUI   API controllers + Razor pages
Tests/InterlaceUI.Tests    unit tests
docs                   gap analysis, roadmap, upgrade notes
```

Database tables and new columns are created automatically at start-up (no migrations to run).

## Run it

1. Install the .NET 10 SDK and SQL Server.
2. Set `ConnectionStrings:DefaultConnection`, `Jwt:Key` (32+ characters) and the SMTP settings. **Do not keep real secrets in `appsettings.json`**: use user secrets or environment variables (for example `ConnectionStrings__DefaultConnection`).
3. Change `AspNetIdentity:DefaultAdmin` before first start, or change that password right after your first sign-in.
4. `dotnet run --project Presentation/InterlaceUI`
5. Run the tests: `dotnet test Tests/InterlaceUI.Tests`

Optional settings: `Assistant:ApiKey` and `Assistant:Model` (AI), `SmtpSettings` (sign-up confirmation, reports, customer portal links, survey e-mails).

## Access control

Each menu page's first URL segment is a role (for example `Customers`, `SupportCases`, `Contracts`). Roles are created at start-up; grant them to users under Users/Roles, then ask users to sign in again. The server enforces the same rules on every API call (`ModulePermissions`), and a unit test fails if a controller is left unmapped.

Roles added recently: `SalesTargets`, `CustomFields`, `Integrations`, `QuotationApprovals`, `PipelineSettings`, `Workflows`, `Reports`, `SupportSettings`, `KnowledgeBase`, `Contracts`, `AiGovernance`.

## Public endpoints

Anonymous and rate limited: login/registration, web-to-lead capture, customer satisfaction survey link, help centre, customer portal sign-in. Everything else needs a signed-in user.

## Backups and keys

Back up the database and the `App_Keys` folder (data-protection keys) and `App_Data/docs` (uploaded documents).

## Roadmap

See `docs/CRM-GAP-ANALYSIS.md`. Not built yet: messaging channels (WhatsApp, SMS, telephony, live chat, e-sign), calendar/mailbox sync, SSO, AI agents and predictive models.

## License

See `LICENSE.txt`. The project started from the Indotalent open-source CRM.
