using Application.Common.Services.IntegrationManager;
using FluentValidation;
using MediatR;

namespace Application.Features.ConnectionManager;

public class ConnectionFieldDto
{
    public string? Key { get; init; }
    public string? Label { get; init; }
    public bool Secret { get; init; }
    public string? Kind { get; init; }
    public string? Hint { get; init; }
    public bool HasValue { get; init; }

    /// <summary>saved, server or none.</summary>
    public string? Source { get; init; }

    /// <summary>Only for fields that are not secret.</summary>
    public string? Value { get; init; }
}

public class ConnectionGroupDto
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Help { get; init; }
    public List<ConnectionFieldDto> Fields { get; init; } = new();
}

public class GetConnectionsResult { public List<ConnectionGroupDto> Data { get; init; } = new(); }
public class GetConnectionsRequest : IRequest<GetConnectionsResult> { }

/// <summary>Secrets are never sent back: the page only learns whether one is set and where it comes from.</summary>
public class GetConnectionsHandler : IRequestHandler<GetConnectionsRequest, GetConnectionsResult>
{
    private readonly IConnectionSettings _settings;
    public GetConnectionsHandler(IConnectionSettings settings) { _settings = settings; }

    public Task<GetConnectionsResult> Handle(GetConnectionsRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new GetConnectionsResult
        {
            Data = ConnectionRegistry.Groups.Select(g => new ConnectionGroupDto
            {
                Id = g.Id, Title = g.Title, Help = g.Help,
                Fields = g.Fields.Select(f =>
                {
                    var v = _settings[f.Key];
                    return new ConnectionFieldDto
                    {
                        Key = f.Key, Label = f.Label, Secret = f.Secret, Kind = f.Kind, Hint = f.Hint,
                        HasValue = !string.IsNullOrEmpty(v), Source = _settings.SourceOf(f.Key), Value = f.Secret ? null : v
                    };
                }).ToList()
            }).ToList()
        });
}

public class SaveConnectionsResult { public int Changed { get; init; } }

public class SaveConnectionsRequest : IRequest<SaveConnectionsResult>
{
    /// <summary>Key to new value. Leave a key out (or null) to keep it, send an empty text to remove the saved value.</summary>
    public Dictionary<string, string?>? Values { get; init; }
    public string? UserId { get; set; }
}

public class SaveConnectionsValidator : AbstractValidator<SaveConnectionsRequest>
{
    public SaveConnectionsValidator()
    {
        RuleFor(x => x.Values).NotNull().Must(v => v!.Count > 0 && v.Count <= 40).WithMessage("Nothing to save.");
        RuleFor(x => x.Values).Must(v => v == null || v.Keys.All(k => ConnectionRegistry.Find(k) != null)).WithMessage("Unknown setting.");
        RuleFor(x => x.Values).Custom((values, ctx) =>
        {
            if (values == null) return;
            foreach (var (k, v) in values)
            {
                var f = ConnectionRegistry.Find(k);
                var error = f == null ? null : ConnectionRegistry.Validate(f, v?.Trim());
                if (error != null) ctx.AddFailure(error);
            }
        });
    }
}

public class SaveConnectionsHandler : IRequestHandler<SaveConnectionsRequest, SaveConnectionsResult>
{
    private readonly IConnectionSettings _settings;
    public SaveConnectionsHandler(IConnectionSettings settings) { _settings = settings; }

    public async Task<SaveConnectionsResult> Handle(SaveConnectionsRequest request, CancellationToken cancellationToken)
    {
        var changes = request.Values!.Where(kv => kv.Value != null)
            .ToDictionary(kv => ConnectionRegistry.Find(kv.Key)!.Key, kv => (string?)kv.Value!.Trim(), StringComparer.OrdinalIgnoreCase);
        if (changes.Count == 0) return new SaveConnectionsResult();

        // what the settings would be after this save
        string? Effective(string key) => changes.TryGetValue(key, out var v) ? (string.IsNullOrEmpty(v) ? null : v) : _settings[key];
        var problem = ConnectionRegistry.LockoutProblem(Effective);
        if (problem != null) throw new Exception(problem);

        await _settings.SaveAsync(changes, request.UserId, cancellationToken);
        return new SaveConnectionsResult { Changed = changes.Count };
    }
}
