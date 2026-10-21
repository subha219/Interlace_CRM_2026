using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class KbArticleConfiguration : BaseEntityConfiguration<KbArticle>
{
    public override void Configure(EntityTypeBuilder<KbArticle> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Body).HasMaxLength(20000).IsRequired(false);
        builder.Property(x => x.CategoryId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Tags).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.IsPublished).IsRequired();
        builder.Property(x => x.ViewCount).IsRequired();
        builder.Property(x => x.HelpfulYes).IsRequired();
        builder.Property(x => x.HelpfulNo).IsRequired();
    }
}
