using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SignRequestConfiguration : BaseEntityConfiguration<SignRequest>
{
    public override void Configure(EntityTypeBuilder<SignRequest> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Number).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedType).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.DocumentText).HasMaxLength(8000).IsRequired(false);
        builder.Property(x => x.DocumentHash).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.SignerName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.SignerEmail).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.TokenHash).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.OtpHash).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.OtpAttempts).IsRequired();
        builder.Property(x => x.OtpSent).IsRequired();
        builder.Property(x => x.SignedName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.SignedIp).HasMaxLength(60).IsRequired(false);
        builder.Property(x => x.SignedUserAgent).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.DeclineReason).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RequestedByName).HasMaxLength(200).IsRequired(false);

        builder.HasIndex(x => x.TokenHash);
        builder.HasIndex(x => x.RelatedId);
    }
}
