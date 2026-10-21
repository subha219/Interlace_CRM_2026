using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class CalendarConnectionConfiguration : BaseEntityConfiguration<CalendarConnection>
{
    public override void Configure(EntityTypeBuilder<CalendarConnection> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Provider).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.Email).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.RefreshTokenProtected).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.LastStatus).HasMaxLength(300).IsRequired(false);

        builder.HasIndex(x => x.UserId);
    }
}

public class CalendarEventLinkConfiguration : BaseEntityConfiguration<CalendarEventLink>
{
    public override void Configure(EntityTypeBuilder<CalendarEventLink> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.ActivityId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ExternalEventId).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Hash).HasMaxLength(100).IsRequired(false);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ActivityId);
    }
}
