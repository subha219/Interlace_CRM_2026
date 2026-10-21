using Application.Features.KnowledgeManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Knowledge base articles and support categories (see ModulePermissions for the roles).</summary>
[Route("api/[controller]")]
public class KnowledgeController : BaseApiController
{
    public KnowledgeController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetCategories")]
    public async Task<ActionResult<ApiSuccessResult<GetCategoriesResult>>> GetCategoriesAsync([FromQuery] GetCategoriesRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetCategoriesAsync));

    [Authorize][HttpPost("SaveCategory")]
    public async Task<ActionResult<ApiSuccessResult<SaveCategoryResult>>> SaveCategoryAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveCategoryAsync));
    }

    [Authorize][HttpPost("DeleteCategory")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> DeleteCategoryAsync(DeleteCategoryRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteCategoryAsync));
    }

    [Authorize][HttpGet("GetCaseCategory")]
    public async Task<ActionResult<ApiSuccessResult<string?>>> GetCaseCategoryAsync([FromQuery] GetCaseCategoryRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetCaseCategoryAsync));

    [Authorize][HttpPost("AssignCategory")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> AssignCategoryAsync(AssignCategoryRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(AssignCategoryAsync));
    }

    [Authorize][HttpGet("Search")]
    public async Task<ActionResult<ApiSuccessResult<SearchArticlesResult>>> SearchAsync([FromQuery] SearchArticlesRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(SearchAsync));

    [Authorize][HttpGet("GetArticle")]
    public async Task<ActionResult<ApiSuccessResult<ArticleDto>>> GetArticleAsync([FromQuery] string id, CancellationToken ct) =>
        Success(await _sender.Send(new GetArticleRequest { Id = id }, ct), nameof(GetArticleAsync));

    [Authorize][HttpPost("SaveArticle")]
    public async Task<ActionResult<ApiSuccessResult<SaveArticleResult>>> SaveArticleAsync(SaveArticleRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveArticleAsync));
    }

    [Authorize][HttpPost("DeleteArticle")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> DeleteArticleAsync(DeleteArticleRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteArticleAsync));
    }
}
