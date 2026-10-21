using Domain.Entities;
using Domain.Enums;

namespace Application.Features.QuotationManager;

public static class QuotationPolicy
{
    public const string None = "None", Required = "Required", Pending = "Pending", Approved = "Approved", Rejected = "Rejected";

    public static bool NeedsApprovalBeforeSending(Quotation q) =>
        q.ApprovalStatus == Required || q.ApprovalStatus == Pending || q.ApprovalStatus == Rejected;

    /// <summary>Sent, accepted and converted quotations must not carry an unapproved discount.</summary>
    public static void EnsureCanProceed(Quotation q, QuotationStatus? target)
    {
        if ((target is QuotationStatus.Sent or QuotationStatus.Accepted or QuotationStatus.Converted) && NeedsApprovalBeforeSending(q))
        {
            throw new Exception(q.ApprovalStatus == Rejected
                ? "The discount on this quotation was rejected. Lower the discount or create a revision."
                : "This quotation's discount needs approval before it can be sent or converted.");
        }
    }

    /// <summary>Share of the item subtotal that remains after the discount, used when copying items to an invoice or order.</summary>
    public static double NetFactor(Quotation q)
    {
        var subtotal = q.QuotationItemList.Where(i => !i.IsDeleted).Sum(i => i.Total ?? 0);
        if (subtotal <= 0 || q.BeforeTaxAmount == null) return 1;
        return Math.Clamp(q.BeforeTaxAmount.Value / subtotal, 0, 1);
    }
}
