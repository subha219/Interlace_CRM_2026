using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LeadManager.Commands;

public class ConvertLeadResult
{
    public bool Converted { get; set; }
    public Customer? Data { get; set; }
    public int ContactsCreated { get; set; }

    /// <summary>Set when a customer with the same e-mail already exists and no choice was made yet.</summary>
    public string? DuplicateCustomerId { get; set; }
    public string? DuplicateCustomerName { get; set; }
}

public class ConvertLeadRequest : IRequest<ConvertLeadResult>
{
    public string? LeadId { get; init; }

    /// <summary>Link the lead to this customer instead of creating a new one.</summary>
    public string? ExistingCustomerId { get; init; }
    public string? CustomerGroupId { get; init; }
    public string? CustomerCategoryId { get; init; }
    public bool CreateContacts { get; init; } = true;
    public string? CreatedById { get; init; }
}

public class ConvertLeadValidator : AbstractValidator<ConvertLeadRequest>
{
    public ConvertLeadValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
    }
}

public class ConvertLeadHandler : IRequestHandler<ConvertLeadRequest, ConvertLeadResult>
{
    private readonly ICommandRepository<Lead> _leadRepository;
    private readonly ICommandRepository<Customer> _customerRepository;
    private readonly ICommandRepository<CustomerContact> _contactRepository;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;

    public ConvertLeadHandler(
        ICommandRepository<Lead> leadRepository,
        ICommandRepository<Customer> customerRepository,
        ICommandRepository<CustomerContact> contactRepository,
        IQueryContext context,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService)
    {
        _leadRepository = leadRepository;
        _customerRepository = customerRepository;
        _contactRepository = contactRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
    }

    public async Task<ConvertLeadResult> Handle(ConvertLeadRequest request, CancellationToken cancellationToken)
    {
        var lead = await _leadRepository.GetAsync(request.LeadId ?? string.Empty, cancellationToken)
            ?? throw new Exception($"Lead not found: {request.LeadId}");

        if (!string.IsNullOrEmpty(lead.ConvertedCustomerId))
        {
            throw new Exception("This lead has already been converted.");
        }

        var leadContacts = await _context.LeadContact
            .AsNoTracking()
            .Where(x => x.LeadId == lead.Id && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        Customer? customer = null;

        if (!string.IsNullOrEmpty(request.ExistingCustomerId))
        {
            customer = await _customerRepository.GetAsync(request.ExistingCustomerId, cancellationToken)
                ?? throw new Exception($"Customer not found: {request.ExistingCustomerId}");
        }
        else if (!string.IsNullOrWhiteSpace(lead.CompanyEmail))
        {
            var email = lead.CompanyEmail.Trim().ToLower();
            var duplicate = await _context.Customer
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.EmailAddress != null && x.EmailAddress.ToLower() == email)
                .Select(x => new { x.Id, x.Name })
                .FirstOrDefaultAsync(cancellationToken);

            if (duplicate != null)
            {
                return new ConvertLeadResult
                {
                    Converted = false,
                    DuplicateCustomerId = duplicate.Id,
                    DuplicateCustomerName = duplicate.Name
                };
            }
        }

        if (customer == null)
        {
            var groupId = request.CustomerGroupId;
            if (string.IsNullOrEmpty(groupId))
            {
                groupId = await _context.CustomerGroup.AsNoTracking().Where(x => !x.IsDeleted)
                    .OrderBy(x => x.Name).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            }

            var categoryId = request.CustomerCategoryId;
            if (string.IsNullOrEmpty(categoryId))
            {
                categoryId = await _context.CustomerCategory.AsNoTracking().Where(x => !x.IsDeleted)
                    .OrderBy(x => x.Name).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            }

            customer = new Customer
            {
                CreatedById = request.CreatedById,
                Name = string.IsNullOrWhiteSpace(lead.CompanyName) ? lead.Title : lead.CompanyName,
                Number = _numberSequenceService.GenerateNumber(nameof(Customer), "", "CST"),
                Description = lead.CompanyDescription,
                Street = lead.CompanyAddressStreet,
                City = lead.CompanyAddressCity,
                State = lead.CompanyAddressState,
                ZipCode = lead.CompanyAddressZipCode,
                Country = lead.CompanyAddressCountry,
                PhoneNumber = lead.CompanyPhoneNumber,
                FaxNumber = lead.CompanyFaxNumber,
                EmailAddress = lead.CompanyEmail,
                Website = lead.CompanyWebsite,
                WhatsApp = lead.CompanyWhatsApp,
                LinkedIn = lead.CompanyLinkedIn,
                Facebook = lead.CompanyFacebook,
                Instagram = lead.CompanyInstagram,
                TwitterX = lead.CompanyTwitter,
                CustomerGroupId = groupId,
                CustomerCategoryId = categoryId,
                LifecycleStage = LifecycleStage.Customer,
                Source = lead.LeadSource,
                Tags = lead.Tags,
                ConsentMarketing = lead.ConsentMarketing,
                ConsentUpdatedAtUtc = lead.ConsentUpdatedAtUtc,
                SourceLeadId = lead.Id,
                OwnerUserId = lead.OwnerUserId
            };
            await _customerRepository.CreateAsync(customer, cancellationToken);
        }
        else if (customer.LifecycleStage == null || customer.LifecycleStage < LifecycleStage.Customer)
        {
            customer.LifecycleStage = LifecycleStage.Customer;
            _customerRepository.Update(customer);
        }

        var created = 0;
        if (request.CreateContacts)
        {
            var existingEmails = await _context.CustomerContact
                .AsNoTracking()
                .Where(x => x.CustomerId == customer.Id && !x.IsDeleted && x.EmailAddress != null)
                .Select(x => x.EmailAddress!.ToLower())
                .ToListAsync(cancellationToken);

            foreach (var lc in leadContacts)
            {
                if (!string.IsNullOrWhiteSpace(lc.Email) && existingEmails.Contains(lc.Email.Trim().ToLower()))
                {
                    continue;
                }

                await _contactRepository.CreateAsync(new CustomerContact
                {
                    CreatedById = request.CreatedById,
                    Name = lc.FullName,
                    Number = _numberSequenceService.GenerateNumber(nameof(CustomerContact), "", "CC"),
                    PhoneNumber = string.IsNullOrWhiteSpace(lc.MobileNumber) ? lc.PhoneNumber : lc.MobileNumber,
                    EmailAddress = lc.Email,
                    Description = lc.Description,
                    ConsentMarketing = lead.ConsentMarketing,
                    Source = lead.LeadSource,
                    CustomerId = customer.Id
                }, cancellationToken);
                created++;
            }
        }

        //>>> a converted lead is a won deal
        if (lead.ClosingStatus != ClosingStatus.ClosedWon)
        {
            lead.PipelineStage = PipelineStage.Closed;
            lead.ClosingStatus = ClosingStatus.ClosedWon;
            lead.DateClosingActual ??= DateTime.Now;
            lead.AmountClosed ??= lead.AmountTargeted;
        }
        lead.ConvertedCustomerId = customer.Id;
        lead.ConvertedAtUtc = DateTime.UtcNow;
        lead.UpdatedById = request.CreatedById;
        _leadRepository.Update(lead);

        await _unitOfWork.SaveAsync(cancellationToken);

        return new ConvertLeadResult
        {
            Converted = true,
            Data = customer,
            ContactsCreated = created
        };
    }
}
