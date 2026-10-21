# Free CRM - Feature Roadmap (market gap analysis, Oct 2026)

Compares what Free CRM already has with what buyers expect from a 2026 CRM.
Priority: P1 = built in this upgrade, P2 = next, P3 = later / optional.

## What the project already covers
Campaigns, budgets, expenses, leads (BANT, pipeline stage), lead contacts and activities,
customers, vendors, products, sales orders, purchase orders, todos, file/image storage,
dashboard widgets, JWT + ASP.NET Identity (lockout, email confirmation), Swagger API.

## Gap analysis

| Market feature (2026) | In project today | Priority | Plan |
|---|---|---|---|
| Customer service / ticket management | No | P1 | **Support Cases** module (priority, status, SLA due date, assignee, comments) |
| Sales quotes | No | P1 | **Quotations** with items, status, convert to sales order / invoice |
| Invoicing and payment tracking (bookkeeping lite) | No (only SO/PO) | P1 | **Invoices + Payments**, balance due, overdue status |
| Pipeline as drag-and-drop board | Stage field only | P1 | **Kanban board** for leads by PipelineStage |
| Forecasting and revenue intelligence | Basic dashboard totals | P1 | Win rate, weighted forecast by stage, by sales team, export |
| Lead scoring and prioritization | BANT fields, no score | P1 | Automatic **lead score** from BANT + activity recency |
| Lead routing / assignment rules | Manual | P1 | **Auto-assignment** (round robin by sales team) |
| Follow-up reminders and notifications | Todo only | P1 | **In-app notifications** + activity reminders |
| Security: audit trail | Audit columns only | P1 | **Audit log** of create/update/delete |
| Security: 2FA / passkeys | No | P2 | ASP.NET Core 10 Identity supports passkeys |
| Two-way email sync and email sequences | SMTP send only | P2 | IMAP/Graph sync, sequences |
| Web forms / instant lead capture | No | P2 | Public lead-capture endpoint + embeddable form |
| Calendar and appointment scheduling | No | P2 | Calendar view over activities, booking link |
| Customer segmentation, tags, custom fields | Groups/categories only | P2 | Tags + custom fields per entity |
| Import / export (CSV, Excel), duplicate detection | No | P2 | Import wizard, merge duplicates |
| Workflow automation builder (no-code rules) | No | P2 | Rule engine UI on top of P1 automation hooks |
| Webhooks and public API keys | JWT API only | P2 | Webhook subscriptions, API keys |
| AI: deal health, next-best-action, email drafts, call notes | No | P3 | Provider-agnostic AI service (e.g. Claude API) |
| Mobile experience | Responsive pages | P3 | PWA manifest + offline shell |
| Multi-currency, localization, multi-tenant | No | P3 | Per-tenant settings |
| Backups | No | P3 | Scheduled DB backup job + health checks |

## Platform items done in the .NET 10 upgrade
- Target framework net10.0, EF Core 10, Identity 10, JwtBearer 10.
- AutoMapper 16.1.1 (fixes GHSA-rvv3-g6hj-g44x); optional `AutoMapper:LicenseKey` setting.
- MediatR stays on 12.x (13+ is commercial dual-licensed).
- Swashbuckle stays on 7.x (10.x needs Microsoft.OpenApi 2.x code changes; do when a compiler is at hand).

## Sources
- https://solguruz.com/blog/crm-features-list/
- https://www.onepagecrm.com/blog/crm-features/
- https://communicateonline.me/insights/hubspot-salesforce-race-towards-agentic-crm/
- https://advisories.gitlab.com/nuget/automapper/GHSA-rvv3-g6hj-g44x/
- https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/
