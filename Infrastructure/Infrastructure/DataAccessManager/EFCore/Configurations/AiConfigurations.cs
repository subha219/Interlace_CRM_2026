using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class AiSettingConfiguration : BaseEntityConfiguration<AiSetting>
{
    public override void Configure(EntityTypeBuilder<AiSetting> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.DailyLimitPerUser).IsRequired();
        builder.Property(x => x.MonthlyLimitTotal).IsRequired();
    }
}

public class AiUsageLogConfiguration : BaseEntityConfiguration<AiUsageLog>
{
    public override void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Feature).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedType).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Success).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.InputChars).IsRequired();
        builder.Property(x => x.OutputChars).IsRequired();
        builder.Property(x => x.DurationMs).IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
