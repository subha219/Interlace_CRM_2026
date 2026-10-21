using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Common.Services.EmailManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.EmailManager.Commands;

public class SendCrmEmailResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? LogId { get; init; }
}

public class SendCrmEmailRequest : IRequest<SendCrmEmailResult>
{
    public string? To { get; init; }
    public string? Subject { get; init; }
    public string? Body { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? RelatedName { get; init; }

    /// <summary>Marketing mail is refused for records that opted out.</summary>
    public bool IsMarketing { get; init; }

    /// <summary>Filled by the controller from the signed-in user.</summary>
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
}

public class SendCrmEmailValidator : AbstractValidator<SendCrmEmailRequest>
{
    public SendCrmEmailValidator()
    {
        RuleFor(x => x.To).NotEmpty().EmailAddress();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public class SendCrmEmailHandler : IRequestHandler<SendCrmEmailRequest, SendCrmEmailResult>
{
    private readonly IEmailService _emailService;
    private readonly IQueryContext _context;
    private readonly ICommandRepository<EmailLog> _logs;
    private readonly ICommandRepository<Activity> _activities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;

    public SendCrmEmailHandler(
        IEmailService emailService,
        IQueryContext context,
        ICommandRepository<EmailLog> logs,
        ICommandRepository<Activity> activities,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService)
    {
        _emailService = emailService;
        _context = context;
        _logs = logs;
        _activities = activities;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
    }

    public async Task<SendCrmEmailResult> Handle(SendCrmEmailRequest request, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>
        {
            ["sender"] = request.UserName,
            ["email"] = request.To
        };

        bool? consent = null;

        if (request.RelatedType == "Customer" && !string.IsNullOrEmpty(request.RelatedId))
        {
            var c = await _context.Customer.AsNoTracking().Where(x => x.Id == request.RelatedId)
                .Select(x => new { x.Name, x.Number, x.ConsentMarketing }).FirstOrDefaultAsync(cancellationToken);
            if (c != null) { values["name"] = c.Name; values["company"] = c.Name; values["number"] = c.Number; consent = c.ConsentMarketing; }
        }
        else if (request.RelatedType == "Lead" && !string.IsNullOrEmpty(request.RelatedId))
        {
            var l = await _context.Lead.AsNoTracking().Where(x => x.Id == request.RelatedId)
                .Select(x => new { x.Title, x.CompanyName, x.Number, x.ConsentMarketing }).FirstOrDefaultAsync(cancellationToken);
            if (l != null) { values["name"] = l.CompanyName ?? l.Title; values["company"] = l.CompanyName; values["number"] = l.Number; consent = l.ConsentMarketing; }
        }

        if (request.IsMarketing && consent == false)
        {
            return new SendCrmEmailResult { Success = false, Error = "This record has opted out of marketing e-mail." };
        }

        var subject = EmailRenderer.RenderSubject(request.Subject, values);
        var html = EmailRenderer.Render(request.Body, values);

        var result = await _emailService.SendEmailWithResultAsync(request.To!, subject, html, request.UserEmail, request.UserName);

        var log = new EmailLog
        {
            CreatedById = request.UserId,
            ToEmail = request.To,
            Subject = subject,
            Body = html,
            Success = result.Success,
            Error = result.Error,
            RelatedType = string.IsNullOrEmpty(request.RelatedType) ? null : request.RelatedType,
            RelatedId = string.IsNullOrEmpty(request.RelatedType) ? null : request.RelatedId,
            RelatedName = string.IsNullOrEmpty(request.RelatedType) ? null : request.RelatedName,
            SentByUserId = request.UserId,
            SentByName = request.UserName,
            SentAtUtc = DateTime.UtcNow
        };
        await _logs.CreateAsync(log, cancellationToken);

        //>>> a sent e-mail also shows up in the activity timeline of the record
        if (result.Success && !string.IsNullOrEmpty(request.RelatedType) && !string.IsNullOrEmpty(request.RelatedId))
        {
            await _activities.CreateAsync(new Activity
            {
                CreatedById = request.UserId,
                OwnerUserId = request.UserId,
                Number = _numberSequenceService.GenerateNumber(nameof(Activity), "", "ACT"),
                Subject = "E-mail: " + subject,
                Description = "Sent to " + request.To,
                Type = ActivityType.Email,
                Status = ActivityStatus.Done,
                Priority = "Normal",
                StartAt = DateTime.Now,
                CompletedAt = DateTime.Now,
                RelatedType = request.RelatedType,
                RelatedId = request.RelatedId,
                RelatedName = request.RelatedName,
                AssignedToUserId = request.UserId
            }, cancellationToken);
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        return new SendCrmEmailResult { Success = result.Success, Error = result.Error, LogId = log.Id };
    }
}

public class SaveEmailTemplateResult { public EmailTemplate? Data { get; set; } }

public class SaveEmailTemplateRequest : IRequest<SaveEmailTemplateResult>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Subject { get; init; }
    public string? Body { get; init; }
    public bool IsMarketing { get; init; }
    public string? UserId { get; set; }
}

public class SaveEmailTemplateValidator : AbstractValidator<SaveEmailTemplateRequest>
{
    public SaveEmailTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public class SaveEmailTemplateHandler : IRequestHandler<SaveEmailTemplateRequest, SaveEmailTemplateResult>
{
    private readonly ICommandRepository<EmailTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveEmailTemplateHandler(ICommandRepository<EmailTemplate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SaveEmailTemplateResult> Handle(SaveEmailTemplateRequest request, CancellationToken cancellationToken)
    {
        var creating = string.IsNullOrEmpty(request.Id);
        var entity = creating
            ? new EmailTemplate { CreatedById = request.UserId }
            : await _repository.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Template not found.");

        entity.Name = request.Name;
        entity.Subject = request.Subject;
        entity.Body = request.Body;
        entity.IsMarketing = request.IsMarketing;
        if (!creating) entity.UpdatedById = request.UserId;

        if (creating) await _repository.CreateAsync(entity, cancellationToken);
        else _repository.Update(entity);

        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveEmailTemplateResult { Data = entity };
    }
}

public class DeleteEmailTemplateRequest : IRequest<SaveEmailTemplateResult>
{
    public string? Id { get; init; }
    public string? UserId { get; set; }
}

public class DeleteEmailTemplateValidator : AbstractValidator<DeleteEmailTemplateRequest>
{
    public DeleteEmailTemplateValidator() { RuleFor(x => x.Id).NotEmpty(); }
}

public class DeleteEmailTemplateHandler : IRequestHandler<DeleteEmailTemplateRequest, SaveEmailTemplateResult>
{
    private readonly ICommandRepository<EmailTemplate> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteEmailTemplateHandler(ICommandRepository<EmailTemplate> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SaveEmailTemplateResult> Handle(DeleteEmailTemplateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Template not found.");
        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveEmailTemplateResult { Data = entity };
    }
}
