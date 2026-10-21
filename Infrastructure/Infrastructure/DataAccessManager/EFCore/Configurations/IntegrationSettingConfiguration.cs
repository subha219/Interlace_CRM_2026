using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class IntegrationSettingConfiguration : BaseEntityConfiguration<IntegrationSetting>
{
    public override void Configure(EntityTypeBuilder<IntegrationSetting> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.IsSecret).IsRequired();
        builder.HasIndex(x => x.Key);
    }
}
