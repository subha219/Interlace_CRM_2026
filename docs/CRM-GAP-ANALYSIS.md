# CRM Gap Analysis: Requirements Spec vs. Current Codebase

_Generated 2026-10-18 from a read-through of the project (entities, features, controllers, pages, menu, infrastructure, config). Companion to `CRM-FEATURE-ROADMAP.md` and `CRM-MARKET-RESEARCH-2026.md`._

**Legend:** ✅ implemented · 🟡 partial · ❌ missing · 🔌 needs a third-party provider/account (cannot be "just coded") · ⛔ removal candidate

---

## 1. What the project is today

| Area | Reality in the code |
|---|---|
| Stack | .NET 10, EF Core 10 (SQL Server), ASP.NET Identity + JWT, MediatR/CQRS, AutoMapper, FluentValidation, Serilog; Razor pages + Vue 3 + Syncfusion grids |
| Entities (39) | Lead, LeadContact, LeadActivity, Campaign, Budget, Expense, Customer, CustomerContact, CustomerGroup/Category, Company, SalesTeam, SalesRepresentative, Product, ProductGroup, UnitMeasure, Tax, Quotation(+Item), SalesOrder(+Item), Invoice(+Item), Payment, SupportCase, SupportCaseComment, Todo(+Item), Notification, AuditLog, FileDocument, FileImage, NumberSequence, Token, Vendor*, PurchaseOrder(+Item) |
| Key design fact | **A "Lead" is also the opportunity/deal**: one table carries BANT scores, pipeline stage, closing status and amounts. There is no separate Opportunity and no Lead→Contact/Account conversion. |
| Security model | Roles = one per top-level menu segment (Pipeline, Support, Sales ...). They are enforced **in the browser only**. All 215 API endpoints carry a plain `[Authorize]`, so any logged-in user can call any endpoint. |
| Intelligence | Rule-based deal health score and next action, weighted forecast, rule-based lead score, assistant drawer (optional Claude LLM, rule-based fallback, read-only). No ML models. |
| Automation | Only hard-coded: auto-assign new lead to least-loaded team, and a background service that raises in-app alerts (overdue SLA cases, slipped closing dates, overdue invoices). No configurable workflow engine. |
| Quality gates | No automated tests, no CI/CD, no health checks, no rate limiting, no caching, list endpoints return all rows (grid pages client-side). |

---

## 2. Section-by-section matrix

### 2. Users, roles & access
| Requirement | Status | Notes / action |
|---|---|---|
| Role list (Sales Manager, Support Agent, Finance ...) | 🟡 | Roles exist but are menu-based, not job roles. Create the job roles as permission templates. |
| RBAC enforced on backend (module/page/record/action) | ❌ | **P0.** Needs policy-based authorization per controller action (Read/Create/Update/Delete/Export/Import/Approve). |
| Record ownership, team & manager visibility, territory access | ❌ | No OwnerId on Lead/Customer/Case/Quote. **P0** before any "my records" filter works. |
| Field-level permissions | ❌ | P2. |
| MFA (TOTP) | ❌ | Identity supports it; add enrol/verify flow. P0/P1. |
| SSO (Google/Microsoft/SAML/OIDC) | ❌ 🔌 | P3 (needs IdP). |
| Lockout, email confirmation, password policy | ✅ | 5 attempts / 30 min lockout; confirmed-email required. Tighten password policy (currently length 6, no complexity). |
| Session mgmt, token refresh | 🟡 | JWT 30 min + RefreshToken command exist; no device/session list or revoke. |
| Invitation-based onboarding | 🟡 | Admin creates user + email confirm; no invite link/flow. |
| User activity / security audit log | 🟡 | Data-change audit log exists (AuditLog + interceptor + page). Login/logout/permission events not logged. |
| Auto-deactivation, temporary access | ❌ | P2. |

### 3. Lead management
| Requirement | Status | Notes / action |
|---|---|---|
| Manual create, edit, list | ✅ | Lead list + form, company/social fields, amounts, dates. |
| Bulk import CSV/Excel | 🟡 | Import wizard (preview, duplicate check) exists for **customers only**. Extend to leads/contacts. |
| Web forms, landing pages, API capture, email-to-lead | ❌ | Public lead-capture endpoint + embeddable form: P1. Email-to-lead 🔌. |
| WhatsApp/ads/social/chatbot capture, business-card OCR | ❌ 🔌 | Provider-dependent; P3. |
| Lead source, campaign attribution | 🟡 | Campaign link only. Add LeadSource field/list. P1. |
| Custom fields, tags | ❌ | Needs a custom-field framework (see §21). P1/P2. |
| Lead profile fields | 🟡 | Missing: designation, industry, company size, budget text, timeline, consent/communication preferences, individual owner. |
| BANT qualification | ✅ | Four scores on the lead. Add a qualification questionnaire UI. |
| Lead scoring (rules) | ✅ | `LeadScoring` + deal health. |
| AI/ML scoring, intent detection | 🟡 | Explainable rule model only; ML needs history volume. P3. |
| Hot / warm / cold | 🟡 | Health labels (Healthy / Needs attention / At risk) exist; add Hot/Warm/Cold on the score. Small. |
| Duplicate detection | 🟡 | Customers at import only. Add on lead/contact create + merge. P1. |
| Enrichment | ❌ 🔌 | Provider needed. |
| Disqualification reasons | 🟡 | `ClosingNote` free text; add reason list. |
| Assignment: manual, workload-based | 🟡 | Auto-assign to least-loaded **team**; no individual rep assignment. |
| Round-robin, territory, product/industry/skill routing | ❌ | P1 (needs territories, §18). |
| Reassign inactive, escalate uncontacted, ownership history | ❌ | Becomes workflow rules (§10). |
| Lead → Contact + Account + Opportunity conversion | ❌ | **P0 model decision** (see Open Questions). |

### 4. Contacts & accounts
| Requirement | Status | Notes |
|---|---|---|
| Customer (account) + multiple contacts, job title, phone/email | ✅ | Customer, CustomerContact; groups/categories. |
| Parent/subsidiary, branches, tiers, relationship map | ❌ | P2. |
| Tags, notes, attachments, timeline | 🟡 | FileDocument exists but not wired to records; no tags/notes; timeline only in Customer 360 aggregation. |
| Consent / opt-out, communication preferences | ❌ | **P1** (needed for DPDP compliance and any outbound messaging). |
| Merge duplicates | ❌ | P1. |
| Import / export | ✅ | Import customers; Excel export from grids. |
| Birthday/anniversary reminders | ❌ | P3. |
| Customer 360 | 🟡 | Leads, quotes, invoices, payments, cases, health, summary. Missing: emails/calls/meetings, documents, consent, CLV, churn risk. |

### 5. Pipeline & opportunities
| Requirement | Status | Notes |
|---|---|---|
| Kanban drag-and-drop, list view | ✅ | Pipeline Board + Lead list. |
| Multiple configurable pipelines, custom stages, stage probability | ❌ | Stages are a fixed enum; probabilities in code. P1: pipeline/stage tables + admin UI. |
| Entry/exit criteria, stage approvals, auto-transitions | ❌ | P2 (workflow/approval engine). |
| Time in stage, stage conversion analytics, pipeline history | ❌ | Needs stage-history table. P1. |
| Opportunity fields | 🟡 | Have: value, close date, stage, owner team, notes. Missing: products/line items, competitors, buying committee, objections, next-action date, win/loss reason list, reopen. |
| Deal health, at-risk, next best action | ✅ | `DealHealthService`. |
| Stalled / overdue / no-engagement detection | 🟡 | Momentum factor exists; no stalled alert, no close-date slippage trend. |
| Similar won-deal comparison, historical patterns | ❌ | P3. |
| Quotations: create, items, tax, status, PDF, convert to order/invoice | ✅ | |
| Quote versions, discounts, price lists, approval, e-sign, expiry reminders | ❌ | P2. Expiry reminder is easy (alert service). |
| Proposal documents | ❌ | P2 (template engine). |

### 6. Activities & tasks
| Requirement | Status | Notes |
|---|---|---|
| Tasks / to-dos | 🟡 | Todo/TodoItem are a **generic list not linked to customers/deals**. Replace with record-linked Activities (⛔ see removals). |
| Call / email / meeting / visit logging | 🟡 | LeadActivity has types and dates, only on leads. Generalise across records. |
| Reminders, overdue alerts | 🟡 | In-app notification bell only. |
| Calendar views, scheduling, recurring tasks, dependencies | ❌ | P1 (calendar) / P2 (recurrence, dependencies). |
| Daily agenda / work queue | 🟡 | Insights "What should I do now" covers part. |
| Manager review, activity reports | ❌ | P2. |

### 7. Email, telephony, unified communication
| Requirement | Status | Notes |
|---|---|---|
| Send system email (confirm, reset) | ✅ | SMTP service. |
| CRM email: send/log, templates, variables, signatures, threading, shared inbox, tracking | ❌ 🔌 | P1 send + templates + logging via SMTP; mailbox sync (Gmail/Graph) is P2 🔌. |
| Telephony, call recording, transcripts | ❌ 🔌 | Provider choice needed. |
| SMS, live chat, social messaging | ❌ 🔌 | Provider choice needed. |
| Unified timeline | ❌ | Build after activities are generalised. |

### 8. WhatsApp Business
❌ 🔌 Whole section. Only a WhatsApp number field exists on lead/customer. Needs a BSP (Meta Cloud API / Twilio / Gupshup...) decision. Phase 4+.

### 9. Marketing automation
| Requirement | Status | Notes |
|---|---|---|
| Campaigns with budget, expenses, linked leads | ✅ | Campaign, Budget, Expense, charts. |
| Email/SMS/WhatsApp campaign sending, scheduling | ❌ 🔌 | Needs email provider + consent (§4). |
| Segments (static/dynamic), saved audiences | ❌ | P2. Depends on saved filters/query engine. |
| A/B tests, UTM, attribution, ROI, cost per lead | ❌ | UTM + cost per lead are cheap given budgets/expenses/leads: P2. |
| Nurture/drip workflows, suppression, frequency caps | ❌ | Needs workflow engine. |

### 10. Workflow automation
❌ Entire visual builder (triggers, conditions, actions, delays, branches, versioning, logs, retries, SLA timers, approvals). Only hard-coded alerts exist. **Largest single build** (P2): data model → rule engine → execution log/retry → builder UI. Ship a rule list (trigger → conditions → actions) first; visual canvas later.

### 11. AI
| Requirement | Status | Notes |
|---|---|---|
| Copilot: Q&A over CRM data, today's briefing, next best action | 🟡 | Assistant drawer + insights, read-only. |
| Citations / links to records, permission-aware, confirmation for actions | 🟡 | Links partly; no action tools, no confirmations. |
| Draft emails, summaries, proposals, task creation from NL | ❌ | P3 (needs email + activities first). |
| Agents (qualification, enrichment, triage, renewal, data quality ...) | ❌ | P3/P4, after workflow engine + approvals + audit. |
| Predictive: win probability, forecast | 🟡 | Stage-weighted forecast; no churn/CLV/slippage model. Do not fabricate: needs real history. |
| AI governance: provider config, usage limits, prompt versions, audit, PII masking | ❌ | P3. Add a usage/audit table before widening AI features. |

### 12. Customer service
| Requirement | Status | Notes |
|---|---|---|
| Cases with number, status, priority, channel, due date, resolution, comments | ✅ | SupportCase + comments. |
| Categories, severity, queues, routing, SLA policies, escalation | ❌ | Due-date breach alert exists; need SLA policy table + timers. P2. |
| Merge/split, related, duplicates, ownership history | ❌ | P3. |
| Email-to-ticket, chat, WhatsApp | ❌ 🔌 | |
| Knowledge base, canned responses, CSAT, portal | ❌ | P2/P3. |
| Support dashboard, backlog, agent productivity | ❌ | P2 (data exists). |
| Sentiment, repeat-issue detection | ❌ | P4. |

### 13. Customer success & retention
❌ Contracts/renewals, customer health, onboarding plans, churn alerts, playbooks, CLV. Note: deal health exists but is per-deal, not per-customer. P2/P3. Requires a Contract entity first.

### 14. Products, pricing, revenue
| Requirement | Status | Notes |
|---|---|---|
| Product catalog, groups, units, tax, numbering | ✅ | |
| Price books, bundles, customer pricing, discount rules | ❌ | P2. |
| Recurring/subscription products, contracts | ❌ | P2/P3. |
| Revenue by product/customer/rep, margin | 🟡 | Sales report page; add rep/customer/product pivots. |
| Commission | ❌ | P3. |
| Orders, invoices, payments | 🟡 | Built in-app (see removals/decision). Spec treats them as integrations. |

### 15. Reporting & BI
| Requirement | Status | Notes |
|---|---|---|
| Executive dashboard | ✅ | Command Center + Business overview (new). |
| Sales manager / rep / marketing / support / success / retention dashboards | ❌ | Role-based dashboards: P1 (sales rep, manager), P2 rest. |
| KPIs: pipeline, weighted pipeline, win rate, forecast | ✅ | |
| Sales cycle, avg deal size, target vs actual, coverage, stage aging | ❌ | Needs targets/quotas (§18) + stage history. |
| Report builder, saved/scheduled/emailed reports | ❌ | P2. |
| Cohort/funnel/drill-down | ❌ | Funnel exists as chart; true analysis P3. |
| Export CSV/Excel | ✅ | Grid export. |
| NL analytics | 🟡 | Assistant covers simple questions. |

### 16. Calendar & meetings
❌ Calendar UI, Google/Outlook sync 🔌, booking links, availability, invites, no-show. Syncfusion Scheduler is already licensed in the bundle: internal calendar is quick (P1); external sync is P2 🔌.

### 17. Documents & contracts
🟡 FileDocument entity, upload manager and controller exist but are not attached to records and have no versions/permissions. Contracts, e-sign 🔌, OCR, retention: ❌. P1 attach-to-record; P2 contracts.

### 18. Territory, team, performance
🟡 SalesTeam + SalesRepresentative only. Missing: hierarchy, territories, quotas/targets, leaderboards, coaching, workload allocation. **Targets are prerequisite for several reports.** P1/P2.

### 19. Customer portal
❌ Entire section (separate auth, scoped views). P3 after tickets, KB and quotes are solid.

### 20. Integration & developer platform
| Requirement | Status | Notes |
|---|---|---|
| REST API + Swagger, JWT | ✅ | |
| Pagination/filtering | ❌ | **P0** server-side paging contract (current endpoints return everything). |
| API keys, OAuth client credentials, scopes | ❌ | P2. |
| Webhooks with retry, event log | ❌ | P2 (shares outbox with workflow engine). |
| Rate limiting, versioning | ❌ | Rate limiting P0 (one-liner middleware); versioning P2. |
| Integration health, marketplace | ❌ | P3/P4. |
| Provider integrations (Google, Microsoft, WhatsApp, telephony, SMS, e-sign, accounting, ERP, payment) | ❌ 🔌 | Each needs an account/decision. |

### 21. Administration & configuration
| Requirement | Status |
|---|---|
| Company settings, tax, number sequences, users, roles | ✅ |
| Logo/branding, locale/currency/time zone, business hours/holidays | ❌ (company currency string only) |
| Custom fields, custom modules, layouts, picklists, validation rules | ❌ **P1/P2: custom fields + picklists first** |
| Email/notification templates, approval config, workflow config | ❌ |
| Config change history | 🟡 (audit log covers data, not settings pages) |
| Multi-organization / tenants, feature enablement by plan | ❌ (decision needed) |

### 22. Notifications
| Requirement | Status |
|---|---|
| In-app notification centre, background alerts (SLA, slipped close, overdue invoice) | ✅ |
| Email notifications, daily/weekly digests, preferences, dedup, delivery history | ❌ (dedup partly in alert service) |
| Push (web/mobile) | ❌ 🔌 |

### 23. Mobile & field sales
🟡 Responsive layouts (new design system). ❌ PWA/offline sync, check-in, business-card scan, voice entry, route planning. Recommend PWA (installable + offline cache for read) in P2; native app out of scope.

### 24. Data import/export/quality
| Requirement | Status |
|---|---|
| Customer CSV import with preview, validation, duplicates | ✅ |
| Import for leads/contacts/products, mapping wizard, error report, background jobs | ❌ |
| Export with permissions, scheduled export | 🟡 (grid export, no permission filter) |
| Data-quality dashboard (incomplete, stale, duplicate) | ❌ (cheap with existing data: P1) |
| Backup/restore, retention, deletion workflow | ❌ (ops + privacy workflow) |

### 25. Security & compliance
| Requirement | Status |
|---|---|
| Password hashing, lockout, JWT, HTTPS/HSTS | ✅ |
| Input validation (FluentValidation) | 🟡 (present on commands; verify coverage) |
| **Secrets in `appsettings.json`** (DB password, JWT key, SMTP, API key placeholders) | ❌ **P0**: move to user-secrets / environment variables, rotate the committed values |
| Rate limiting, CSRF review, file-upload hardening | ❌ P0/P1 |
| Encryption at rest, tenant isolation, data residency | ❌ (infra/decision) |
| Consent, privacy requests (DPDP Act), retention/deletion | ❌ P1/P2 |
| MFA/SSO | ❌ |
| Compliance claims | n/a Do not claim certification. |

### 26. Reliability & scalability
| Requirement | Status |
|---|---|
| Structured logging (Serilog) | ✅ |
| EF migrations + legacy upgrader | ✅ |
| Background processing | 🟡 (one hosted service; no queue/outbox/retries) |
| Server-side paging, indexes review, caching | ❌ P0/P1 |
| Health checks, error tracking, monitoring | ❌ P0 (health checks), P1 rest |
| Automated tests, CI/CD, staging, feature flags, rollback | ❌ **P0** test project + CI |
| Backups/DR, load tests | ❌ (ops) |

### 27. Differentiators
Relationship maps ❌ · buying committee ❌ · revenue intelligence 🟡 (forecast only; add best-case/committed, gap, coverage) · customer intelligence ❌ (needs contracts/health) · data intelligence 🟡 (duplicate suggestions on import; add missing-data and stale detection) · vertical modules ❌ (design as optional later).

### 28. UI/UX
| Requirement | Status |
|---|---|
| Modern SaaS look, light/dark, responsive | ✅ (new design system) |
| Global search, command palette, keyboard shortcut | ✅ (Ctrl/Cmd+K) |
| Excel-style filters, paging, row actions, column chooser | ✅ (shared grid enhancer) |
| Kanban + table + detail views | 🟡 (Customer 360 and pipeline board; no detail pages for others) |
| Quick-create, saved views, bulk actions, inline edit | ❌ P1 |
| Recent/favourites/pinned, personal work queue, configurable home | ❌ P1/P2 |
| Unsaved-change warnings, onboarding, a11y audit | ❌ P2 |
| Spec's main navigation (Dashboard, Leads, Contacts, Accounts, Opportunities, Activities, Campaigns, Communication, Support, Success, Products & Quotes, Reports, AI, Automation, Integrations, Admin) | ❌ Current menu is ERP-style (Pipeline, Third Party, Sales, Purchase, Inventory, Utilities ...). Re-organise (P1, cosmetic + role keys). |

### 29. SaaS subscription management
❌ Only relevant if you sell this as SaaS. Recommend **defer** and decide hosting model first.

---

## 3. Removal / cleanup candidates (nothing has been deleted)

| # | Item | Why it is outside the spec | Risk / dependencies | Recommendation |
|---|---|---|---|---|
| R1 | **Purchase module**: PurchaseOrder, PurchaseOrderItem, Purchase Report page, controllers, features, seeder, menu "Purchase", purchase widgets on Business overview | Procurement is ERP, not CRM; spec §14 says accounting/inventory/payments are integrations | Needs a migration to drop tables; dashboards reference it | **Remove** |
| R2 | **Vendor group**: Vendor, VendorCategory, VendorGroup, VendorContact | Only used by purchase orders | Remove together with R1 | **Remove** |
| R3 | Todo, TodoItem ("Utilities") | Generic list not tied to customers/deals; replaced by record-linked Activities | Migrate any rows, then drop | **Replace** during Activities build |
| R4 | Menu groups "Third Party", "Inventory", "Utilities", "Membership" naming | ERP naming | Role keys derive from first URL segment, so renaming means a role sync | **Rename** (Accounts, Products & Pricing, Admin) |
| R5 | Sales Order (+Items) and Invoice/Payment | Spec treats order/invoice/payment as integration | They are built and used by quote conversion, receivables alerts, Finance role | **Keep as optional module** behind a feature flag |
| R6 | Housekeeping: `_to_delete/incomplete_backup_0553.tar.gz`, `_backup_before_net10_...tar.gz`, `InterlaceUI.csproj.user`, unused AdminLTE demo scripts (`wwwroot/adminlte/js/pages/dashboard*.js`) | Dead weight | None | **Delete** once you confirm the .NET 10 build is stable |
| R7 | Demo-data seeders and `IsDemoVersion` flag | Fine for demos, risky in production | Gate seeders by environment | **Gate**, don't delete |
| R8 | Committed credentials in `appsettings.json` | Security | Rotate DB password and JWT key after moving | **Fix (P0)** |

---

## 4. Proposed build order (each milestone ships working, tested features)

| Milestone | Scope | Maps to spec phase |
|---|---|---|
| **M0 Foundations (P0)** | Backend permission enforcement + job-role templates; record ownership (OwnerId) on Lead/Customer/Case/Quote; server-side paging/filter/sort contract; secrets out of config; rate limiting, health checks; MFA (TOTP); login/security audit; test project + CI | §2, 20, 25, 26 |
| **M1 Core CRM completeness (P1)** | Lead→Customer/Contact/Opportunity conversion (per model decision); lead source/tags; consent; duplicate detect + merge; lead/contact import; generalised Activities (tasks/calls/meetings/notes, calendar view, reminders); CRM email send + templates + logging; documents attached to records; data-quality dashboard; saved views + bulk actions; role dashboards (rep, manager); menu re-organisation; cleanup R1-R4 | MVP |
| **M2 Sales productivity (P1/P2)** | Configurable pipelines/stages + stage history; targets/quotas/territories + leaderboard; quote versions, discounts, price books, approval; workflow rule engine + execution log + approvals; webhooks + API keys; report builder + scheduled reports; custom fields + picklists; Google/Microsoft calendar 🔌 | Phase 2 |
| **M3 Service & success (P2)** | SLA policies/escalations, categories/queues, KB, CSAT, support dashboard; contracts/renewals, customer health, churn alerts; customer portal | Phase 3 |
| **M4 AI (P2/P3)** | Copilot actions with confirmation and citations; email/meeting drafts and summaries; AI governance (usage, audit, limits); controlled agents; predictive models only once data volume allows | Phase 3 |
| **M5 Channels 🔌** | WhatsApp, SMS, telephony, live chat, marketing sends, e-sign: each after a provider decision | Phase 2-4 |
| **M6 Enterprise** | SSO, multi-tenant (if chosen), custom modules, marketplace, DR, SaaS billing (if SaaS), vertical modules | Phase 4 |

**Testing rule for every milestone:** unit tests for the handlers, API integration tests for permissions (including "user without role is rejected"), and a smoke checklist for the UI, run in CI before merge.

---

## 5. Open questions (answers change the design)

1. **Lead vs Opportunity:** keep today's single "Lead = deal" record and add lifecycle stages + a "Convert" action (cheaper, recommended), or split into Lead and Opportunity tables (cleaner, a bigger migration)?
2. **Multi-tenant?** One company per install (as now) or many organisations in one database (affects every table and query)?
3. **Hosting / SaaS?** Is this sold as SaaS (then §29 and tenancy matter) or deployed per customer?
4. **Providers:** which WhatsApp BSP, SMS gateway, telephony, e-sign, mailbox (Gmail/Outlook), calendar, accounting/ERP, and which AI model provider do you want to support first?
5. **Purchase/Vendor modules:** confirm R1/R2 removal (and whether any data must be exported first).
6. **Orders/Invoices/Payments:** keep as built-in optional module (recommended) or reduce to integration only?
7. **Priority:** which of M1's items matter most to your first customers?
8. **Compliance:** DPDP consent and data-deletion workflows needed at launch?

## 6. Risks

- Permission enforcement touches every controller: do it first, with tests, or later features inherit the hole.
- Splitting Lead/Opportunity or adding tenancy late is expensive; decide before M1.
- Workflow engine and AI agents multiply side effects; they need approvals, logs and idempotency from day one.
- I cannot compile or run the .NET app in this environment, so each milestone is verified by static checks and your build/run feedback. Adding the test project and CI (M0) closes that gap.
