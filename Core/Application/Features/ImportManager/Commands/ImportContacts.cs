using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ImportManager.Commands;

public class ImportContactsRequest : IRequest<ImportResult>
{
    public string? Csv { get; init; }
    public bool Preview { get; init; }
    public string? CreatedById { get; init; }
}

/// <summary>Imports people into existing customers. The customer is matched by its name or number.</summary>
public class ImportContactsHandler : IRequestHandler<ImportContactsRequest, ImportResult>
{
    private const int MaxRows = 5000;

    private readonly ICommandRepository<CustomerContact> _contacts;
    private readonly ICommandRepository<Customer> _customers;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly IUnitOfWork _unitOfWork;

    public ImportContactsHandler(
        ICommandRepository<CustomerContact> contacts,
        ICommandRepository<Customer> customers,
        NumberSequenceService numberSequenceService,
        IUnitOfWork unitOfWork)
    {
        _contacts = contacts;
        _customers = customers;
        _numberSequenceService = numberSequenceService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult> Handle(ImportContactsRequest request, CancellationToken cancellationToken)
    {
        var table = new CsvTable(request.Csv);
        var cName = table.Col("name", "contact", "contactname", "fullname");
        var cCustomer = table.Col("customer", "customername", "company", "companyname", "customernumber");
        if (cName < 0 || cCustomer < 0)
        {
            throw new Exception("The header must contain 'Name' and 'Customer' columns.");
        }
        var cEmail = table.Col("email", "emailaddress", "mail");
        var cPhone = table.Col("phone", "phonenumber", "mobile", "telephone");
        var cJob = table.Col("jobtitle", "title", "position", "role");
        var cNotes = table.Col("description", "notes", "note");
        var cConsent = table.Col("consent", "marketingconsent", "optin");

        var customers = await _customers.GetQuery().Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.Name, x.Number }).ToListAsync(cancellationToken);
        var byKey = new Dictionary<string, string>();
        foreach (var c in customers)
        {
            if (!string.IsNullOrWhiteSpace(c.Name)) byKey.TryAdd(c.Name.Trim().ToLowerInvariant(), c.Id);
            if (!string.IsNullOrWhiteSpace(c.Number)) byKey.TryAdd(c.Number.Trim().ToLowerInvariant(), c.Id);
        }

        var knownEmails = (await _contacts.GetQuery().Where(x => !x.IsDeleted && x.EmailAddress != null)
            .Select(x => new { x.CustomerId, x.EmailAddress }).ToListAsync(cancellationToken))
            .Select(x => (x.CustomerId, Email: x.EmailAddress!.Trim().ToLowerInvariant())).ToHashSet();

        var issues = new List<ImportRowIssue>();
        var imported = 0;
        var total = 0;

        for (int r = 1; r < table.Rows.Count && r <= MaxRows; r++)
        {
            var row = table.Rows[r];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            total++;

            var name = CsvTable.Cell(row, cName);
            var customerKey = CsvTable.Cell(row, cCustomer).ToLowerInvariant();
            var email = CsvTable.Cell(row, cEmail);

            if (name.Length < 2) { issues.Add(new ImportRowIssue(r + 1, name, "Name is missing.")); continue; }
            if (!byKey.TryGetValue(customerKey, out var customerId)) { issues.Add(new ImportRowIssue(r + 1, name, $"Customer '{CsvTable.Cell(row, cCustomer)}' was not found.")); continue; }
            if (email.Length > 0 && !email.Contains('@')) { issues.Add(new ImportRowIssue(r + 1, name, $"'{email}' is not a valid email address.")); continue; }
            if (email.Length > 0 && !knownEmails.Add((customerId, email.ToLowerInvariant()))) { issues.Add(new ImportRowIssue(r + 1, name, $"Duplicate: {email} already exists for this customer.")); continue; }

            if (request.Preview) { imported++; continue; }

            await _contacts.CreateAsync(new CustomerContact
            {
                CreatedById = request.CreatedById,
                Number = _numberSequenceService.GenerateNumber(nameof(CustomerContact), "", "CC"),
                Name = name,
                JobTitle = CsvTable.NullIfEmpty(CsvTable.Cell(row, cJob)),
                EmailAddress = CsvTable.NullIfEmpty(email),
                PhoneNumber = CsvTable.NullIfEmpty(CsvTable.Cell(row, cPhone)),
                Description = CsvTable.NullIfEmpty(CsvTable.Cell(row, cNotes, 4000)),
                ConsentMarketing = CsvTable.ParseConsent(CsvTable.Cell(row, cConsent)),
                Source = "Import",
                CustomerId = customerId
            }, cancellationToken);
            imported++;
        }

        if (!request.Preview && imported > 0)
        {
            await _unitOfWork.SaveAsync(cancellationToken);
        }

        return new ImportResult { Preview = request.Preview, TotalRows = total, Imported = imported, Skipped = issues.Count, Issues = issues.Take(200).ToList() };
    }
}
