using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class QuotationConfiguration : BaseEntityConfiguration<Quotation>
{
    public override void Configure(EntityTypeBuilder<Quotation> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Number).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.QuotationDate).IsRequired(false);
        builder.Property(x => x.QuotationStatus).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.CustomerId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.TaxId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.BeforeTaxAmount).IsRequired(false);
        builder.Property(x => x.TaxAmount).IsRequired(false);
        builder.Property(x => x.AfterTaxAmount).IsRequired(false);
        builder.Property(x => x.DiscountPercent).IsRequired(false);
        builder.Property(x => x.DiscountAmount).IsRequired(false);
        builder.Property(x => x.ApprovalStatus).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.ApprovalRequestedById).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.ApprovalDecidedById).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.ApprovalDecidedAtUtc).IsRequired(false);
        builder.Property(x => x.ApprovalNote).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.RevisionNumber).IsRequired(false);
        builder.Property(x => x.ParentQuotationId).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.ValidUntil).IsRequired(false);
        builder.Property(x => x.ConvertedInvoiceId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ConvertedSalesOrderId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.Number);
    }
}

