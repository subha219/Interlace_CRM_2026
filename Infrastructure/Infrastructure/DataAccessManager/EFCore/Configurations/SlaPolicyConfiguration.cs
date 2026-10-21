using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SlaPolicyConfiguration : BaseEntityConfiguration<SlaPolicy>
{
    public override void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Priority).IsRequired();
        builder.Property(x => x.FirstResponseHours).IsRequired();
        builder.Property(x => x.ResolutionHours).IsRequired();
        builder.Property(x => x.EscalationUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
    }
}
