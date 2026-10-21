using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SalesPolicyConfiguration : BaseEntityConfiguration<SalesPolicy>
{
    public override void Configure(EntityTypeBuilder<SalesPolicy> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.DiscountApprovalThresholdPercent).IsRequired();
    }
}
