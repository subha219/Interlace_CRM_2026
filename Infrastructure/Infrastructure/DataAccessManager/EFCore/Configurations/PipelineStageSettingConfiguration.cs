using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class PipelineStageSettingConfiguration : BaseEntityConfiguration<PipelineStageSetting>
{
    public override void Configure(EntityTypeBuilder<PipelineStageSetting> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Stage).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Probability).IsRequired();
    }
}
