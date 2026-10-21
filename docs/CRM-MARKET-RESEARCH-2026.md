# CRM market research (Oct 2026) and how it maps to Free CRM

Researched leading CRMs (Salesforce Agentforce Sales, HubSpot, Zoho, Pipedrive, Close, Monday CRM, SAP, Freshsales)
and 2026 trend/UX articles. Sources at the bottom.

## What the market is converging on

| Theme | What vendors ship | Applied in Free CRM |
|---|---|---|
| AI as infrastructure, not a side panel | Inline suggestions, auto-scoring on save, drafts the user edits, natural-language commands | Deal health score on every lead, next-best-action text, AI Assistant drawer (works without a key, uses Claude when a key is configured), email draft helper |
| Explainable AI | "Explain this score": factors, source (rule / AI / manual) | Every health score lists its factors with points; audit log already records changes |
| Agentic / rule-driven automation | Agents create records and tasks when a trigger fires, with approval options | Background alert job (done), insights engine turns signals into a prioritised "Today" list |
| Unified customer record | Customer 360, chronological activity timeline, one database | Customer 360 page: lifetime value, open balance, quotes, invoices, orders, cases and merged timeline |
| Forecasting and deal health | Weighted forecast, at-risk deals, stage stagnation, buying-committee breadth | Forecast page (done), health model uses stage, BANT, closing date, activity recency, contact breadth |
| Role-focused dashboards: "what should I do now?" | KPI + trend + target, tiles link to filtered detail, urgency colour used sparingly | New Insights home: Today list, KPI tiles with sparklines, at-risk deals, pipeline, receivables ageing |
| Command palette + unified search | Ctrl/Cmd+K, fuzzy search, recents, actions and navigation in one list | Global search API + Ctrl+K palette (pages, records, quick actions) |
| Calm, minimal UI, dark mode | Lean default views, typography-led hierarchy, one primary action, consistent palette, 4.5:1 contrast | New design layer: Inter, tokens, soft cards, refined sidebar, light/dark toggle, accessible badges (never colour alone) |
| Real-time alerts | Overdue tasks, stage stagnation, SLA, delivered where the user is | Bell + alert job (done) |
| Governance | Audit trail, role-based views | Audit log (done), role menus (done) |

## Still open (P2/P3)
Two-way email/calendar sync, call recording and transcripts, sentiment analysis, web-to-lead forms,
no-code workflow builder UI, public API keys/webhooks, 2FA/passkeys, multi-currency, multi-tenant, mobile PWA.

## Sources
- https://www.4crms.com/blog/crm-trends-2026-2027
- https://www.solution4guru.com/crm-functionality-top-crm-features-in-2026/
- https://www.lazarev.agency/articles/crm-dashboard-design
- https://www.fullcast.ai/content/ai-in-deal-scoring
- https://www.g2.com/articles/best-crm-software
- https://saasui.design/blog/7-saas-ui-design-trends-2026
- https://www.taskade.com/blog/best-ai-crm-software
