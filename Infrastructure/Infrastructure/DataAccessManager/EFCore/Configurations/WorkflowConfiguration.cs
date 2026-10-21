using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class WorkflowRuleConfiguration : BaseEntityConfiguration<WorkflowRule>
{
    public override void Configure(EntityTypeBuilder<WorkflowRule> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.TriggerEvent).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.ConditionsJson).IsRequired(false);
        builder.Property(x => x.ActionsJson).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.RunCount).IsRequired();
        builder.Property(x => x.LastRunAtUtc).IsRequired(false);

        builder.HasIndex(e => e.TriggerEvent);
    }
}

public class WorkflowRunConfiguration : BaseEntityConfiguration<WorkflowRun>
{
    public override void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.RuleId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RuleName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EventName).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.RecordId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RecordLabel).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Success).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.RunAtUtc).IsRequired();

        builder.HasIndex(e => e.RunAtUtc);
    }
}
