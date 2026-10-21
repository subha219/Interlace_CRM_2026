using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ChannelMessageConfiguration : BaseEntityConfiguration<ChannelMessage>
{
    public override void Configure(EntityTypeBuilder<ChannelMessage> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Direction).HasMaxLength(10).IsRequired(false);
        builder.Property(x => x.Address).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.ContactName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.TemplateName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Error).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.ExternalId).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.IsMarketing).IsRequired();
        builder.Property(x => x.RelatedType).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.SentByUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.SentByName).HasMaxLength(200).IsRequired(false);

        builder.HasIndex(x => x.Address);
        builder.HasIndex(x => x.ExternalId);
    }
}

public class MessageTemplateConfiguration : BaseEntityConfiguration<MessageTemplate>
{
    public override void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.ProviderTemplateId).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Language).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.Variables).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
    }
}

public class ChannelOptOutConfiguration : BaseEntityConfiguration<ChannelOptOut>
{
    public override void Configure(EntityTypeBuilder<ChannelOptOut> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Address).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired(false);

        builder.HasIndex(x => x.Address);
    }
}
