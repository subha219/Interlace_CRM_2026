# Roles and logins

A **role** in Interlace is the first part of a menu address (for example `Leads`, `SupportCases`, `Contracts`). A user sees a menu page only if they hold that role. A **job profile** is a ready-made set of these roles for a typical job.

## Sample logins (development)

Set in `appsettings.Development.json` (never in production):

```json
"Seed": { "SampleLogins": true, "SampleUserPassword": "Demo@123456" }
```

On start-up one login per profile is created (or topped up with missing roles; nothing is ever removed):

| Login | Profile | Can work with |
|---|---|---|
| `admin@root.com` (password from `AspNetIdentity:DefaultAdmin`) | Administrator | Everything, including users, roles, settings, audit log, integrations, AI governance |
| `salesrep@root.com` | Sales representative | Leads, pipeline, customers and contacts, quotations, sales orders, contracts and e-signatures, WhatsApp/SMS, calls, live chat, reports |
| `salesmanager@root.com` | Sales manager | The above plus quotation approvals, sales targets and teams, invoices and payments, campaigns, imports, pipeline settings, workflow rules, custom fields, AI agents |
| `supportagent@root.com` | Support agent | Support cases and comments, knowledge base, customers and contacts, contracts, WhatsApp/SMS, calls, live chat |
| `supportmanager@root.com` | Support manager | The above plus support settings (categories, SLA), reports, workflow rules, imports, e-signatures, AI agents |
| `marketing@root.com` | Marketing | Campaigns, budgets, leads, customers and groups, WhatsApp/SMS, reports, imports |
| `finance@root.com` | Finance | Invoices, payments, sales and purchase orders and reports, budgets, expenses, taxes, vendors, products, contracts |

Everybody also gets: Profiles, Dashboards, Notifications, Todos, Activities, Documents, Emails.

None of the profiles can open Users, Roles, Settings, Audit logs, Integrations, AI governance, Number sequences or Companies.

## Real users

Create them in **Users** and tick the roles, using the table above as a guide. A user must log out and in again after roles change. Single sign-on creates new users with the `Profiles` role only.

## Old demo users

The demo data also creates `alex@example.com` ... `sage@example.com` (password `123456`, `Profiles` role only). Delete them before going live.

## What each login sees on the dashboards and menu

Menu pages need the role of their section. The dashboards share one section (Dashboards), so each also lists the extra roles it needs (`PageAccess.cs`). The server applies the same rules to the data, not just the screen (`ModulePermissions.cs`).

| Page | Needs (besides Dashboards) |
|---|---|
| My Work | nothing extra, it shows the user's own work |
| Insights (today's briefing) | Pipelines, Quotations, Invoices or Payments. Revenue and receivables only with Invoices or Payments, open cases only with SupportCases |
| Default dashboard | any of Leads, Pipelines, Campaigns, Budgets, Expenses, SalesOrders, Invoices, PurchaseOrders; each card inside needs its own module (leads, campaigns, budgets/expenses, sales, purchases) |
| Support | SupportCases |
| Customer Health | Contracts |
| Data Quality | Leads, Customers, CustomerContacts or SupportCases |

After login each user lands on the first dashboard that fits: Insights, then Default, then Support, then My Work. The search box (Ctrl+K) and quick actions list only what the user may open.

Known limits: the Insights priority list can still mention items from several modules, and other screens still let any signed-in user read lookup lists (customers, products) because forms need them.

## AI agents

Page **AI Agents** (role `Agents`, given to the two manager profiles and the administrator). Four agents look for work and write *suggestions*; a person approves or rejects each one.

| Agent | What it suggests | Approver needs |
|---|---|---|
| Lead follow-up | A follow-up task for open leads with no activity for 7 days | Activities |
| Support triage | A new priority for new support cases (AI model if set up, keyword rules otherwise) | Support cases |
| Contract renewal | A renewal task for active, non-auto-renew contracts ending within their notice window with no renewal | Activities |
| Data quality | A task to complete customers and leads that have no e-mail or phone | Activities |

Agents start switched off. Running one twice does not repeat suggestions: a record with a pending suggestion, a rejection in the last 30 days or an approval in the last 7 days is skipped, as is a record with an open task. Model calls count against the limits on the AI Governance page.

## AI agents: categories and draft messages

- **Support triage** also suggests a category for a new case that has none (the AI model picks one of your active categories, or a category name found in the text). Approving sets the category and leaves a private comment.
- **Lead follow-up** and **Contract renewal** add a draft message to the task suggestion. The lead draft is written by the AI model when one is set up (no links allowed; otherwise a short template). The person approving can edit the text. Nothing is sent: on approval the draft is saved in the task's description to copy into an e-mail or chat.

## AI agents: running by themselves

On **Settings → AI Agents**, an agent that is switched on also has "Run by itself once a day". The server then runs it about every 23 hours (checked every 30 minutes, first check 2 minutes after start-up). Runs show as "Automatic". It still only writes suggestions; a person approves each one. Agents that use the AI model count against the AI limits.

## Scheduled reports: CSV, PDF or both

When scheduling a report, choose **Attach**: CSV (all rows, every language), PDF (first 2,000 rows, landscape A4, header on every page) or both. The PDF uses the built-in Helvetica font, so letters outside Western European languages (for example Hindi) print as "?" in the PDF; use the CSV for those.

## Calendar sync: Google or Outlook

Each user picks one calendar on **My Profile area → Calendar Sync**. Outlook / Microsoft 365 uses the Microsoft application saved under Settings → Connections (client, secret, tenant). In Azure, add the delegated permission `Calendars.ReadWrite` and the redirect address `/api/Calendar/OutlookCallback` (also listed on the Connections page). Sync is one-way, CRM to calendar, every 10 minutes. Switching calendars leaves the old events where they are.

## Connections (keys for outside services)

Each company enters its own keys under **Settings → Connections** (role `Integrations`, the administrator): Google (Calendar and Gmail), single sign-on, WhatsApp, SMS (MSG91), calls (Exotel) and the AI model. The page also shows the addresses to register with each provider.

- Secrets are stored encrypted with the application's data-protection keys (back up the `App_Keys` folder together with the database, or saved secrets cannot be read after a move). They are never sent back to the browser.
- A value saved on the page wins. If nothing is saved, the server configuration file or environment variables (`Google:ClientId` and so on) are still used, so existing set-ups keep working.
- Changes apply within about 30 seconds. Password sign-in can only be switched off once a complete single sign-on is set up.
