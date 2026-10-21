using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class CustomFieldDefinitionConfiguration : BaseEntityConfiguration<CustomFieldDefinition>
{
    public override void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Entity).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Label).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.FieldType).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Options).IsRequired(false);
        builder.Property(x => x.IsRequired).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
    }
}

public class CustomFieldValueConfiguration : BaseEntityConfiguration<CustomFieldValue>
{
    public override void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Entity).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RecordId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DefinitionId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Value).IsRequired(false);

        builder.HasIndex(e => new { e.Entity, e.RecordId });
    }
}
