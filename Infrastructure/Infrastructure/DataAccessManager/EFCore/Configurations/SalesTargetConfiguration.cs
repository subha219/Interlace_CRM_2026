using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SalesTargetConfiguration : BaseEntityConfiguration<SalesTarget>
{
    public override void Configure(EntityTypeBuilder<SalesTarget> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Year).IsRequired();
        builder.Property(x => x.Month).IsRequired();
        builder.Property(x => x.Amount).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.Year, e.Month });
    }
}
