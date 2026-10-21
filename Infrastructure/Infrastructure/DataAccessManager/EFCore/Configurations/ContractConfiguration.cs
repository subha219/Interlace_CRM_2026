using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ContractConfiguration : BaseEntityConfiguration<Contract>
{
    public override void Configure(EntityTypeBuilder<Contract> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.OwnerUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Number).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.StartDate).IsRequired(false);
        builder.Property(x => x.EndDate).IsRequired(false);
        builder.Property(x => x.Value).IsRequired(false);
        builder.Property(x => x.BillingCycle).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.AutoRenew).IsRequired();
        builder.Property(x => x.RenewalNoticeDays).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.RenewedFromId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
    }
}
