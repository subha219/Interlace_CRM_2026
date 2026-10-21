using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SupportCaseCommentConfiguration : BaseEntityConfiguration<SupportCaseComment>
{
    public override void Configure(EntityTypeBuilder<SupportCaseComment> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.SupportCaseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Comment).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.SupportCaseId);
    }
}
