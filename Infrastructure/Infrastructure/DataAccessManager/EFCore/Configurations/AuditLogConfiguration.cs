using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class AuditLogConfiguration : BaseEntityConfiguration<AuditLog>
{
    public override void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.EntityName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EntityId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Action).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.UserName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Changes).IsRequired(false);

        builder.HasIndex(e => e.EntityName);
        builder.HasIndex(e => e.EntityId);
    }
}
