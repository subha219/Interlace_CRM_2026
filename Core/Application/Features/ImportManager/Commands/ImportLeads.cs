using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ImportManager.Commands;

public class ImportLeadsRequest : IRequest<ImportResult>
{
    public string? Csv { get; init; }
    public bool Preview { get; init; }
    public string? CreatedById { get; init; }
}

public class ImportLeadsHandler : IRequestHandler<ImportLeadsRequest, ImportResult>
{
    private const int MaxRows = 5000;

    private readonly ICommandRepository<Lead> _leads;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly IUnitOfWork _unitOfWork;

    public ImportLeadsHandler(ICommandRepository<Lead> leads, NumberSequenceService numberSequenceService, IUnitOfWork unitOfWork)
    {
        _leads = leads;
        _numberSequenceService = numberSequenceService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult> Handle(ImportLeadsRequest request, CancellationToken cancellationToken)
    {
        var table = new CsvTable(request.Csv);
        var cTitle = table.Col("title", "lead", "leadtitle", "name", "subject");
        var cCompany = table.Col("company", "companyname", "organization", "organisation");
        if (cTitle < 0 && cCompany < 0)
        {
            throw new Exception("The header must contain a 'Title' or 'Company' column.");
        }
        var cEmail = table.Col("email", "emailaddress", "companyemail", "mail");
        var cPhone = table.Col("phone", "phonenumber", "mobile", "telephone");
        var cWebsite = table.Col("website", "web", "url");
        var cStreet = table.Col("street", "address", "addressstreet");
        var cCity = table.Col("city");
        var cState = table.Col("state", "province", "region");
        var cZip = table.Col("zip", "zipcode", "postcode", "postalcode");
        var cCountry = table.Col("country");
        var cSource = table.Col("source", "leadsource");
        var cTags = table.Col("tags", "tag", "labels");
        var cConsent = table.Col("consent", "marketingconsent", "optin");
        var cAmount = table.Col("amount", "value", "dealvalue", "amounttargeted");
        var cNotes = table.Col("description", "notes", "note");

        var existing = await _leads.GetQuery().Where(x => !x.IsDeleted)
            .Select(x => new { x.CompanyName, x.CompanyEmail }).ToListAsync(cancellationToken);
        var knownNames = new HashSet<string>(existing.Where(x => !string.IsNullOrWhiteSpace(x.CompanyName)).Select(x => x.CompanyName!.Trim().ToLowerInvariant()));
        var knownEmails = new HashSet<string>(existing.Where(x => !string.IsNullOrWhiteSpace(x.CompanyEmail)).Select(x => x.CompanyEmail!.Trim().ToLowerInvariant()));

        var issues = new List<ImportRowIssue>();
        var imported = 0;
        var total = 0;

        for (int r = 1; r < table.Rows.Count && r <= MaxRows; r++)
        {
            var row = table.Rows[r];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            total++;

            var company = CsvTable.Cell(row, cCompany);
            var title = CsvTable.Cell(row, cTitle);
            if (title.Length == 0) title = company;
            var email = CsvTable.Cell(row, cEmail);
            var label = company.Length > 0 ? company : title;

            if (title.Length < 2) { issues.Add(new ImportRowIssue(r + 1, label, "Title or company is missing.")); continue; }
            if (email.Length > 0 && !email.Contains('@')) { issues.Add(new ImportRowIssue(r + 1, label, $"'{email}' is not a valid email address.")); continue; }
            if (company.Length > 0 && knownNames.Contains(company.ToLowerInvariant())) { issues.Add(new ImportRowIssue(r + 1, label, "Duplicate: a lead for this company already exists.")); continue; }
            if (email.Length > 0 && knownEmails.Contains(email.ToLowerInvariant())) { issues.Add(new ImportRowIssue(r + 1, label, $"Duplicate: email {email} is already used.")); continue; }

            if (company.Length > 0) knownNames.Add(company.ToLowerInvariant());
            if (email.Length > 0) knownEmails.Add(email.ToLowerInvariant());

            if (request.Preview) { imported++; continue; }

            double? amount = null;
            if (double.TryParse(CsvTable.Cell(row, cAmount).Replace(",", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var a)) amount = a;
            var consent = CsvTable.ParseConsent(CsvTable.Cell(row, cConsent));

            var lead = new Lead
            {
                CreatedById = request.CreatedById,
                Number = _numberSequenceService.GenerateNumber(nameof(Lead), "", "LEA"),
                Title = title,
                CompanyName = CsvTable.NullIfEmpty(company),
                CompanyEmail = CsvTable.NullIfEmpty(email),
                CompanyPhoneNumber = CsvTable.NullIfEmpty(CsvTable.Cell(row, cPhone)),
                CompanyWebsite = CsvTable.NullIfEmpty(CsvTable.Cell(row, cWebsite)),
                CompanyAddressStreet = CsvTable.NullIfEmpty(CsvTable.Cell(row, cStreet)),
                CompanyAddressCity = CsvTable.NullIfEmpty(CsvTable.Cell(row, cCity)),
                CompanyAddressState = CsvTable.NullIfEmpty(CsvTable.Cell(row, cState)),
                CompanyAddressZipCode = CsvTable.NullIfEmpty(CsvTable.Cell(row, cZip)),
                CompanyAddressCountry = CsvTable.NullIfEmpty(CsvTable.Cell(row, cCountry)),
                Description = CsvTable.NullIfEmpty(CsvTable.Cell(row, cNotes, 4000)),
                LeadSource = CsvTable.NullIfEmpty(CsvTable.Cell(row, cSource)) ?? "Import",
                Tags = CsvTable.NullIfEmpty(CsvTable.Cell(row, cTags)),
                ConsentMarketing = consent,
                ConsentUpdatedAtUtc = consent == null ? null : DateTime.UtcNow,
                AmountTargeted = amount,
                DateProspecting = DateTime.Now,
                DateClosingEstimation = DateTime.Now.AddDays(30),
                PipelineStage = PipelineStage.Prospecting,
                ClosingStatus = ClosingStatus.OnProgress
            };

            await _leads.CreateAsync(lead, cancellationToken);
            imported++;
        }

        if (!request.Preview && imported > 0)
        {
            await _unitOfWork.SaveAsync(cancellationToken);
        }

        return new ImportResult { Preview = request.Preview, TotalRows = total, Imported = imported, Skipped = issues.Count, Issues = issues.Take(200).ToList() };
    }
}
