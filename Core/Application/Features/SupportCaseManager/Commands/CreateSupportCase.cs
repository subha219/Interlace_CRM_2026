using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.SupportCaseManager.Commands;

public class CreateSupportCaseResult
{
    public SupportCase? Data { get; set; }
}

public class CreateSupportCaseRequest : IRequest<CreateSupportCaseResult>
{
    public string? Title { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Channel { get; init; }
    public string? CustomerId { get; init; }
    public string? SalesRepresentativeId { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Description { get; init; }
    public string? Resolution { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateSupportCaseValidator : AbstractValidator<CreateSupportCaseRequest>
{
    public CreateSupportCaseValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.Priority).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public class CreateSupportCaseHandler : IRequestHandler<CreateSupportCaseRequest, CreateSupportCaseResult>
{
    private readonly ICommandRepository<SupportCase> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly Application.Common.CQS.Queries.IQueryContext _context;

    public CreateSupportCaseHandler(
        ICommandRepository<SupportCase> repository,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService,
        Application.Common.CQS.Queries.IQueryContext context
    )
    {
        _context = context;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
    }

    public async Task<CreateSupportCaseResult> Handle(CreateSupportCaseRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new SupportCase();
        entity.CreatedById = request.CreatedById;
        entity.Number = _numberSequenceService.GenerateNumber(nameof(SupportCase), "", "CASE");
        entity.Title = request.Title;
        entity.Status = (CaseStatus)int.Parse(request.Status!);
        entity.Priority = (CasePriority)int.Parse(request.Priority!);
        entity.Channel = string.IsNullOrEmpty(request.Channel) ? null : (CaseChannel?)int.Parse(request.Channel);
        entity.CustomerId = request.CustomerId;
        entity.SalesRepresentativeId = request.SalesRepresentativeId;
        entity.DueDate = request.DueDate;
        entity.Description = request.Description;
        entity.Resolution = request.Resolution;
        var sla = await SlaConfig.LoadAsync(_context, cancellationToken);
        if (entity.DueDate == null)
        {
            entity.DueDate = sla.ResolutionDue(entity.Priority);
        }
        entity.FirstResponseDue = sla.FirstResponseDueAt(entity.Priority);

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);


        return new CreateSupportCaseResult
        {
            Data = entity
        };
    }
}
