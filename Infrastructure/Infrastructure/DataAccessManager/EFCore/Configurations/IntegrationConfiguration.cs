using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ApiKeyConfiguration : BaseEntityConfiguration<ApiKey>
{
    public override void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.KeyHash).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Prefix).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.OwnerUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.LastUsedAtUtc).IsRequired(false);
        builder.Property(x => x.ExpiresAtUtc).IsRequired(false);

        builder.HasIndex(e => e.KeyHash);
    }
}

public class WebhookEndpointConfiguration : BaseEntityConfiguration<WebhookEndpoint>
{
    public override void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Url).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.Secret).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Events).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
    }
}

public class WebhookDeliveryConfiguration : BaseEntityConfiguration<WebhookDelivery>
{
    public override void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.EndpointId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EndpointName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EventName).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Success).IsRequired();
        builder.Property(x => x.HttpStatus).IsRequired(false);
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.DeliveredAtUtc).IsRequired();

        builder.HasIndex(e => e.DeliveredAtUtc);
    }
}
