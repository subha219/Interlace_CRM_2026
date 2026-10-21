using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ReportScheduleConfiguration : BaseEntityConfiguration<ReportSchedule>
{
    public override void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.ReportId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.OwnerUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Frequency).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Day).IsRequired();
        builder.Property(x => x.Hour).IsRequired();
        builder.Property(x => x.Recipients).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.LastStatus).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.Attach).HasMaxLength(10).IsRequired(false);

        builder.HasIndex(x => x.ReportId);
    }
}
