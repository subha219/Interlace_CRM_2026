using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class PortalSessionConfiguration : BaseEntityConfiguration<PortalSession>
{
    public override void Configure(EntityTypeBuilder<PortalSession> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.CustomerId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Email).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.LoginTokenHash).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.SessionTokenHash).HasMaxLength(100).IsRequired(false);

        builder.HasIndex(x => x.LoginTokenHash);
        builder.HasIndex(x => x.SessionTokenHash);
    }
}
