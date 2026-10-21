using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ChatSessionConfiguration : BaseEntityConfiguration<ChatSession>
{
    public override void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.TokenHash).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.VisitorName).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.VisitorEmail).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.CustomerId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.AssignedUserId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.AssignedUserName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.AgentUnread).IsRequired();
        builder.Property(x => x.CaseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.CaseNumber).HasMaxLength(50).IsRequired(false);

        builder.HasIndex(x => x.TokenHash);
    }
}

public class ChatMessageConfiguration : BaseEntityConfiguration<ChatMessage>
{
    public override void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.SessionId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.FromVisitor).IsRequired();
        builder.Property(x => x.SenderName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired(false);

        builder.HasIndex(x => x.SessionId);
    }
}
