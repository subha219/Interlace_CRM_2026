using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class EmailLogConfiguration : BaseEntityConfiguration<EmailLog>
{
    public override void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.ToEmail).HasMaxLength(EmailConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Subject).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Body).IsRequired(false);
        builder.Property(x => x.Success).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedType).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.SentByUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.SentByName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.SentAtUtc).IsRequired(false);
        builder.HasIndex(e => e.SentAtUtc);
        builder.HasIndex(e => new { e.RelatedType, e.RelatedId });
    }
}
