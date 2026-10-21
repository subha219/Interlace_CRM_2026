using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LeadManager.Commands;

public class CreateLeadResult
{
    public Lead? Data { get; set; }
}

public class CreateLeadRequest : IRequest<CreateLeadResult>
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? CompanyName { get; init; }
    public string? CompanyAddressStreet { get; init; }
    public string? CompanyAddressCity { get; init; }
    public string? CompanyAddressState { get; init; }
    public string? CompanyPhoneNumber { get; init; }
    public string? CompanyEmail { get; init; }
    public DateTime? DateProspecting { get; init; }
    public DateTime? DateClosingEstimation { get; init; }
    public DateTime? DateClosingActual { get; init; }
    public double? AmountTargeted { get; init; }
    public double? AmountClosed { get; init; }
    public double? BudgetScore { get; init; }
    public double? AuthorityScore { get; init; }
    public double? NeedScore { get; init; }
    public double? TimelineScore { get; init; }
    public string? PipelineStage { get; init; }
    public string? ClosingStatus { get; init; }
    public string? CampaignId { get; init; }
    public string? LeadSource { get; init; }
    public string? Tags { get; init; }
    public bool? ConsentMarketing { get; init; }
    public string? ClosingNote { get; init; }
    public string? SalesTeamId { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateLeadValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
        //>>> SalesTeamId is optional: when empty the lead is auto-assigned to the least loaded team
        RuleFor(x => x.CompanyName).NotEmpty();
        RuleFor(x => x.CompanyAddressStreet).NotEmpty();
        RuleFor(x => x.CompanyAddressCity).NotEmpty();
        RuleFor(x => x.CompanyAddressState).NotEmpty();
        RuleFor(x => x.CompanyPhoneNumber).NotEmpty();
        RuleFor(x => x.CompanyEmail).NotEmpty();
        RuleFor(x => x.DateProspecting).NotNull();
        RuleFor(x => x.DateClosingEstimation).NotNull();
        RuleFor(x => x.AmountTargeted).NotNull();
        RuleFor(x => x.AmountClosed).NotNull();
        RuleFor(x => x.BudgetScore).NotNull();
        RuleFor(x => x.AuthorityScore).NotNull();
        RuleFor(x => x.NeedScore).NotNull();
        RuleFor(x => x.TimelineScore).NotNull();
        RuleFor(x => x.PipelineStage).NotEmpty();
        RuleFor(x => x.CampaignId).NotEmpty();
    }
}

public class CreateLeadHandler : IRequestHandler<CreateLeadRequest, CreateLeadResult>
{
    private readonly ICommandRepository<Lead> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly ICommandRepository<SalesTeam> _salesTeamRepository;

    public CreateLeadHandler(
        ICommandRepository<Lead> repository,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService,
        ICommandRepository<SalesTeam> salesTeamRepository
    )
    {
        _salesTeamRepository = salesTeamRepository;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
    }

    public async Task<CreateLeadResult> Handle(CreateLeadRequest request, CancellationToken cancellationToken = default)
    {
        var salesTeamId = request.SalesTeamId;
        if (string.IsNullOrWhiteSpace(salesTeamId))
        {
            salesTeamId = await PickLeastLoadedTeamAsync(cancellationToken);
        }

        var entity = new Lead
        {
            CreatedById = request.CreatedById,
            Number = _numberSequenceService.GenerateNumber(nameof(Lead), "", "LEA"),
            Title = request.Title,
            SalesTeamId = salesTeamId,
            Description = request.Description,
            CompanyName = request.CompanyName,
            CompanyAddressStreet = request.CompanyAddressStreet,
            CompanyAddressCity = request.CompanyAddressCity,
            CompanyAddressState = request.CompanyAddressState,
            CompanyPhoneNumber = request.CompanyPhoneNumber,
            CompanyEmail = request.CompanyEmail,
            DateProspecting = request.DateProspecting,
            DateClosingEstimation = request.DateClosingEstimation,
            DateClosingActual = request.DateClosingActual,
            AmountTargeted = request.AmountTargeted,
            AmountClosed = request.AmountClosed,
            BudgetScore = request.BudgetScore,
            AuthorityScore = request.AuthorityScore,
            NeedScore = request.NeedScore,
            TimelineScore = request.TimelineScore,
            PipelineStage = (PipelineStage)int.Parse(request.PipelineStage!),
            CampaignId = request.CampaignId,
            LeadSource = request.LeadSource,
            Tags = request.Tags,
            ConsentMarketing = request.ConsentMarketing,
            ConsentUpdatedAtUtc = request.ConsentMarketing == null ? null : DateTime.UtcNow,
            ClosingNote = request.ClosingNote
        };

        if (!string.IsNullOrEmpty(request.ClosingStatus))
        {
            entity.ClosingStatus = (ClosingStatus)int.Parse(request.ClosingStatus!);
        }

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new CreateLeadResult
        {
            Data = entity
        };
    }

    /// <summary>
    /// Picks the sales team that currently holds the fewest open leads (ties go to the team name order).
    /// </summary>
    private async Task<string?> PickLeastLoadedTeamAsync(CancellationToken cancellationToken)
    {
        var teams = await _salesTeamRepository.GetQuery()
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        if (teams.Count == 0)
        {
            return null;
        }

        var openLeads = await _repository.GetQuery()
            .Where(x => !x.IsDeleted && x.SalesTeamId != null
                        && x.ClosingStatus != ClosingStatus.ClosedWon && x.ClosingStatus != ClosingStatus.ClosedLost)
            .GroupBy(x => x.SalesTeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return teams
            .OrderBy(t => openLeads.FirstOrDefault(l => l.TeamId == t.Id)?.Count ?? 0)
            .ThenBy(t => t.Name)
            .First().Id;
    }
}
