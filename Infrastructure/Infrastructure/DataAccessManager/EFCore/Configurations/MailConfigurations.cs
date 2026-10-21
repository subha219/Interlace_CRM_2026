using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class MailConnectionConfiguration : BaseEntityConfiguration<MailConnection>
{
    public override void Configure(EntityTypeBuilder<MailConnection> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Email).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.RefreshTokenProtected).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CursorMs).IsRequired(false);
        builder.Property(x => x.LastStatus).HasMaxLength(300).IsRequired(false);

        builder.HasIndex(x => x.UserId);
    }
}

public class EmailMessageConfiguration : BaseEntityConfiguration<EmailMessage>
{
    public override void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.UserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.GmailId).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.ThreadId).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Subject).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.FromAddress).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.ToAddresses).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.Snippet).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Direction).HasMaxLength(10).IsRequired(false);
        builder.Property(x => x.RelatedType).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.ActivityId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);

        builder.HasIndex(x => new { x.UserId, x.GmailId });
        builder.HasIndex(x => x.RelatedId);
    }
}
