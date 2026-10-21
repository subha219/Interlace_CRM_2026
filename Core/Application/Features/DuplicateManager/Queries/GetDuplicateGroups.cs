using System.Text.RegularExpressions;
using Application.Common.CQS.Queries;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DuplicateManager.Queries;

public class DuplicateItemDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class DuplicateGroupDto
{
    public List<string> Reasons { get; init; } = new();
    public List<DuplicateItemDto> Items { get; init; } = new();
}

public class GetDuplicateGroupsResult
{
    public List<DuplicateGroupDto>? Data { get; init; }
}

public class GetDuplicateGroupsRequest : IRequest<GetDuplicateGroupsResult>
{
    /// <summary>Customer or Lead.</summary>
    public string? Entity { get; init; }
}

public class GetDuplicateGroupsValidator : AbstractValidator<GetDuplicateGroupsRequest>
{
    public GetDuplicateGroupsValidator()
    {
        RuleFor(x => x.Entity).Must(x => x == "Customer" || x == "Lead").WithMessage("Entity must be Customer or Lead.");
    }
}

public class GetDuplicateGroupsHandler : IRequestHandler<GetDuplicateGroupsRequest, GetDuplicateGroupsResult>
{
    private readonly IQueryContext _context;

    public GetDuplicateGroupsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetDuplicateGroupsResult> Handle(GetDuplicateGroupsRequest request, CancellationToken cancellationToken)
    {
        List<DuplicateItemDto> items;

        if (request.Entity == "Customer")
        {
            items = await _context.Customer.AsNoTracking().Where(x => !x.IsDeleted)
                .Select(x => new DuplicateItemDto { Id = x.Id, Number = x.Number, Name = x.Name, Email = x.EmailAddress, Phone = x.PhoneNumber, CreatedAtUtc = x.CreatedAtUtc })
                .ToListAsync(cancellationToken);
        }
        else
        {
            //>>> converted leads are already linked to a customer, they are not duplicates of one
            items = await _context.Lead.AsNoTracking().Where(x => !x.IsDeleted)
                .Select(x => new DuplicateItemDto { Id = x.Id, Number = x.Number, Name = x.CompanyName ?? x.Title, Email = x.CompanyEmail, Phone = x.CompanyPhoneNumber, CreatedAtUtc = x.CreatedAtUtc })
                .ToListAsync(cancellationToken);
        }

        return new GetDuplicateGroupsResult { Data = DuplicateFinder.Group(items) };
    }
}

public static class DuplicateFinder
{
    private static readonly Regex NonAlphaNum = new("[^a-z0-9]", RegexOptions.Compiled);
    private static readonly Regex NonDigit = new("[^0-9]", RegexOptions.Compiled);

    public static string? EmailKey(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : "email:" + v.Trim().ToLowerInvariant();

    public static string? PhoneKey(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var digits = NonDigit.Replace(v, "");
        if (digits.Length < 7) return null;
        return "phone:" + (digits.Length > 10 ? digits[^10..] : digits);
    }

    public static string? NameKey(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var k = NonAlphaNum.Replace(v.ToLowerInvariant(), "");
        return k.Length < 3 ? null : "name:" + k;
    }

    /// <summary>Clusters records that share an e-mail, phone number or name (transitively).</summary>
    public static List<DuplicateGroupDto> Group(List<DuplicateItemDto> items)
    {
        var parent = items.ToDictionary(x => x.Id!, x => x.Id!);
        string Find(string id)
        {
            while (parent[id] != id) { parent[id] = parent[parent[id]]; id = parent[id]; }
            return id;
        }

        var byKey = new Dictionary<string, List<DuplicateItemDto>>();
        foreach (var item in items)
        {
            foreach (var key in new[] { EmailKey(item.Email), PhoneKey(item.Phone), NameKey(item.Name) })
            {
                if (key == null) continue;
                if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = new List<DuplicateItemDto>();
                list.Add(item);
            }
        }

        var duplicateKeys = byKey.Where(kv => kv.Value.Count > 1).ToList();
        foreach (var kv in duplicateKeys)
        {
            var first = Find(kv.Value[0].Id!);
            foreach (var other in kv.Value.Skip(1))
            {
                parent[Find(other.Id!)] = first;
            }
        }

        var reasons = new Dictionary<string, HashSet<string>>();
        foreach (var kv in duplicateKeys)
        {
            var root = Find(kv.Value[0].Id!);
            if (!reasons.TryGetValue(root, out var set)) reasons[root] = set = new HashSet<string>();
            set.Add(kv.Key.Split(':')[0] switch { "email" => "Same e-mail", "phone" => "Same phone", _ => "Same name" });
        }

        return items
            .GroupBy(x => Find(x.Id!))
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroupDto
            {
                Reasons = reasons.TryGetValue(g.Key, out var r) ? r.OrderBy(x => x).ToList() : new List<string>(),
                Items = g.OrderBy(x => x.CreatedAtUtc ?? DateTime.MaxValue).ToList()
            })
            .OrderByDescending(g => g.Items.Count)
            .ToList();
    }
}
