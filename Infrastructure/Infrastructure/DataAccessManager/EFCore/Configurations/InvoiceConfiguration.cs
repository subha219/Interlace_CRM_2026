using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class InvoiceConfiguration : BaseEntityConfiguration<Invoice>
{
    public override void Configure(EntityTypeBuilder<Invoice> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Number).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.InvoiceDate).IsRequired(false);
        builder.Property(x => x.InvoiceStatus).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.CustomerId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.TaxId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.BeforeTaxAmount).IsRequired(false);
        builder.Property(x => x.TaxAmount).IsRequired(false);
        builder.Property(x => x.AfterTaxAmount).IsRequired(false);
        builder.Property(x => x.DueDate).IsRequired(false);
        builder.Property(x => x.QuotationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.AmountPaid).IsRequired(false);
        builder.Property(x => x.BalanceDue).IsRequired(false);

        builder.HasIndex(e => e.Number);
    }
}

