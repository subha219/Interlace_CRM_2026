using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class LeadStageHistoryConfiguration : BaseEntityConfiguration<LeadStageHistory>
{
    public override void Configure(EntityTypeBuilder<LeadStageHistory> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.LeadId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.FromStage).IsRequired(false);
        builder.Property(x => x.ToStage).IsRequired(false);
        builder.Property(x => x.FromClosing).IsRequired(false);
        builder.Property(x => x.ToClosing).IsRequired(false);
        builder.Property(x => x.Amount).IsRequired(false);
        builder.Property(x => x.OwnerUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ChangedAtUtc).IsRequired();

        builder.HasIndex(e => e.LeadId);
    }
}
