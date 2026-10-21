using System.Text;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ImportManager.Commands;

public record ImportRowIssue(int Row, string Name, string Reason);

public class ImportCustomersResult
{
    public bool Preview { get; init; }
    public int TotalRows { get; init; }
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public List<ImportRowIssue> Issues { get; init; } = new();
}

public class ImportCustomersRequest : IRequest<ImportCustomersResult>
{
    /// <summary>The whole CSV file as text. First line is the header.</summary>
    public string? Csv { get; init; }

    /// <summary>When true nothing is saved, the result only shows what would happen.</summary>
    public bool Preview { get; init; }
    public string? CreatedById { get; init; }
}

public class ImportCustomersHandler : IRequestHandler<ImportCustomersRequest, ImportCustomersResult>
{
    private const int Max = 255;
    private const int MaxRows = 5000;

    private readonly ICommandRepository<Customer> _customers;
    private readonly ICommandRepository<CustomerGroup> _groups;
    private readonly ICommandRepository<CustomerCategory> _categories;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly IUnitOfWork _unitOfWork;

    public ImportCustomersHandler(
        ICommandRepository<Customer> customers,
        ICommandRepository<CustomerGroup> groups,
        ICommandRepository<CustomerCategory> categories,
        NumberSequenceService numberSequenceService,
        IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _groups = groups;
        _categories = categories;
        _numberSequenceService = numberSequenceService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportCustomersResult> Handle(ImportCustomersRequest request, CancellationToken cancellationToken)
    {
        var rows = CsvReader.Parse(request.Csv ?? string.Empty);
        if (rows.Count < 2)
        {
            throw new Exception("The file needs a header row and at least one data row.");
        }

        var header = rows[0].Select(h => Normalize(h)).ToList();
        int Col(params string[] names)
        {
            foreach (var n in names)
            {
                var i = header.IndexOf(n);
                if (i >= 0) return i;
            }
            return -1;
        }

        var cName = Col("name", "customer", "customername", "company", "companyname");
        if (cName < 0)
        {
            throw new Exception("The header must contain a 'Name' column.");
        }
        var cEmail = Col("email", "emailaddress", "mail");
        var cPhone = Col("phone", "phonenumber", "mobile", "telephone");
        var cStreet = Col("street", "address", "addressstreet");
        var cCity = Col("city");
        var cState = Col("state", "province", "region");
        var cZip = Col("zip", "zipcode", "postcode", "postalcode");
        var cCountry = Col("country");
        var cWebsite = Col("website", "web", "url");
        var cWhatsApp = Col("whatsapp");
        var cDescription = Col("description", "notes", "note");
        var cGroup = Col("group", "customergroup");
        var cCategory = Col("category", "customercategory");

        var groups = await _groups.GetQuery().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        var categories = await _categories.GetQuery().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        var existing = await _customers.GetQuery().Where(x => !x.IsDeleted)
            .Select(x => new { x.Name, x.EmailAddress }).ToListAsync(cancellationToken);

        var knownNames = new HashSet<string>(existing.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => x.Name!.Trim().ToLowerInvariant()));
        var knownEmails = new HashSet<string>(existing.Where(x => !string.IsNullOrWhiteSpace(x.EmailAddress)).Select(x => x.EmailAddress!.Trim().ToLowerInvariant()));

        string Cell(List<string> row, int col) => col >= 0 && col < row.Count ? Truncate(row[col].Trim(), Max) : string.Empty;

        var issues = new List<ImportRowIssue>();
        var imported = 0;
        var total = 0;

        for (int r = 1; r < rows.Count && r <= MaxRows; r++)
        {
            var row = rows[r];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }
            total++;

            var name = Cell(row, cName);
            var email = Cell(row, cEmail);

            if (name.Length < 2)
            {
                issues.Add(new ImportRowIssue(r + 1, name, "Name is missing."));
                continue;
            }
            if (knownNames.Contains(name.ToLowerInvariant()))
            {
                issues.Add(new ImportRowIssue(r + 1, name, "Duplicate: a customer with this name already exists."));
                continue;
            }
            if (email.Length > 0 && knownEmails.Contains(email.ToLowerInvariant()))
            {
                issues.Add(new ImportRowIssue(r + 1, name, $"Duplicate: email {email} is already used."));
                continue;
            }
            if (email.Length > 0 && !email.Contains('@'))
            {
                issues.Add(new ImportRowIssue(r + 1, name, $"'{email}' is not a valid email address."));
                continue;
            }

            knownNames.Add(name.ToLowerInvariant());
            if (email.Length > 0) knownEmails.Add(email.ToLowerInvariant());

            if (request.Preview)
            {
                imported++;
                continue;
            }

            var groupName = Cell(row, cGroup);
            var categoryName = Cell(row, cCategory);
            var group = groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase)) ?? groups.FirstOrDefault();
            var category = categories.FirstOrDefault(c => string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase)) ?? categories.FirstOrDefault();

            var entity = new Customer
            {
                CreatedById = request.CreatedById,
                Number = _numberSequenceService.GenerateNumber(nameof(Customer), "", "CST"),
                Name = name,
                EmailAddress = email.Length > 0 ? email : null,
                PhoneNumber = NullIfEmpty(Cell(row, cPhone)),
                Street = NullIfEmpty(Cell(row, cStreet)),
                City = NullIfEmpty(Cell(row, cCity)),
                State = NullIfEmpty(Cell(row, cState)),
                ZipCode = NullIfEmpty(Cell(row, cZip)),
                Country = NullIfEmpty(Cell(row, cCountry)),
                Website = NullIfEmpty(Cell(row, cWebsite)),
                WhatsApp = NullIfEmpty(Cell(row, cWhatsApp)),
                Description = NullIfEmpty(Truncate(cDescription >= 0 && cDescription < row.Count ? row[cDescription].Trim() : string.Empty, 4000)),
                CustomerGroupId = group?.Id,
                CustomerCategoryId = category?.Id
            };

            await _customers.CreateAsync(entity, cancellationToken);
            imported++;
        }

        if (!request.Preview && imported > 0)
        {
            await _unitOfWork.SaveAsync(cancellationToken);
        }

        return new ImportCustomersResult
        {
            Preview = request.Preview,
            TotalRows = total,
            Imported = imported,
            Skipped = issues.Count,
            Issues = issues.Take(200).ToList()
        };
    }

    private static string Normalize(string h) =>
        new string((h ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string Truncate(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

/// <summary>Small RFC 4180 reader: quoted fields, escaped quotes, commas or semicolons, CRLF or LF.</summary>
public static class CsvReader
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return rows;
        }

        text = text.TrimStart('\uFEFF');
        var firstLine = text.Split('\n')[0];
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';

        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); }
            else field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
