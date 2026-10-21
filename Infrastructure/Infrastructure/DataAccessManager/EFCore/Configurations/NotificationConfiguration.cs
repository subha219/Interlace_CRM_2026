using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class NotificationConfiguration : BaseEntityConfiguration<Notification>
{
    public override void Configure(EntityTypeBuilder<Notification> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Title).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Message).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Type).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Url).HasMaxLength(PathConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DedupKey).HasMaxLength(NameConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.DedupKey);
    }
}
