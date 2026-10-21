using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.KnowledgeManager;

// ---------- categories ----------

public record CategoryDto(string? Id, string? Name, string? Description, string? DefaultOwnerUserId, string? DefaultPriority, bool IsActive);
public class GetCategoriesResult { public List<CategoryDto> Data { get; init; } = new(); }
public class GetCategoriesRequest : IRequest<GetCategoriesResult> { public bool ActiveOnly { get; init; } }

public class GetCategoriesHandler : IRequestHandler<GetCategoriesRequest, GetCategoriesResult>
{
    private readonly IQueryContext _context;
    public GetCategoriesHandler(IQueryContext context) { _context = context; }

    public async Task<GetCategoriesResult> Handle(GetCategoriesRequest request, CancellationToken cancellationToken)
    {
        var q = _context.SupportCategory.AsNoTracking().Where(x => !x.IsDeleted);
        if (request.ActiveOnly) q = q.Where(x => x.IsActive);
        var rows = await q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return new GetCategoriesResult { Data = rows.Select(x => new CategoryDto(x.Id, x.Name, x.Description, x.DefaultOwnerUserId, x.DefaultPriority?.ToString(), x.IsActive)).ToList() };
    }
}

public class SaveCategoryResult { public string? Id { get; init; } }

public class SaveCategoryRequest : IRequest<SaveCategoryResult>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? DefaultOwnerUserId { get; init; }
    public string? DefaultPriority { get; init; }
    public bool IsActive { get; init; } = true;
    public string? UserId { get; set; }
}

public class SaveCategoryValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DefaultPriority).Must(v => string.IsNullOrEmpty(v) || Enum.TryParse<CasePriority>(v, out _)).WithMessage("Unknown priority.");
    }
}

public class SaveCategoryHandler : IRequestHandler<SaveCategoryRequest, SaveCategoryResult>
{
    private readonly ICommandRepository<SupportCategory> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveCategoryHandler(ICommandRepository<SupportCategory> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveCategoryResult> Handle(SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name!.Trim();
        var duplicate = await _repository.GetQuery().AnyAsync(x => !x.IsDeleted && x.Name == name && x.Id != request.Id, cancellationToken);
        if (duplicate) throw new Exception("A category with this name already exists.");

        var entity = string.IsNullOrEmpty(request.Id) ? new SupportCategory { CreatedById = request.UserId } : await _repository.GetAsync(request.Id, cancellationToken) ?? throw new Exception("Category not found.");
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.DefaultOwnerUserId = string.IsNullOrWhiteSpace(request.DefaultOwnerUserId) ? null : request.DefaultOwnerUserId;
        entity.DefaultPriority = string.IsNullOrEmpty(request.DefaultPriority) ? null : Enum.Parse<CasePriority>(request.DefaultPriority);
        entity.IsActive = request.IsActive;
        entity.UpdatedById = request.UserId;
        if (string.IsNullOrEmpty(request.Id)) await _repository.CreateAsync(entity, cancellationToken); else _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveCategoryResult { Id = entity.Id };
    }
}

public class DeleteCategoryRequest : IRequest<bool> { public string? Id { get; init; } public string? UserId { get; set; } }

public class DeleteCategoryHandler : IRequestHandler<DeleteCategoryRequest, bool>
{
    private readonly ICommandRepository<SupportCategory> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteCategoryHandler(ICommandRepository<SupportCategory> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(DeleteCategoryRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Category not found.");
        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

public class GetCaseCategoryRequest : IRequest<string?> { public string? CaseId { get; init; } }

public class GetCaseCategoryHandler : IRequestHandler<GetCaseCategoryRequest, string?>
{
    private readonly IQueryContext _context;
    public GetCaseCategoryHandler(IQueryContext context) { _context = context; }
    public async Task<string?> Handle(GetCaseCategoryRequest request, CancellationToken cancellationToken) =>
        await _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.Id == request.CaseId).Select(x => x.CategoryId).FirstOrDefaultAsync(cancellationToken);
}

public class AssignCategoryRequest : IRequest<bool>
{
    public string? CaseId { get; init; }
    public string? CategoryId { get; init; }

    /// <summary>Also move the case to the category's default owner and priority.</summary>
    public bool ApplyRouting { get; init; }
    public string? UserId { get; set; }
}

public class AssignCategoryHandler : IRequestHandler<AssignCategoryRequest, bool>
{
    private readonly ICommandRepository<SupportCase> _cases;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    public AssignCategoryHandler(ICommandRepository<SupportCase> cases, IQueryContext context, IUnitOfWork unitOfWork) { _cases = cases; _context = context; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(AssignCategoryRequest request, CancellationToken cancellationToken)
    {
        var c = await _cases.GetAsync(request.CaseId ?? "", cancellationToken) ?? throw new Exception("Case not found.");
        if (string.IsNullOrEmpty(request.CategoryId))
        {
            c.CategoryId = null;
        }
        else
        {
            var cat = await _context.SupportCategory.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == request.CategoryId, cancellationToken) ?? throw new Exception("Category not found.");
            c.CategoryId = cat.Id;
            if (request.ApplyRouting)
            {
                if (!string.IsNullOrEmpty(cat.DefaultOwnerUserId)) c.OwnerUserId = cat.DefaultOwnerUserId;
                if (cat.DefaultPriority != null && cat.DefaultPriority != c.Priority)
                {
                    c.Priority = cat.DefaultPriority;
                    var sla = await SupportCaseManager.SlaConfig.LoadAsync(_context, cancellationToken);
                    var from = c.CreatedAtUtc?.ToLocalTime() ?? DateTime.Now;
                    c.DueDate = sla.ResolutionDue(c.Priority, from);
                    if (c.FirstRespondedAt == null) c.FirstResponseDue = sla.FirstResponseDueAt(c.Priority, from);
                }
            }
        }
        c.UpdatedById = request.UserId;
        _cases.Update(c);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

// ---------- knowledge base ----------

public class ArticleDto
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Body { get; init; }
    public string? CategoryId { get; init; }
    public string? Tags { get; init; }
    public bool IsPublished { get; init; }
    public int ViewCount { get; init; }
    public int HelpfulYes { get; init; }
    public int HelpfulNo { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public static class KnowledgeRules
{
    public static ArticleDto ToDto(KbArticle x, bool withBody) => new()
    {
        Id = x.Id, Title = x.Title, Summary = x.Summary, Body = withBody ? x.Body : null, CategoryId = x.CategoryId, Tags = x.Tags,
        IsPublished = x.IsPublished, ViewCount = x.ViewCount, HelpfulYes = x.HelpfulYes, HelpfulNo = x.HelpfulNo, UpdatedAtUtc = x.UpdatedAtUtc ?? x.CreatedAtUtc
    };

    /// <summary>Splits a search text into lower-case words (max 6, at least 2 letters).</summary>
    public static List<string> Words(string? text) =>
        (text ?? "").ToLowerInvariant().Split(new[] { ' ', ',', ';', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2).Distinct().Take(6).ToList();
}

public class SearchArticlesResult { public List<ArticleDto> Data { get; init; } = new(); }

public class SearchArticlesRequest : IRequest<SearchArticlesResult>
{
    public string? Query { get; init; }
    public string? CategoryId { get; init; }

    /// <summary>True for the public help centre: only published articles, no body.</summary>
    public bool PublishedOnly { get; init; }
}

public class SearchArticlesHandler : IRequestHandler<SearchArticlesRequest, SearchArticlesResult>
{
    private readonly IQueryContext _context;
    public SearchArticlesHandler(IQueryContext context) { _context = context; }

    public async Task<SearchArticlesResult> Handle(SearchArticlesRequest request, CancellationToken cancellationToken)
    {
        var q = _context.KbArticle.AsNoTracking().Where(x => !x.IsDeleted);
        if (request.PublishedOnly) q = q.Where(x => x.IsPublished);
        if (!string.IsNullOrEmpty(request.CategoryId)) q = q.Where(x => x.CategoryId == request.CategoryId);
        foreach (var w in KnowledgeRules.Words(request.Query))
        {
            var word = w;
            q = q.Where(x => (x.Title ?? "").ToLower().Contains(word) || (x.Summary ?? "").ToLower().Contains(word) || (x.Tags ?? "").ToLower().Contains(word) || (x.Body ?? "").ToLower().Contains(word));
        }
        var rows = await q.OrderByDescending(x => x.HelpfulYes).ThenByDescending(x => x.ViewCount).Take(100).ToListAsync(cancellationToken);
        return new SearchArticlesResult { Data = rows.Select(x => KnowledgeRules.ToDto(x, false)).ToList() };
    }
}

public class GetArticleRequest : IRequest<ArticleDto>
{
    public string? Id { get; init; }
    public bool PublishedOnly { get; init; }
    public bool CountView { get; init; }
}

public class GetArticleHandler : IRequestHandler<GetArticleRequest, ArticleDto>
{
    private readonly ICommandRepository<KbArticle> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public GetArticleHandler(ICommandRepository<KbArticle> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<ArticleDto> Handle(GetArticleRequest request, CancellationToken cancellationToken)
    {
        var a = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == request.Id && (!request.PublishedOnly || x.IsPublished), cancellationToken)
            ?? throw new KeyNotFoundException("Article not found.");
        if (request.CountView)
        {
            a.ViewCount++;
            _repository.Update(a);
            await _unitOfWork.SaveAsync(cancellationToken);
        }
        return KnowledgeRules.ToDto(a, true);
    }
}

public class SaveArticleResult { public string? Id { get; init; } }

public class SaveArticleRequest : IRequest<SaveArticleResult>
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Body { get; init; }
    public string? CategoryId { get; init; }
    public string? Tags { get; init; }
    public bool IsPublished { get; init; }
    public string? UserId { get; set; }
}

public class SaveArticleValidator : AbstractValidator<SaveArticleRequest>
{
    public SaveArticleValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(20000);
        RuleFor(x => x.Tags).MaximumLength(300);
    }
}

public class SaveArticleHandler : IRequestHandler<SaveArticleRequest, SaveArticleResult>
{
    private readonly ICommandRepository<KbArticle> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveArticleHandler(ICommandRepository<KbArticle> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveArticleResult> Handle(SaveArticleRequest request, CancellationToken cancellationToken)
    {
        var a = string.IsNullOrEmpty(request.Id) ? new KbArticle { CreatedById = request.UserId } : await _repository.GetAsync(request.Id, cancellationToken) ?? throw new Exception("Article not found.");
        a.Title = request.Title!.Trim();
        a.Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim();
        a.Body = request.Body;
        a.CategoryId = string.IsNullOrWhiteSpace(request.CategoryId) ? null : request.CategoryId;
        a.Tags = string.IsNullOrWhiteSpace(request.Tags) ? null : request.Tags.Trim();
        a.IsPublished = request.IsPublished;
        a.UpdatedById = request.UserId;
        if (string.IsNullOrEmpty(request.Id)) await _repository.CreateAsync(a, cancellationToken); else _repository.Update(a);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveArticleResult { Id = a.Id };
    }
}

public class DeleteArticleRequest : IRequest<bool> { public string? Id { get; init; } public string? UserId { get; set; } }

public class DeleteArticleHandler : IRequestHandler<DeleteArticleRequest, bool>
{
    private readonly ICommandRepository<KbArticle> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteArticleHandler(ICommandRepository<KbArticle> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(DeleteArticleRequest request, CancellationToken cancellationToken)
    {
        var a = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Article not found.");
        a.UpdatedById = request.UserId;
        _repository.Delete(a);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

public class RateArticleRequest : IRequest<bool> { public string? Id { get; init; } public bool Helpful { get; init; } }

public class RateArticleHandler : IRequestHandler<RateArticleRequest, bool>
{
    private readonly ICommandRepository<KbArticle> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public RateArticleHandler(ICommandRepository<KbArticle> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(RateArticleRequest request, CancellationToken cancellationToken)
    {
        var a = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.IsPublished && x.Id == request.Id, cancellationToken) ?? throw new KeyNotFoundException("Article not found.");
        if (request.Helpful) a.HelpfulYes++; else a.HelpfulNo++;
        _repository.Update(a);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}
