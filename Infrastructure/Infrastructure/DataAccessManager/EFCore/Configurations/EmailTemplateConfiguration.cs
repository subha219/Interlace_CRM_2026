using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class EmailTemplateConfiguration : BaseEntityConfiguration<EmailTemplate>
{
    public override void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Subject).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Body).IsRequired(false);
        builder.Property(x => x.IsMarketing).IsRequired();
        builder.HasIndex(e => e.Name);
    }
}
