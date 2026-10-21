using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SupportCaseConfiguration : BaseEntityConfiguration<SupportCase>
{
    public override void Configure(EntityTypeBuilder<SupportCase> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Number).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Title).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Status).IsRequired(false);
        builder.Property(x => x.Priority).IsRequired(false);
        builder.Property(x => x.Channel).IsRequired(false);
        builder.Property(x => x.CustomerId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.SalesRepresentativeId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DueDate).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Resolution).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ResolvedAtUtc).IsRequired(false);
        builder.Property(x => x.FirstResponseDue).IsRequired(false);
        builder.Property(x => x.FirstRespondedAt).IsRequired(false);
        builder.Property(x => x.EscalatedAt).IsRequired(false);
        builder.Property(x => x.CsatScore).IsRequired(false);
        builder.Property(x => x.CsatComment).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.CategoryId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.CsatToken).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.CsatSubmittedAt).IsRequired(false);

        builder.HasIndex(e => e.Number);
        builder.HasIndex(e => e.Title);
        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => e.SalesRepresentativeId);
    }
}
