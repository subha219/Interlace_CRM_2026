using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class RecordDocumentConfiguration : BaseEntityConfiguration<RecordDocument>
{
    public override void Configure(EntityTypeBuilder<RecordDocument> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.RelatedType).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.GeneratedName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.OriginalName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.FileSize).IsRequired(false);
        builder.Property(x => x.UploadedByName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => new { e.RelatedType, e.RelatedId });
    }
}
