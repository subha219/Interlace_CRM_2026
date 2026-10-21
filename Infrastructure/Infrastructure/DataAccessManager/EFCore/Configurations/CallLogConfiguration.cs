using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class CallLogConfiguration : BaseEntityConfiguration<CallLog>
{
    public override void Configure(EntityTypeBuilder<CallLog> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Direction).HasMaxLength(10).IsRequired(false);
        builder.Property(x => x.CustomerNumber).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.AgentNumber).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.DurationSeconds).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.RecordingUrl).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.Error).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.RelatedType).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.UserName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.ActivityId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);

        builder.HasIndex(x => x.ExternalId);
    }
}
