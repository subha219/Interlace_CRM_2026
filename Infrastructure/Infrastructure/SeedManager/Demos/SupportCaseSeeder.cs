using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Application.Features.SupportCaseManager;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SeedManager.Demos;

public class SupportCaseSeeder
{
    private readonly ICommandRepository<SupportCase> _caseRepository;
    private readonly ICommandRepository<Customer> _customerRepository;
    private readonly ICommandRepository<SalesRepresentative> _salesRepresentativeRepository;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly IUnitOfWork _unitOfWork;

    public SupportCaseSeeder(
        ICommandRepository<SupportCase> caseRepository,
        ICommandRepository<Customer> customerRepository,
        ICommandRepository<SalesRepresentative> salesRepresentativeRepository,
        NumberSequenceService numberSequenceService,
        IUnitOfWork unitOfWork
    )
    {
        _caseRepository = caseRepository;
        _customerRepository = customerRepository;
        _salesRepresentativeRepository = salesRepresentativeRepository;
        _numberSequenceService = numberSequenceService;
        _unitOfWork = unitOfWork;
    }

    public async Task GenerateDataAsync()
    {
        var random = new Random();

        var customerIds = await _customerRepository.GetQuery()
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync();

        var representativeIds = await _salesRepresentativeRepository.GetQuery()
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync();

        if (customerIds.Count == 0)
        {
            return;
        }

        var titles = new[]
        {
            "Unable to log in to the customer portal",
            "Invoice amount does not match the quotation",
            "Request for product demo and pricing",
            "Delivery delayed beyond the agreed date",
            "Need to update billing contact details",
            "Report export is missing columns",
            "Questions about the renewal terms",
            "Integration with accounting software fails",
            "Product arrived damaged",
            "Request to upgrade the subscription plan"
        };

        var statuses = new[] { CaseStatus.New, CaseStatus.Open, CaseStatus.Pending, CaseStatus.Resolved, CaseStatus.Closed };
        var priorities = new[] { CasePriority.Low, CasePriority.Normal, CasePriority.Normal, CasePriority.High, CasePriority.Critical };
        var channels = new[] { CaseChannel.Email, CaseChannel.Phone, CaseChannel.Web, CaseChannel.Chat, CaseChannel.SocialMedia };

        for (var i = 0; i < 25; i++)
        {
            var status = statuses[random.Next(statuses.Length)];
            var priority = priorities[random.Next(priorities.Length)];
            var created = DateTime.UtcNow.AddHours(-random.Next(1, 24 * 20));

            var entity = new SupportCase
            {
                Number = _numberSequenceService.GenerateNumber(nameof(SupportCase), "", "CASE"),
                Title = titles[random.Next(titles.Length)],
                Description = "Customer reported this issue through the support channel and is waiting for an update.",
                Status = status,
                Priority = priority,
                Channel = channels[random.Next(channels.Length)],
                CustomerId = customerIds[random.Next(customerIds.Count)],
                SalesRepresentativeId = representativeIds.Count > 0 ? representativeIds[random.Next(representativeIds.Count)] : null,
                DueDate = SupportCaseSla.CalculateDueDate(priority, created)
            };

            if (status == CaseStatus.Resolved || status == CaseStatus.Closed)
            {
                entity.ResolvedAtUtc = created.AddHours(random.Next(1, 48));
                entity.Resolution = "Issue investigated and resolved. Customer confirmed the fix.";
            }

            await _caseRepository.CreateAsync(entity);
        }

        await _unitOfWork.SaveAsync();
    }
}
