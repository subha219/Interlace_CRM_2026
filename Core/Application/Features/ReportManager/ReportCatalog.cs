using Application.Common.CQS.Queries;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReportManager;

/// <summary>What can be reported on, and how each record type is loaded.</summary>
public static class ReportCatalog
{
    public static readonly Dictionary<string, string> ModuleRole = new()
    {
        ["Lead"] = "Leads", ["Customer"] = "Customers", ["SupportCase"] = "SupportCases", ["Quotation"] = "Quotations", ["Activity"] = "Activities"
    };

    public static readonly Dictionary<string, string> Labels = new()
    {
        ["Lead"] = "Leads", ["Customer"] = "Customers", ["SupportCase"] = "Support cases", ["Quotation"] = "Quotations", ["Activity"] = "Activities"
    };

    public static readonly Dictionary<string, List<ReportField>> Fields = new()
    {
        ["Lead"] = new()
        {
            new("number", "Number", "text"), new("title", "Title", "text"), new("company", "Company", "text"), new("email", "E-mail", "text"),
            new("stage", "Stage", "text"), new("status", "Closing status", "text"), new("amountTargeted", "Target amount", "number"), new("amountClosed", "Closed amount", "number"),
            new("source", "Source", "text"), new("tags", "Tags", "text"), new("team", "Sales team", "text"), new("owner", "Owner", "user"),
            new("expectedClose", "Expected close", "date"), new("closedOn", "Closed on", "date"), new("createdAt", "Created", "date")
        },
        ["Customer"] = new()
        {
            new("number", "Number", "text"), new("name", "Name", "text"), new("email", "E-mail", "text"), new("phone", "Phone", "text"),
            new("lifecycle", "Lifecycle stage", "text"), new("source", "Source", "text"), new("tags", "Tags", "text"), new("group", "Group", "text"),
            new("owner", "Owner", "user"), new("createdAt", "Created", "date")
        },
        ["SupportCase"] = new()
        {
            new("number", "Number", "text"), new("title", "Title", "text"), new("status", "Status", "text"), new("priority", "Priority", "text"),
            new("channel", "Channel", "text"), new("customer", "Customer", "text"), new("owner", "Owner", "user"),
            new("dueDate", "Due", "date"), new("resolvedAt", "Resolved", "date"), new("createdAt", "Created", "date")
        },
        ["Quotation"] = new()
        {
            new("number", "Number", "text"), new("customer", "Customer", "text"), new("status", "Status", "text"), new("approval", "Approval", "text"),
            new("subtotal", "Net amount", "number"), new("amount", "Total with tax", "number"), new("discountPercent", "Discount %", "number"),
            new("owner", "Owner", "user"), new("quotationDate", "Quotation date", "date"), new("validUntil", "Valid until", "date")
        },
        ["Activity"] = new()
        {
            new("number", "Number", "text"), new("subject", "Subject", "text"), new("type", "Type", "text"), new("status", "Status", "text"),
            new("priority", "Priority", "text"), new("relatedType", "Related to", "text"), new("relatedName", "Record", "text"),
            new("assignee", "Assigned to", "user"), new("owner", "Owner", "user"), new("startAt", "Start", "date"), new("completedAt", "Completed", "date")
        }
    };

    public static async Task<List<Dictionary<string, object?>>> FetchAsync(IQueryContext c, string entity, CancellationToken ct)
    {
        var take = ReportEngine.MaxRows;
        switch (entity)
        {
            case "Lead":
            {
                var rows = await c.Lead.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAtUtc).Take(take)
                    .Select(x => new { x.Number, x.Title, x.CompanyName, x.CompanyEmail, x.PipelineStage, x.ClosingStatus, x.AmountTargeted, x.AmountClosed, x.LeadSource, x.Tags, Team = x.SalesTeam != null ? x.SalesTeam.Name : null, x.OwnerUserId, x.DateClosingEstimation, x.DateClosingActual, x.CreatedAtUtc })
                    .ToListAsync(ct);
                return rows.Select(x => new Dictionary<string, object?>
                {
                    ["number"] = x.Number, ["title"] = x.Title, ["company"] = x.CompanyName, ["email"] = x.CompanyEmail, ["stage"] = x.PipelineStage?.ToString(),
                    ["status"] = x.ClosingStatus?.ToString(), ["amountTargeted"] = x.AmountTargeted, ["amountClosed"] = x.AmountClosed, ["source"] = x.LeadSource,
                    ["tags"] = x.Tags, ["team"] = x.Team, ["owner"] = x.OwnerUserId, ["expectedClose"] = x.DateClosingEstimation, ["closedOn"] = x.DateClosingActual, ["createdAt"] = x.CreatedAtUtc
                }).ToList();
            }
            case "Customer":
            {
                var rows = await c.Customer.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAtUtc).Take(take)
                    .Select(x => new { x.Number, x.Name, x.EmailAddress, x.PhoneNumber, x.LifecycleStage, x.Source, x.Tags, Group = x.CustomerGroup != null ? x.CustomerGroup.Name : null, x.OwnerUserId, x.CreatedAtUtc })
                    .ToListAsync(ct);
                return rows.Select(x => new Dictionary<string, object?>
                {
                    ["number"] = x.Number, ["name"] = x.Name, ["email"] = x.EmailAddress, ["phone"] = x.PhoneNumber, ["lifecycle"] = x.LifecycleStage?.ToString(),
                    ["source"] = x.Source, ["tags"] = x.Tags, ["group"] = x.Group, ["owner"] = x.OwnerUserId, ["createdAt"] = x.CreatedAtUtc
                }).ToList();
            }
            case "SupportCase":
            {
                var rows = await c.SupportCase.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAtUtc).Take(take)
                    .Select(x => new { x.Number, x.Title, x.Status, x.Priority, x.Channel, Customer = x.Customer != null ? x.Customer.Name : null, x.OwnerUserId, x.DueDate, x.ResolvedAtUtc, x.CreatedAtUtc })
                    .ToListAsync(ct);
                return rows.Select(x => new Dictionary<string, object?>
                {
                    ["number"] = x.Number, ["title"] = x.Title, ["status"] = x.Status?.ToString(), ["priority"] = x.Priority?.ToString(), ["channel"] = x.Channel?.ToString(),
                    ["customer"] = x.Customer, ["owner"] = x.OwnerUserId, ["dueDate"] = x.DueDate, ["resolvedAt"] = x.ResolvedAtUtc, ["createdAt"] = x.CreatedAtUtc
                }).ToList();
            }
            case "Quotation":
            {
                var rows = await c.Quotation.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAtUtc).Take(take)
                    .Select(x => new { x.Number, Customer = x.Customer != null ? x.Customer.Name : null, x.QuotationStatus, x.ApprovalStatus, x.BeforeTaxAmount, x.AfterTaxAmount, x.DiscountPercent, x.OwnerUserId, x.QuotationDate, x.ValidUntil })
                    .ToListAsync(ct);
                return rows.Select(x => new Dictionary<string, object?>
                {
                    ["number"] = x.Number, ["customer"] = x.Customer, ["status"] = x.QuotationStatus?.ToString(), ["approval"] = x.ApprovalStatus ?? "None",
                    ["subtotal"] = x.BeforeTaxAmount, ["amount"] = x.AfterTaxAmount, ["discountPercent"] = x.DiscountPercent ?? 0, ["owner"] = x.OwnerUserId,
                    ["quotationDate"] = x.QuotationDate, ["validUntil"] = x.ValidUntil
                }).ToList();
            }
            case "Activity":
            {
                var rows = await c.Activity.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAtUtc).Take(take)
                    .Select(x => new { x.Number, x.Subject, x.Type, x.Status, x.Priority, x.RelatedType, x.RelatedName, x.AssignedToUserId, x.OwnerUserId, x.StartAt, x.CompletedAt })
                    .ToListAsync(ct);
                return rows.Select(x => new Dictionary<string, object?>
                {
                    ["number"] = x.Number, ["subject"] = x.Subject, ["type"] = x.Type?.ToString(), ["status"] = x.Status?.ToString(), ["priority"] = x.Priority,
                    ["relatedType"] = x.RelatedType, ["relatedName"] = x.RelatedName, ["assignee"] = x.AssignedToUserId, ["owner"] = x.OwnerUserId,
                    ["startAt"] = x.StartAt, ["completedAt"] = x.CompletedAt
                }).ToList();
            }
            default:
                throw new Exception("Unknown record type.");
        }
    }
}
