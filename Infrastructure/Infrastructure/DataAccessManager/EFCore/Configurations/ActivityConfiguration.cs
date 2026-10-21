using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ActivityConfiguration : BaseEntityConfiguration<Activity>
{
    public override void Configure(EntityTypeBuilder<Activity> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.OwnerUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Number).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Subject).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Type).IsRequired(false);
        builder.Property(x => x.Status).IsRequired(false);
        builder.Property(x => x.Priority).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.StartAt).IsRequired(false);
        builder.Property(x => x.EndAt).IsRequired(false);
        builder.Property(x => x.CompletedAt).IsRequired(false);
        builder.Property(x => x.Outcome).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedType).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.AssignedToUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.Number);
        builder.HasIndex(e => e.AssignedToUserId);
        builder.HasIndex(e => e.StartAt);
        builder.HasIndex(e => new { e.RelatedType, e.RelatedId });
    }
}
