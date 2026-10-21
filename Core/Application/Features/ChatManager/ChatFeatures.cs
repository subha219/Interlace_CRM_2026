using System.Security.Cryptography;
using System.Text;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Application.Features.SupportCaseManager;
using Domain.Enums;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ChatManager;

public static class ChatRules
{
    public const int MaxMessageLength = 1000;
    public const int MaxVisitorMessagesPerMinute = 10;
    public const int MaxNewChatsPerHour = 60;
    public static readonly TimeSpan AutoCloseAfter = TimeSpan.FromHours(12);

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Strips control characters and trims; chat text is plain text only and is always HTML-encoded when shown.</summary>
    public static string Clean(string? text, int max)
    {
        var s = new string((text ?? "").Where(c => !char.IsControl(c) || c is '\n').ToArray()).Trim();
        return s.Length > max ? s[..max] : s;
    }

    public static bool LooksLikeEmail(string? e) =>
        !string.IsNullOrWhiteSpace(e) && e.Length <= 255 && e.Contains('@') && !e.Contains(' ') && e.IndexOf('@') > 0 && e.IndexOf('@') < e.Length - 3;

    public static bool IsStale(DateTime? lastMessageUtc, DateTime nowUtc) => lastMessageUtc != null && nowUtc - lastMessageUtc.Value > AutoCloseAfter;
}

public class ChatLineDto
{
    public string? Id { get; init; }
    public bool FromVisitor { get; init; }
    public string? SenderName { get; init; }
    public string? Body { get; init; }
    public DateTime? AtUtc { get; init; }
}

internal static class ChatLookup
{
    public static async Task<ChatSession> ByTokenAsync(ICommandRepository<ChatSession> repo, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) throw new UnauthorizedAccessException("Chat not found.");
        var hash = ChatRules.Hash(token);
        return await repo.GetQuery().FirstOrDefaultAsync(x => x.TokenHash == hash && !x.IsDeleted, ct) ?? throw new UnauthorizedAccessException("Chat not found.");
    }
}

// ---------- visitor side (anonymous) ----------

public class StartChatResult { public string? Token { get; init; } public List<ChatLineDto> Messages { get; init; } = new(); }

public class StartChatRequest : IRequest<StartChatResult>
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Message { get; init; }
}

public class StartChatValidator : AbstractValidator<StartChatRequest>
{
    public StartChatValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).Must(e => string.IsNullOrWhiteSpace(e) || ChatRules.LooksLikeEmail(e)).WithMessage("That e-mail address does not look right.");
        RuleFor(x => x.Message).NotEmpty().MaximumLength(ChatRules.MaxMessageLength);
    }
}

public class StartChatHandler : IRequestHandler<StartChatRequest, StartChatResult>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<ChatSession> _sessions;
    private readonly ICommandRepository<ChatMessage> _messages;
    private readonly ICommandRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public StartChatHandler(IQueryContext context, ICommandRepository<ChatSession> sessions, ICommandRepository<ChatMessage> messages, ICommandRepository<Notification> notifications, IUnitOfWork unitOfWork)
    { _context = context; _sessions = sessions; _messages = messages; _notifications = notifications; _unitOfWork = unitOfWork; }

    public async Task<StartChatResult> Handle(StartChatRequest request, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        if (await _context.ChatSession.CountAsync(x => x.CreatedAtUtc >= since, cancellationToken) >= ChatRules.MaxNewChatsPerHour)
            throw new Exception("Chat is busy right now. Please try again later or send us an e-mail.");

        var name = ChatRules.Clean(request.Name, 100);
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var text = ChatRules.Clean(request.Message, ChatRules.MaxMessageLength);
        if (name.Length == 0 || text.Length == 0) throw new Exception("Please enter your name and a message.");

        // an e-mail that matches a customer links the chat to them; an unverified address never reveals anything about that customer
        string? customerId = null, customerName = null, ownerId = null;
        if (email != null)
        {
            var lower = email.ToLowerInvariant();
            var c = await _context.Customer.AsNoTracking().Where(x => !x.IsDeleted && x.EmailAddress != null && x.EmailAddress.ToLower() == lower)
                .Select(x => new { x.Id, x.Name, x.OwnerUserId }).FirstOrDefaultAsync(cancellationToken);
            if (c != null) { customerId = c.Id; customerName = c.Name; ownerId = c.OwnerUserId; }
        }

        var token = ChatRules.NewToken();
        var now = DateTime.UtcNow;
        var session = new ChatSession
        {
            TokenHash = ChatRules.Hash(token), VisitorName = name, VisitorEmail = email, Status = "Open", CustomerId = customerId, CustomerName = customerName,
            LastMessageAtUtc = now, AgentUnread = true
        };
        await _sessions.CreateAsync(session, cancellationToken);
        var first = new ChatMessage { SessionId = session.Id, FromVisitor = true, SenderName = name, Body = text };
        await _messages.CreateAsync(first, cancellationToken);
        await _notifications.CreateAsync(new Notification
        {
            Title = "New live chat", Message = $"{name}: {(text.Length > 100 ? text[..100] + "..." : text)}", Type = "Info", Url = "/Chats/ChatInbox", UserId = ownerId, DedupKey = "chat-new:" + session.Id
        }, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new StartChatResult { Token = token, Messages = new() { new ChatLineDto { Id = first.Id, FromVisitor = true, SenderName = name, Body = text, AtUtc = first.CreatedAtUtc ?? now } } };
    }
}

public class VisitorSendRequest : IRequest<bool>
{
    public string? Token { get; set; }
    public string? Text { get; init; }
}

public class VisitorSendValidator : AbstractValidator<VisitorSendRequest>
{
    public VisitorSendValidator() { RuleFor(x => x.Text).NotEmpty().MaximumLength(ChatRules.MaxMessageLength); }
}

public class VisitorSendHandler : IRequestHandler<VisitorSendRequest, bool>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<ChatSession> _sessions;
    private readonly ICommandRepository<ChatMessage> _messages;
    private readonly ICommandRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public VisitorSendHandler(IQueryContext context, ICommandRepository<ChatSession> sessions, ICommandRepository<ChatMessage> messages, ICommandRepository<Notification> notifications, IUnitOfWork unitOfWork)
    { _context = context; _sessions = sessions; _messages = messages; _notifications = notifications; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(VisitorSendRequest request, CancellationToken cancellationToken)
    {
        var session = await ChatLookup.ByTokenAsync(_sessions, request.Token, cancellationToken);
        if (session.Status != "Open") throw new Exception("This chat has ended. Please start a new one.");
        var text = ChatRules.Clean(request.Text, ChatRules.MaxMessageLength);
        if (text.Length == 0) throw new Exception("Please write a message.");

        var minuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var recent = await _context.ChatMessage.CountAsync(x => x.SessionId == session.Id && x.FromVisitor && x.CreatedAtUtc >= minuteAgo, cancellationToken);
        if (recent >= ChatRules.MaxVisitorMessagesPerMinute) throw new Exception("You are sending messages too fast. Please wait a moment.");

        await _messages.CreateAsync(new ChatMessage { SessionId = session.Id, FromVisitor = true, SenderName = session.VisitorName, Body = text }, cancellationToken);
        if (!session.AgentUnread)
        {
            await _notifications.CreateAsync(new Notification
            {
                Title = "Live chat reply", Message = $"{session.VisitorName}: {(text.Length > 100 ? text[..100] + "..." : text)}", Type = "Info", Url = "/Chats/ChatInbox",
                UserId = session.AssignedUserId, DedupKey = $"chat-msg:{session.Id}:{DateTime.UtcNow.Ticks}"
            }, cancellationToken);
        }
        session.AgentUnread = true;
        session.LastMessageAtUtc = DateTime.UtcNow;
        _sessions.Update(session);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

public class VisitorPollResult { public List<ChatLineDto> Messages { get; init; } = new(); public bool Closed { get; init; } }

public class VisitorPollRequest : IRequest<VisitorPollResult>
{
    public string? Token { get; set; }
    public DateTime? AfterUtc { get; init; }
}

public class VisitorPollHandler : IRequestHandler<VisitorPollRequest, VisitorPollResult>
{
    private readonly ICommandRepository<ChatSession> _sessions;
    private readonly IQueryContext _context;
    public VisitorPollHandler(ICommandRepository<ChatSession> sessions, IQueryContext context) { _sessions = sessions; _context = context; }

    public async Task<VisitorPollResult> Handle(VisitorPollRequest request, CancellationToken cancellationToken)
    {
        var session = await ChatLookup.ByTokenAsync(_sessions, request.Token, cancellationToken);
        var q = _context.ChatMessage.AsNoTracking().Where(x => x.SessionId == session.Id && !x.IsDeleted);
        if (request.AfterUtc != null) q = q.Where(x => x.CreatedAtUtc > request.AfterUtc);
        var rows = await q.OrderBy(x => x.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);
        return new VisitorPollResult
        {
            Closed = session.Status != "Open",
            Messages = rows.Select(x => new ChatLineDto { Id = x.Id, FromVisitor = x.FromVisitor, SenderName = x.SenderName, Body = x.Body, AtUtc = x.CreatedAtUtc }).ToList()
        };
    }
}

// ---------- agent side ----------

public class ChatSummaryDto
{
    public string? Id { get; init; }
    public string? VisitorName { get; init; }
    public string? VisitorEmail { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? Status { get; init; }
    public string? AssignedUserName { get; init; }
    public bool AgentUnread { get; init; }
    public string? CaseId { get; init; }
    public string? CaseNumber { get; init; }
    public string? LastText { get; init; }
    public DateTime? LastMessageAtUtc { get; init; }
}

public class GetChatsResult { public List<ChatSummaryDto> Data { get; init; } = new(); public int UnreadCount { get; init; } }
public class GetChatsRequest : IRequest<GetChatsResult> { public bool IncludeClosed { get; init; } }

public class GetChatsHandler : IRequestHandler<GetChatsRequest, GetChatsResult>
{
    private readonly IQueryContext _context;
    public GetChatsHandler(IQueryContext context) { _context = context; }

    public async Task<GetChatsResult> Handle(GetChatsRequest request, CancellationToken cancellationToken)
    {
        var q = _context.ChatSession.AsNoTracking().Where(x => !x.IsDeleted);
        if (!request.IncludeClosed) q = q.Where(x => x.Status == "Open");
        var sessions = await q.OrderByDescending(x => x.LastMessageAtUtc).Take(100).ToListAsync(cancellationToken);
        var ids = sessions.Select(s => s.Id).ToList();
        var last = (await _context.ChatMessage.AsNoTracking().Where(m => m.SessionId != null && ids.Contains(m.SessionId) && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAtUtc).Take(2000).ToListAsync(cancellationToken))
            .GroupBy(m => m.SessionId).ToDictionary(g => g.Key!, g => g.First().Body);

        return new GetChatsResult
        {
            UnreadCount = sessions.Count(s => s.AgentUnread && s.Status == "Open"),
            Data = sessions.Select(s => new ChatSummaryDto
            {
                Id = s.Id, VisitorName = s.VisitorName, VisitorEmail = s.VisitorEmail, CustomerId = s.CustomerId, CustomerName = s.CustomerName, Status = s.Status,
                AssignedUserName = s.AssignedUserName, AgentUnread = s.AgentUnread, CaseId = s.CaseId, CaseNumber = s.CaseNumber, LastText = last.TryGetValue(s.Id, out var t) ? (t != null && t.Length > 80 ? t[..80] + "..." : t) : null,
                LastMessageAtUtc = s.LastMessageAtUtc
            }).ToList()
        };
    }
}

public class GetChatThreadResult { public ChatSummaryDto? Chat { get; init; } public List<ChatLineDto> Messages { get; init; } = new(); }
public class GetChatThreadRequest : IRequest<GetChatThreadResult> { public string? Id { get; init; } }

public class GetChatThreadHandler : IRequestHandler<GetChatThreadRequest, GetChatThreadResult>
{
    private readonly ICommandRepository<ChatSession> _sessions;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    public GetChatThreadHandler(ICommandRepository<ChatSession> sessions, IQueryContext context, IUnitOfWork unitOfWork) { _sessions = sessions; _context = context; _unitOfWork = unitOfWork; }

    public async Task<GetChatThreadResult> Handle(GetChatThreadRequest request, CancellationToken cancellationToken)
    {
        var s = await _sessions.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Chat not found.");
        if (s.AgentUnread) { s.AgentUnread = false; _sessions.Update(s); await _unitOfWork.SaveAsync(cancellationToken); }
        var rows = await _context.ChatMessage.AsNoTracking().Where(x => x.SessionId == s.Id && !x.IsDeleted).OrderBy(x => x.CreatedAtUtc).Take(500).ToListAsync(cancellationToken);
        return new GetChatThreadResult
        {
            Chat = new ChatSummaryDto { Id = s.Id, VisitorName = s.VisitorName, VisitorEmail = s.VisitorEmail, CustomerId = s.CustomerId, CustomerName = s.CustomerName, Status = s.Status, AssignedUserName = s.AssignedUserName, AgentUnread = false, CaseId = s.CaseId, CaseNumber = s.CaseNumber, LastMessageAtUtc = s.LastMessageAtUtc },
            Messages = rows.Select(x => new ChatLineDto { Id = x.Id, FromVisitor = x.FromVisitor, SenderName = x.SenderName, Body = x.Body, AtUtc = x.CreatedAtUtc }).ToList()
        };
    }
}

public class AgentReplyRequest : IRequest<bool>
{
    public string? Id { get; init; }
    public string? Text { get; init; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class AgentReplyValidator : AbstractValidator<AgentReplyRequest>
{
    public AgentReplyValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Text).NotEmpty().MaximumLength(ChatRules.MaxMessageLength); }
}

public class AgentReplyHandler : IRequestHandler<AgentReplyRequest, bool>
{
    private readonly ICommandRepository<ChatSession> _sessions;
    private readonly ICommandRepository<ChatMessage> _messages;
    private readonly IUnitOfWork _unitOfWork;
    public AgentReplyHandler(ICommandRepository<ChatSession> sessions, ICommandRepository<ChatMessage> messages, IUnitOfWork unitOfWork) { _sessions = sessions; _messages = messages; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(AgentReplyRequest request, CancellationToken cancellationToken)
    {
        var s = await _sessions.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Chat not found.");
        if (s.Status != "Open") throw new Exception("This chat is closed.");
        var text = ChatRules.Clean(request.Text, ChatRules.MaxMessageLength);
        if (text.Length == 0) throw new Exception("Please write a message.");
        await _messages.CreateAsync(new ChatMessage { SessionId = s.Id, FromVisitor = false, SenderName = request.UserName ?? "Support", Body = text, CreatedById = request.UserId }, cancellationToken);
        if (string.IsNullOrEmpty(s.AssignedUserId)) { s.AssignedUserId = request.UserId; s.AssignedUserName = request.UserName; }
        s.AgentUnread = false;
        s.LastMessageAtUtc = DateTime.UtcNow;
        s.UpdatedById = request.UserId;
        _sessions.Update(s);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

public class CloseChatRequest : IRequest<bool> { public string? Id { get; init; } public string? UserId { get; set; } }

public class CloseChatHandler : IRequestHandler<CloseChatRequest, bool>
{
    private readonly ICommandRepository<ChatSession> _sessions;
    private readonly IUnitOfWork _unitOfWork;
    public CloseChatHandler(ICommandRepository<ChatSession> sessions, IUnitOfWork unitOfWork) { _sessions = sessions; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(CloseChatRequest request, CancellationToken cancellationToken)
    {
        var s = await _sessions.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Chat not found.");
        s.Status = "Closed"; s.AgentUnread = false; s.UpdatedById = request.UserId;
        _sessions.Update(s);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

public class CreateCaseFromChatResult { public string? CaseId { get; init; } public string? CaseNumber { get; init; } public bool AlreadyExisted { get; init; } }

public class CreateCaseFromChatRequest : IRequest<CreateCaseFromChatResult>
{
    public string? Id { get; init; }
    /// <summary>0 Low, 1 Normal, 2 High, 3 Critical. Defaults to Normal.</summary>
    public int? Priority { get; init; }
    public string? UserId { get; set; }
}

/// <summary>Turns a chat into a support case: the transcript becomes the description and the chat keeps a link to the case. Doing it twice returns the same case.</summary>
public class CreateCaseFromChatHandler : IRequestHandler<CreateCaseFromChatRequest, CreateCaseFromChatResult>
{
    private readonly ICommandRepository<ChatSession> _sessions;
    private readonly ICommandRepository<SupportCase> _cases;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;

    public CreateCaseFromChatHandler(ICommandRepository<ChatSession> sessions, ICommandRepository<SupportCase> cases, IQueryContext context, IUnitOfWork unitOfWork, NumberSequenceService numbers)
    { _sessions = sessions; _cases = cases; _context = context; _unitOfWork = unitOfWork; _numbers = numbers; }

    public async Task<CreateCaseFromChatResult> Handle(CreateCaseFromChatRequest request, CancellationToken cancellationToken)
    {
        var s = await _sessions.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Chat not found.");
        if (!string.IsNullOrEmpty(s.CaseId)) return new CreateCaseFromChatResult { CaseId = s.CaseId, CaseNumber = s.CaseNumber, AlreadyExisted = true };

        var priority = Enum.IsDefined(typeof(CasePriority), request.Priority ?? 1) ? (CasePriority)(request.Priority ?? 1) : CasePriority.Normal;
        var lines = await _context.ChatMessage.AsNoTracking().Where(x => x.SessionId == s.Id && !x.IsDeleted).OrderBy(x => x.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);
        var first = lines.FirstOrDefault(l => l.FromVisitor)?.Body ?? "";
        var title = $"Chat: {(first.Length > 70 ? first[..70] + "..." : first)}".Trim();
        if (title == "Chat:") title = $"Chat with {s.VisitorName}";

        var transcript = new StringBuilder();
        transcript.AppendLine($"From live chat. Visitor: {s.VisitorName} {s.VisitorEmail}".TrimEnd());
        foreach (var l in lines) transcript.AppendLine($"{(l.FromVisitor ? (s.VisitorName ?? "Visitor") : (l.SenderName ?? "Agent"))}: {l.Body}");
        var description = transcript.ToString();
        var max = Domain.Common.Constants.DescriptionConsts.MaxLength;
        if (description.Length > max) description = description[..(max - 3)] + "...";

        var sla = await SlaConfig.LoadAsync(_context, cancellationToken);
        var entity = new SupportCase
        {
            CreatedById = request.UserId,
            Number = _numbers.GenerateNumber(nameof(SupportCase), "", "CASE"),
            Title = title.Length > 200 ? title[..200] : title,
            Status = CaseStatus.New,
            Priority = priority,
            Channel = CaseChannel.Chat,
            CustomerId = s.CustomerId,
            Description = description,
            DueDate = sla.ResolutionDue(priority),
            FirstResponseDue = sla.FirstResponseDueAt(priority),
        };
        await _cases.CreateAsync(entity, cancellationToken);

        s.CaseId = entity.Id; s.CaseNumber = entity.Number; s.UpdatedById = request.UserId;
        _sessions.Update(s);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new CreateCaseFromChatResult { CaseId = entity.Id, CaseNumber = entity.Number };
    }
}
