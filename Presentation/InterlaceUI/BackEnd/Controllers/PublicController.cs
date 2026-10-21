using Application.Features.IntegrationManager;
using Application.Features.KnowledgeManager;
using Application.Features.SupportInsightManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Endpoints for outside systems. Authenticated by the X-Api-Key header, never by a user login.</summary>
[Route("api/[controller]")]
public class PublicController : BaseApiController
{
    public PublicController(ISender sender) : base(sender)
    {
    }

    /// <summary>Public help centre: published articles only.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpGet("SearchHelp")]
    public async Task<ActionResult<ApiSuccessResult<SearchArticlesResult>>> SearchHelpAsync([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SearchArticlesRequest { Query = q, PublishedOnly = true }, cancellationToken);
        return Ok(new ApiSuccessResult<SearchArticlesResult> { Code = StatusCodes.Status200OK, Message = "OK", Content = result });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpGet("GetHelpArticle")]
    public async Task<ActionResult<ApiSuccessResult<ArticleDto>>> GetHelpArticleAsync([FromQuery] string? id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetArticleRequest { Id = id, PublishedOnly = true, CountView = true }, cancellationToken);
            return Ok(new ApiSuccessResult<ArticleDto> { Code = StatusCodes.Status200OK, Message = "OK", Content = result });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("RateHelpArticle")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> RateHelpArticleAsync(RateArticleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(request, cancellationToken);
            return Ok(new ApiSuccessResult<bool> { Code = StatusCodes.Status200OK, Message = "Thank you", Content = result });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Survey page: which case does this link belong to? The token is the secret.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpGet("GetCsatSurvey")]
    public async Task<ActionResult<ApiSuccessResult<GetCsatSurveyResult>>> GetCsatSurveyAsync([FromQuery] string? token, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetCsatSurveyRequest { Token = token }, cancellationToken);
            return Ok(new ApiSuccessResult<GetCsatSurveyResult> { Code = StatusCodes.Status200OK, Message = "OK", Content = result });
        }
        catch (UnauthorizedAccessException) { return NotFound(); }
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("SubmitCsat")]
    public async Task<ActionResult<ApiSuccessResult<SubmitCsatResult>>> SubmitCsatAsync(SubmitCsatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(request, cancellationToken);
            return Ok(new ApiSuccessResult<SubmitCsatResult> { Code = StatusCodes.Status200OK, Message = "Thank you", Content = result });
        }
        catch (UnauthorizedAccessException) { return NotFound(); }
    }

    /// <summary>Web-to-lead: creates a lead from a website form or another system.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("CaptureLead")]
    public async Task<ActionResult<ApiSuccessResult<CaptureLeadResult>>> CaptureLeadAsync(CaptureLeadRequest request, CancellationToken cancellationToken)
    {
        var key = Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key)) return Unauthorized();

        request.ApiKey = key;
        try
        {
            var result = await _sender.Send(request, cancellationToken);
            return Ok(new ApiSuccessResult<CaptureLeadResult> { Code = StatusCodes.Status200OK, Message = "Lead received", Content = result });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
