using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MessagingManager;

public record ContactMatch(string Type, string Id, string Name, string? OwnerUserId, bool? ConsentMarketing);

public static class ContactMatcher
{
    /// <summary>Finds the customer or lead whose phone or WhatsApp number ends with the same ten digits.</summary>
    public static async Task<ContactMatch?> FindAsync(IEntityDbSet db, string address, CancellationToken ct)
    {
        var key = ChannelRules.LastTen(address);
        if (key.Length < 7) return null;

        var customers = await db.Customer.AsNoTracking().Where(x => !x.IsDeleted && (x.PhoneNumber != null || x.WhatsApp != null))
            .Select(x => new { x.Id, x.Name, x.OwnerUserId, x.PhoneNumber, x.WhatsApp, x.ConsentMarketing }).ToListAsync(ct);
        var c = customers.FirstOrDefault(x => ChannelRules.LastTen(x.WhatsApp) == key || ChannelRules.LastTen(x.PhoneNumber) == key);
        if (c != null) return new ContactMatch("Customer", c.Id, c.Name ?? "Customer", c.OwnerUserId, c.ConsentMarketing);

        var leads = await db.Lead.AsNoTracking().Where(x => !x.IsDeleted && (x.CompanyPhoneNumber != null || x.CompanyWhatsApp != null))
            .Select(x => new { x.Id, x.Title, x.CompanyName, x.OwnerUserId, x.CompanyPhoneNumber, x.CompanyWhatsApp }).ToListAsync(ct);
        var l = leads.FirstOrDefault(x => ChannelRules.LastTen(x.CompanyWhatsApp) == key || ChannelRules.LastTen(x.CompanyPhoneNumber) == key);
        return l == null ? null : new ContactMatch("Lead", l.Id, l.CompanyName ?? l.Title ?? "Lead", l.OwnerUserId, null);
    }

    public static async Task<bool> IsOptedOutAsync(IEntityDbSet db, string channel, string address, CancellationToken ct)
    {
        var key = ChannelRules.LastTen(address);
        var list = await db.ChannelOptOut.AsNoTracking().Where(x => !x.IsDeleted && x.Channel == channel).Select(x => x.Address).ToListAsync(ct);
        return list.Any(a => ChannelRules.LastTen(a) == key);
    }
}

// ---------- conversations ----------

public class ConversationDto
{
    public string? Channel { get; init; }
    public string? Address { get; init; }
    public string? Name { get; init; }
    public string? LastText { get; init; }
    public string? LastDirection { get; init; }
    public DateTime? LastAtUtc { get; init; }
    public int Count { get; init; }
}

public class GetConversationsResult
{
    public List<ConversationDto> Data { get; init; } = new();
    public Dictionary<string, bool> Configured { get; init; } = new();
}

public class GetConversationsRequest : IRequest<GetConversationsResult> { }

public class GetConversationsHandler : IRequestHandler<GetConversationsRequest, GetConversationsResult>
{
    private readonly IQueryContext _context;
    private readonly IEnumerable<IChannelProvider> _providers;
    public GetConversationsHandler(IQueryContext context, IEnumerable<IChannelProvider> providers) { _context = context; _providers = providers; }

    public async Task<GetConversationsResult> Handle(GetConversationsRequest request, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-180);
        var rows = await _context.ChannelMessage.AsNoTracking().Where(x => !x.IsDeleted && x.CreatedAtUtc >= since)
            .OrderByDescending(x => x.CreatedAtUtc).Take(5000).ToListAsync(cancellationToken);

        var data = rows.GroupBy(x => new { x.Channel, x.Address }).Select(g =>
        {
            var last = g.First();
            return new ConversationDto
            {
                Channel = g.Key.Channel, Address = g.Key.Address,
                Name = g.Select(x => x.ContactName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? g.Select(x => x.RelatedName).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
                LastText = (last.Body ?? "").Length > 80 ? last.Body![..80] + "..." : last.Body,
                LastDirection = last.Direction, LastAtUtc = last.CreatedAtUtc, Count = g.Count()
            };
        }).OrderByDescending(x => x.LastAtUtc).ToList();

        return new GetConversationsResult { Data = data, Configured = _providers.ToDictionary(p => p.Channel, p => p.IsConfigured) };
    }
}

public class MessageDto
{
    public string? Id { get; init; }
    public string? Direction { get; init; }
    public string? Body { get; init; }
    public string? TemplateName { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
    public string? SentByName { get; init; }
    public DateTime? AtUtc { get; init; }
}

public class GetThreadResult
{
    public string? Channel { get; init; }
    public string? Address { get; init; }
    public string? Name { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public bool WindowOpen { get; init; }
    public bool OptedOut { get; init; }
    public List<MessageDto> Messages { get; init; } = new();
}

public class GetThreadRequest : IRequest<GetThreadResult> { public string? Channel { get; init; } public string? Address { get; init; } }

public class GetThreadHandler : IRequestHandler<GetThreadRequest, GetThreadResult>
{
    private readonly IQueryContext _context;
    public GetThreadHandler(IQueryContext context) { _context = context; }

    public async Task<GetThreadResult> Handle(GetThreadRequest request, CancellationToken cancellationToken)
    {
        var channel = request.Channel ?? "";
        var address = ChannelRules.NormalizePhone(request.Address) ?? request.Address ?? "";
        var rows = await _context.ChannelMessage.AsNoTracking().Where(x => !x.IsDeleted && x.Channel == channel && x.Address == address)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);
        rows.Reverse();

        var lastIn = rows.Where(x => x.Direction == "In").Select(x => x.CreatedAtUtc).LastOrDefault();
        var match = await ContactMatcher.FindAsync(_context, address, cancellationToken);
        return new GetThreadResult
        {
            Channel = channel, Address = address, Name = match?.Name ?? rows.Select(x => x.ContactName).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
            RelatedType = match?.Type, RelatedId = match?.Id,
            WindowOpen = channel == ChannelRules.WhatsApp && ChannelRules.SessionOpen(lastIn, DateTime.UtcNow),
            OptedOut = await ContactMatcher.IsOptedOutAsync(_context, channel, address, cancellationToken),
            Messages = rows.Select(x => new MessageDto { Id = x.Id, Direction = x.Direction, Body = x.Body, TemplateName = x.TemplateName, Status = x.Status, Error = x.Error, SentByName = x.SentByName, AtUtc = x.CreatedAtUtc }).ToList()
        };
    }
}

// ---------- templates ----------

public class TemplateDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Channel { get; init; }
    public string? ProviderTemplateId { get; init; }
    public string? Language { get; init; }
    public string? Body { get; init; }
    public string? Variables { get; init; }
    public bool IsActive { get; init; }
}

public class GetTemplatesResult { public List<TemplateDto> Data { get; init; } = new(); }
public class GetTemplatesRequest : IRequest<GetTemplatesResult> { public string? Channel { get; init; } }

public class GetTemplatesHandler : IRequestHandler<GetTemplatesRequest, GetTemplatesResult>
{
    private readonly IQueryContext _context;
    public GetTemplatesHandler(IQueryContext context) { _context = context; }

    public async Task<GetTemplatesResult> Handle(GetTemplatesRequest request, CancellationToken cancellationToken)
    {
        var q = _context.MessageTemplate.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.Channel)) q = q.Where(x => x.Channel == request.Channel);
        var rows = await q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return new GetTemplatesResult { Data = rows.Select(x => new TemplateDto { Id = x.Id, Name = x.Name, Channel = x.Channel, ProviderTemplateId = x.ProviderTemplateId, Language = x.Language, Body = x.Body, Variables = x.Variables, IsActive = x.IsActive }).ToList() };
    }
}

public class SaveTemplateResult { public string? Id { get; init; } }

public class SaveTemplateRequest : IRequest<SaveTemplateResult>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Channel { get; init; }
    public string? ProviderTemplateId { get; init; }
    public string? Language { get; init; }
    public string? Body { get; init; }
    public string? Variables { get; init; }
    public bool IsActive { get; init; } = true;
    public string? UserId { get; set; }
}

public class SaveTemplateValidator : AbstractValidator<SaveTemplateRequest>
{
    public SaveTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Channel).Must(c => ChannelRules.Channels.Contains(c ?? "")).WithMessage("Choose WhatsApp or SMS.");
        RuleFor(x => x.ProviderTemplateId).NotEmpty().MaximumLength(200).WithMessage("Enter the template name (WhatsApp) or template id (SMS) from your provider.");
        RuleFor(x => x.Language).MaximumLength(20);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Variables).MaximumLength(500);
    }
}

public class SaveTemplateHandler : IRequestHandler<SaveTemplateRequest, SaveTemplateResult>
{
    private readonly ICommandRepository<MessageTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveTemplateHandler(ICommandRepository<MessageTemplate> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveTemplateResult> Handle(SaveTemplateRequest request, CancellationToken cancellationToken)
    {
        var isNew = string.IsNullOrEmpty(request.Id);
        var t = isNew ? new MessageTemplate { CreatedById = request.UserId } : await _repository.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Template not found.");
        t.Name = request.Name!.Trim();
        t.Channel = request.Channel;
        t.ProviderTemplateId = request.ProviderTemplateId!.Trim();
        t.Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language.Trim();
        t.Body = request.Body!.Trim();
        t.Variables = string.Join(",", ChannelRules.VariableNames(request.Variables));
        t.IsActive = request.IsActive;
        t.UpdatedById = request.UserId;
        if (isNew) await _repository.CreateAsync(t, cancellationToken); else _repository.Update(t);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveTemplateResult { Id = t.Id };
    }
}

public class DeleteTemplateRequest : IRequest<bool> { public string? Id { get; init; } public string? UserId { get; set; } }

public class DeleteTemplateHandler : IRequestHandler<DeleteTemplateRequest, bool>
{
    private readonly ICommandRepository<MessageTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteTemplateHandler(ICommandRepository<MessageTemplate> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(DeleteTemplateRequest request, CancellationToken cancellationToken)
    {
        var t = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Template not found.");
        t.UpdatedById = request.UserId;
        _repository.Delete(t);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

// ---------- sending ----------

public class SendMessageResult { public string? Id { get; init; } public string? Status { get; init; } public string? Error { get; init; } }

public class SendMessageRequest : IRequest<SendMessageResult>
{
    public string? Channel { get; init; }
    public string? To { get; init; }
    public string? Text { get; init; }
    public string? TemplateId { get; init; }
    public Dictionary<string, string>? Variables { get; init; }

    /// <summary>Marketing messages respect opt-outs and the customer's marketing consent; service replies do not.</summary>
    public bool IsMarketing { get; init; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.Channel).Must(c => ChannelRules.Channels.Contains(c ?? "")).WithMessage("Choose WhatsApp or SMS.");
        RuleFor(x => x.To).Must(t => ChannelRules.NormalizePhone(t) != null).WithMessage("Enter a valid phone number with country code.");
        RuleFor(x => x.Text).MaximumLength(4000);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Text) || !string.IsNullOrEmpty(x.TemplateId)).WithMessage("Write a message or choose a template.");
    }
}

public class SendMessageHandler : IRequestHandler<SendMessageRequest, SendMessageResult>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<ChannelMessage> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IChannelProvider> _providers;

    public SendMessageHandler(IQueryContext context, ICommandRepository<ChannelMessage> repository, IUnitOfWork unitOfWork, IEnumerable<IChannelProvider> providers)
    { _context = context; _repository = repository; _unitOfWork = unitOfWork; _providers = providers; }

    public async Task<SendMessageResult> Handle(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var channel = request.Channel!;
        var to = ChannelRules.NormalizePhone(request.To)!;
        var provider = _providers.FirstOrDefault(p => p.Channel == channel);
        if (provider == null || !provider.IsConfigured) throw new Exception($"{channel} is not set up yet. An administrator can add the provider keys under Settings > Connections.");

        var match = await ContactMatcher.FindAsync(_context, to, cancellationToken);
        if (request.IsMarketing)
        {
            if (match?.ConsentMarketing == false) throw new Exception("This customer has not agreed to marketing messages.");
            if (await ContactMatcher.IsOptedOutAsync(_context, channel, to, cancellationToken)) throw new Exception("This number has opted out of messages.");
        }

        MessageTemplate? template = null;
        if (!string.IsNullOrEmpty(request.TemplateId))
        {
            template = await _context.MessageTemplate.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.TemplateId && !x.IsDeleted && x.IsActive, cancellationToken)
                ?? throw new Exception("Template not found or switched off.");
            if (template.Channel != channel) throw new Exception("That template is for another channel.");
        }

        var variables = new List<KeyValuePair<string, string>>();
        string body;
        if (template != null)
        {
            foreach (var name in ChannelRules.VariableNames(template.Variables))
            {
                var value = request.Variables != null && request.Variables.TryGetValue(name, out var v) ? (v ?? "").Trim() : "";
                if (value.Length == 0) throw new Exception($"Fill in \"{name}\".");
                variables.Add(new(name, value));
            }
            body = ChannelRules.Render(template.Body, variables);
        }
        else
        {
            if (channel == ChannelRules.Sms) throw new Exception("SMS must use a registered template.");
            var lastIn = await _context.ChannelMessage.AsNoTracking().Where(x => !x.IsDeleted && x.Channel == channel && x.Address == to && x.Direction == "In")
                .OrderByDescending(x => x.CreatedAtUtc).Select(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
            if (!ChannelRules.SessionOpen(lastIn, DateTime.UtcNow)) throw new Exception("WhatsApp only allows free text within 24 hours of the customer's last message. Use a template instead.");
            body = request.Text!.Trim();
        }

        var result = await provider.SendAsync(new ChannelSendRequest(to, body, template?.Name, template?.ProviderTemplateId, template?.Language, variables), cancellationToken);

        var m = new ChannelMessage
        {
            Channel = channel, Direction = "Out", Address = to, ContactName = match?.Name, Body = body, TemplateName = template?.Name,
            Status = result.Success ? "Sent" : "Failed", Error = result.Error is { Length: > 500 } ? result.Error[..500] : result.Error, ExternalId = result.ExternalId,
            IsMarketing = request.IsMarketing, RelatedType = match?.Type, RelatedId = match?.Id, RelatedName = match?.Name, SentByUserId = request.UserId, SentByName = request.UserName, CreatedById = request.UserId
        };
        await _repository.CreateAsync(m, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SendMessageResult { Id = m.Id, Status = m.Status, Error = m.Error };
    }
}

// ---------- inbound (webhook) ----------

public class ProcessWhatsAppWebhookRequest : IRequest<int> { public string? Json { get; init; } }

public class ProcessWhatsAppWebhookHandler : IRequestHandler<ProcessWhatsAppWebhookRequest, int>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<ChannelMessage> _messages;
    private readonly ICommandRepository<ChannelOptOut> _optOuts;
    private readonly ICommandRepository<Notification> _notifications;
    private readonly ICommandRepository<Customer> _customers;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessWhatsAppWebhookHandler(IQueryContext context, ICommandRepository<ChannelMessage> messages, ICommandRepository<ChannelOptOut> optOuts,
        ICommandRepository<Notification> notifications, ICommandRepository<Customer> customers, IUnitOfWork unitOfWork)
    { _context = context; _messages = messages; _optOuts = optOuts; _notifications = notifications; _customers = customers; _unitOfWork = unitOfWork; }

    public async Task<int> Handle(ProcessWhatsAppWebhookRequest request, CancellationToken cancellationToken)
    {
        var events = ChannelRules.ParseWhatsApp(request.Json);
        var changed = 0;

        foreach (var s in events.Statuses)
        {
            var row = await _messages.GetQuery().FirstOrDefaultAsync(x => x.ExternalId == s.Id && !x.IsDeleted, cancellationToken);
            if (row == null) continue;
            // a late "sent" must not overwrite "read"
            var rank = new[] { "Queued", "Sent", "Delivered", "Read" };
            var oldRank = Array.IndexOf(rank, row.Status ?? "Queued");
            var newRank = Array.IndexOf(rank, s.Status);
            if (s.Status == "Failed" || newRank > oldRank)
            {
                row.Status = s.Status;
                if (s.Status == "Failed") row.Error = s.Error is { Length: > 500 } ? s.Error[..500] : s.Error;
                _messages.Update(row);
                changed++;
            }
        }

        foreach (var m in events.Messages)
        {
            if (await _context.ChannelMessage.AsNoTracking().AnyAsync(x => x.ExternalId == m.Id, cancellationToken)) continue;
            var address = ChannelRules.NormalizePhone("+" + m.From) ?? m.From;
            var match = await ContactMatcher.FindAsync(_context, address, cancellationToken);

            await _messages.CreateAsync(new ChannelMessage
            {
                Channel = ChannelRules.WhatsApp, Direction = "In", Address = address, ContactName = match?.Name ?? m.Name, Body = m.Text, Status = "Received", ExternalId = m.Id,
                RelatedType = match?.Type, RelatedId = match?.Id, RelatedName = match?.Name
            }, cancellationToken);
            changed++;

            if (ChannelRules.IsStopKeyword(m.Text))
            {
                if (!await ContactMatcher.IsOptedOutAsync(_context, ChannelRules.WhatsApp, address, cancellationToken))
                    await _optOuts.CreateAsync(new ChannelOptOut { Channel = ChannelRules.WhatsApp, Address = address, Reason = "Replied STOP" }, cancellationToken);
                if (match?.Type == "Customer")
                {
                    var customer = await _customers.GetAsync(match.Id, cancellationToken);
                    if (customer != null) { customer.ConsentMarketing = false; _customers.Update(customer); }
                }
            }
            else
            {
                await _notifications.CreateAsync(new Notification
                {
                    Title = "New WhatsApp message", Message = $"{match?.Name ?? m.Name ?? address}: {(m.Text.Length > 120 ? m.Text[..120] + "..." : m.Text)}",
                    Type = "Info", Url = "/Messaging/Inbox", UserId = match?.OwnerUserId, DedupKey = "wa-in:" + m.Id
                }, cancellationToken);
            }
        }

        if (changed > 0) await _unitOfWork.SaveAsync(cancellationToken);
        return changed;
    }
}

// ---------- everything said to one customer or lead ----------

public class RecordCommItemDto
{
    /// <summary>whatsapp, sms, call or email.</summary>
    public string? Kind { get; init; }
    public string? Direction { get; init; }
    public string? Text { get; init; }
    public string? Status { get; init; }
    public string? By { get; init; }
    public DateTime? AtUtc { get; init; }
}

public class GetRecordCommsResult
{
    public string? Name { get; init; }
    public string? Phone { get; init; }
    public string? WhatsApp { get; init; }
    public List<RecordCommItemDto> Items { get; init; } = new();
}

public class GetRecordCommsRequest : IRequest<GetRecordCommsResult>
{
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
}

public class GetRecordCommsHandler : IRequestHandler<GetRecordCommsRequest, GetRecordCommsResult>
{
    private readonly IQueryContext _context;
    public GetRecordCommsHandler(IQueryContext context) { _context = context; }

    public async Task<GetRecordCommsResult> Handle(GetRecordCommsRequest request, CancellationToken cancellationToken)
    {
        var id = request.RelatedId ?? "";
        string? name, phone, whatsApp;
        if (request.RelatedType == "Customer")
        {
            var c = await _context.Customer.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new { x.Name, x.PhoneNumber, x.WhatsApp }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new Exception("Customer not found.");
            (name, phone, whatsApp) = (c.Name, c.PhoneNumber, c.WhatsApp);
        }
        else if (request.RelatedType == "Lead")
        {
            var l = await _context.Lead.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new { x.CompanyName, x.Title, x.CompanyPhoneNumber, x.CompanyWhatsApp }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new Exception("Lead not found.");
            (name, phone, whatsApp) = (l.CompanyName ?? l.Title, l.CompanyPhoneNumber, l.CompanyWhatsApp);
        }
        else throw new Exception("Only customers and leads are supported.");

        var items = new List<RecordCommItemDto>();
        foreach (var m in await _context.ChannelMessage.AsNoTracking().Where(x => !x.IsDeleted && x.RelatedId == id).OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(cancellationToken))
            items.Add(new RecordCommItemDto { Kind = (m.Channel ?? "").ToLowerInvariant(), Direction = m.Direction, Text = m.Body, Status = m.Status, By = m.SentByName, AtUtc = m.CreatedAtUtc });
        foreach (var c in await _context.CallLog.AsNoTracking().Where(x => !x.IsDeleted && x.RelatedId == id).OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(cancellationToken))
            items.Add(new RecordCommItemDto
            {
                Kind = "call", Direction = c.Direction, Status = c.Status, By = c.UserName, AtUtc = c.CreatedAtUtc,
                Text = (c.DurationSeconds > 0 ? $"{c.DurationSeconds / 60}:{c.DurationSeconds % 60:00} " : "") + (string.IsNullOrWhiteSpace(c.Notes) ? "" : c.Notes)
            });
        foreach (var e in await _context.EmailMessage.AsNoTracking().Where(x => !x.IsDeleted && x.RelatedId == id).OrderByDescending(x => x.SentAtUtc).Take(100).ToListAsync(cancellationToken))
            items.Add(new RecordCommItemDto { Kind = "email", Direction = e.Direction, Text = e.Subject + (string.IsNullOrWhiteSpace(e.Snippet) ? "" : " - " + e.Snippet), AtUtc = e.SentAtUtc });

        return new GetRecordCommsResult { Name = name, Phone = phone, WhatsApp = whatsApp, Items = items.OrderByDescending(x => x.AtUtc).Take(150).ToList() };
    }
}
