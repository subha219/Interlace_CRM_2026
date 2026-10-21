using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class AgentSettingConfiguration : BaseEntityConfiguration<AgentSetting>
{
    public override void Configure(EntityTypeBuilder<AgentSetting> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.AgentKey).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.MaxPerRun).IsRequired();
        builder.HasIndex(x => x.AgentKey);
    }
}

public class AgentRunConfiguration : BaseEntityConfiguration<AgentRun>
{
    public override void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.AgentKey).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RunByUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RunByName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Error).HasMaxLength(300).IsRequired(false);
        builder.HasIndex(x => x.AgentKey);
    }
}

public class AgentProposalConfiguration : BaseEntityConfiguration<AgentProposal>
{
    public override void Configure(EntityTypeBuilder<AgentProposal> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.AgentKey).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RunId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedType).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.RelatedId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RelatedName).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.Kind).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.Title).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.Reason).HasMaxLength(600).IsRequired(false);
        builder.Property(x => x.Payload).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.RequiredRole).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Source).HasMaxLength(10).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.DedupKey).HasMaxLength(150).IsRequired(false);
        builder.Property(x => x.DecidedByUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DecidedByName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Result).HasMaxLength(300).IsRequired(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.DedupKey);
    }
}
