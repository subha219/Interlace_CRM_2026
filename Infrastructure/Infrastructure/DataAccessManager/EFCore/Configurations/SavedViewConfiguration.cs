using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class SavedViewConfiguration : BaseEntityConfiguration<SavedView>
{
    public override void Configure(EntityTypeBuilder<SavedView> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Page).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Name).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.StateJson).IsRequired(false);
        builder.Property(x => x.OwnerUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.OwnerName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.IsShared).IsRequired();

        builder.HasIndex(e => e.Page);
    }
}
