using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SupportCategoryConfiguration : BaseEntityConfiguration<SupportCategory>
{
    public override void Configure(EntityTypeBuilder<SupportCategory> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.DefaultOwnerUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DefaultPriority).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
    }
}
