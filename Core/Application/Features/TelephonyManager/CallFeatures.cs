using System.Security.Cryptography;
using System.Text;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.MessagingManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TelephonyManager;

public interface ICallProvider
{
    bool IsConfigured { get; }
    Task<ChannelSendResult> StartAsync(string agentNumber, string customerNumber, string? statusCallbackUrl, CancellationToken cancellationToken);
}

public static class CallRules
{
    public static string MapStatus(string? provider) => (provider ?? "").Trim().ToLowerInvariant() switch
    {
        "completed" => "Completed",
        "busy" => "Busy",
        "no-answer" or "noanswer" => "NoAnswer",
        "failed" or "canceled" or "cancelled" => "Failed",
        "in-progress" or "inprogress" => "InProgress",
        _ => "Ringing"
    };

    public static bool IsFinished(string? status) => status is "Completed" or "Busy" or "NoAnswer" or "Failed";

    public static string FormatDuration(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    /// <summary>Recording links are only kept when they are plain https links; anything else could be a script or tracking link.</summary>
    public static string? SafeRecordingUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.Length <= 1000 && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? u.ToString() : null;

    public static bool KeyMatches(string? given, string? expected)
    {
        if (string.IsNullOrEmpty(given) || string.IsNullOrEmpty(expected)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(given));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public class CallDto
{
    public string? Id { get; init; }
    public string? Direction { get; init; }
    public string? CustomerNumber { get; init; }
    public string? AgentNumber { get; init; }
    public string? Status { get; init; }
    public int DurationSeconds { get; init; }
    public string? Duration { get; init; }
    public string? RecordingUrl { get; init; }
    public string? Notes { get; init; }
    public string? Error { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? RelatedName { get; init; }
    public string? UserName { get; init; }
    public DateTime? AtUtc { get; init; }
}

public class GetCallsResult { public List<CallDto> Data { get; init; } = new(); public bool Configured { get; init; } }
public class GetCallsRequest : IRequest<GetCallsResult> { public string? RelatedId { get; init; } }

public class GetCallsHandler : IRequestHandler<GetCallsRequest, GetCallsResult>
{
    private readonly IQueryContext _context;
    private readonly ICallProvider _provider;
    public GetCallsHandler(IQueryContext context, ICallProvider provider) { _context = context; _provider = provider; }

    public async Task<GetCallsResult> Handle(GetCallsRequest request, CancellationToken cancellationToken)
    {
        var q = _context.CallLog.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.RelatedId)) q = q.Where(x => x.RelatedId == request.RelatedId);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync(cancellationToken);
        return new GetCallsResult
        {
            Configured = _provider.IsConfigured,
            Data = rows.Select(x => new CallDto
            {
                Id = x.Id, Direction = x.Direction, CustomerNumber = x.CustomerNumber, AgentNumber = x.AgentNumber, Status = x.Status, DurationSeconds = x.DurationSeconds,
                Duration = CallRules.FormatDuration(x.DurationSeconds), RecordingUrl = x.RecordingUrl, Notes = x.Notes, Error = x.Error, RelatedType = x.RelatedType,
                RelatedId = x.RelatedId, RelatedName = x.RelatedName, UserName = x.UserName, AtUtc = x.CreatedAtUtc
            }).ToList()
        };
    }
}

public class StartCallResult { public string? Id { get; init; } public string? Status { get; init; } public string? Error { get; init; } }

public class StartCallRequest : IRequest<StartCallResult>
{
    public string? To { get; init; }
    public string? AgentNumber { get; init; }
    public string? StatusCallbackUrl { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class StartCallValidator : AbstractValidator<StartCallRequest>
{
    public StartCallValidator()
    {
        RuleFor(x => x.To).Must(t => ChannelRules.NormalizePhone(t) != null).WithMessage("Enter a valid customer number with country code.");
        RuleFor(x => x.AgentNumber).Must(t => ChannelRules.NormalizePhone(t) != null).WithMessage("Enter your own phone number, the one Exotel should ring first.");
    }
}

public class StartCallHandler : IRequestHandler<StartCallRequest, StartCallResult>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<CallLog> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICallProvider _provider;

    public StartCallHandler(IQueryContext context, ICommandRepository<CallLog> repository, IUnitOfWork unitOfWork, ICallProvider provider)
    { _context = context; _repository = repository; _unitOfWork = unitOfWork; _provider = provider; }

    public async Task<StartCallResult> Handle(StartCallRequest request, CancellationToken cancellationToken)
    {
        if (!_provider.IsConfigured) throw new Exception("Calling is not set up yet. An administrator can add the Exotel keys under Settings > Connections.");
        var to = ChannelRules.NormalizePhone(request.To)!;
        var agent = ChannelRules.NormalizePhone(request.AgentNumber)!;
        var match = await ContactMatcher.FindAsync(_context, to, cancellationToken);

        var result = await _provider.StartAsync(agent, to, request.StatusCallbackUrl, cancellationToken);
        var log = new CallLog
        {
            Direction = "Out", CustomerNumber = to, AgentNumber = agent, Status = result.Success ? "Ringing" : "Failed", ExternalId = result.ExternalId,
            Error = result.Error is { Length: > 500 } ? result.Error[..500] : result.Error, RelatedType = match?.Type, RelatedId = match?.Id, RelatedName = match?.Name,
            UserId = request.UserId, UserName = request.UserName, CreatedById = request.UserId
        };
        await _repository.CreateAsync(log, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new StartCallResult { Id = log.Id, Status = log.Status, Error = log.Error };
    }
}

public class SaveCallNoteRequest : IRequest<bool> { public string? Id { get; init; } public string? Notes { get; init; } public string? UserId { get; set; } }

public class SaveCallNoteValidator : AbstractValidator<SaveCallNoteRequest>
{
    public SaveCallNoteValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Notes).MaximumLength(2000); }
}

public class SaveCallNoteHandler : IRequestHandler<SaveCallNoteRequest, bool>
{
    private readonly ICommandRepository<CallLog> _repository;
    private readonly ICommandRepository<Activity> _activities;
    private readonly IUnitOfWork _unitOfWork;
    public SaveCallNoteHandler(ICommandRepository<CallLog> repository, ICommandRepository<Activity> activities, IUnitOfWork unitOfWork) { _repository = repository; _activities = activities; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(SaveCallNoteRequest request, CancellationToken cancellationToken)
    {
        var log = await _repository.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Call not found.");
        log.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        log.UpdatedById = request.UserId;
        _repository.Update(log);
        if (!string.IsNullOrEmpty(log.ActivityId))
        {
            var activity = await _activities.GetAsync(log.ActivityId, cancellationToken);
            if (activity != null) { activity.Description = log.Notes; activity.Outcome = log.Notes; _activities.Update(activity); }
        }
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

/// <summary>What the provider's status callback tells us.</summary>
public class ProcessCallCallbackRequest : IRequest<bool>
{
    public string? CallSid { get; init; }
    public string? Status { get; init; }
    public int DurationSeconds { get; init; }
    public string? RecordingUrl { get; init; }
    public string? Direction { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
}

public class ProcessCallCallbackHandler : IRequestHandler<ProcessCallCallbackRequest, bool>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<CallLog> _repository;
    private readonly ICommandRepository<Activity> _activities;
    private readonly ICommandRepository<Notification> _notifications;
    private readonly NumberSequenceService _numbers;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessCallCallbackHandler(IQueryContext context, ICommandRepository<CallLog> repository, ICommandRepository<Activity> activities,
        ICommandRepository<Notification> notifications, NumberSequenceService numbers, IUnitOfWork unitOfWork)
    { _context = context; _repository = repository; _activities = activities; _notifications = notifications; _numbers = numbers; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(ProcessCallCallbackRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CallSid)) return false;
        var status = CallRules.MapStatus(request.Status);
        var duration = Math.Clamp(request.DurationSeconds, 0, 86_400);

        var log = await _repository.GetQuery().FirstOrDefaultAsync(x => x.ExternalId == request.CallSid && !x.IsDeleted, cancellationToken);
        if (log == null)
        {
            // a call somebody made to our number: keep it as an inbound call
            var incoming = (request.Direction ?? "").StartsWith("inbound", StringComparison.OrdinalIgnoreCase) || (request.Direction ?? "").StartsWith("incoming", StringComparison.OrdinalIgnoreCase);
            if (!incoming) return false;
            var number = ChannelRules.NormalizePhone("+" + new string((request.From ?? "").Where(char.IsDigit).ToArray())) ?? request.From;
            var match = number == null ? null : await ContactMatcher.FindAsync(_context, number, cancellationToken);
            log = new CallLog { Direction = "In", CustomerNumber = number, AgentNumber = request.To, ExternalId = request.CallSid, RelatedType = match?.Type, RelatedId = match?.Id, RelatedName = match?.Name, Status = status };
            await _repository.CreateAsync(log, cancellationToken);
            if (status != "Completed")
                await _notifications.CreateAsync(new Notification
                {
                    Title = "Missed call", Message = $"{match?.Name ?? number}", Type = "Warning", Url = "/Calls/CallLog", UserId = match?.OwnerUserId, DedupKey = "call-missed:" + request.CallSid
                }, cancellationToken);
        }

        var oldFinished = CallRules.IsFinished(log.Status);
        if (!oldFinished || CallRules.IsFinished(status))
        {
            log.Status = status;
            if (duration > 0) log.DurationSeconds = duration;
        }
        var recording = CallRules.SafeRecordingUrl(request.RecordingUrl);
        if (recording != null) log.RecordingUrl = recording;

        if (status == "Completed" && string.IsNullOrEmpty(log.ActivityId) && log.UserId != null)
        {
            var activity = new Activity
            {
                Number = _numbers.GenerateNumber(nameof(Activity), "", "ACT"), Type = ActivityType.Call, Status = ActivityStatus.Done, Priority = "Normal",
                Subject = $"{(log.Direction == "In" ? "Call from" : "Call with")} {log.RelatedName ?? "+" + log.CustomerNumber} ({CallRules.FormatDuration(log.DurationSeconds)})",
                StartAt = DateTime.Now.AddSeconds(-log.DurationSeconds), EndAt = DateTime.Now, CompletedAt = DateTime.Now, RelatedType = log.RelatedType, RelatedId = log.RelatedId,
                RelatedName = log.RelatedName, OwnerUserId = log.UserId, AssignedToUserId = log.UserId, CreatedById = log.UserId
            };
            await _activities.CreateAsync(activity, cancellationToken);
            log.ActivityId = activity.Id;
        }

        _repository.Update(log);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}
