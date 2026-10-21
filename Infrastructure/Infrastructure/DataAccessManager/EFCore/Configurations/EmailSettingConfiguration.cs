using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class EmailSettingConfiguration : BaseEntityConfiguration<EmailSetting>
{
    public override void Configure(EntityTypeBuilder<EmailSetting> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Host).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Port).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.PasswordProtected).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.FromAddress).HasMaxLength(EmailConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.FromName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Security).HasMaxLength(CodeConsts.MaxLength).IsRequired(false);
    }
}
