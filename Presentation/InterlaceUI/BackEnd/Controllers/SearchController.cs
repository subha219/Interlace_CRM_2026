using Application.Features.SearchManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class SearchController : BaseApiController
{
    public SearchController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GlobalSearch")]
    public async Task<ActionResult<ApiSuccessResult<GlobalSearchResult>>> GlobalSearchAsync([FromQuery] GlobalSearchRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GlobalSearchResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GlobalSearchAsync)}",
            Content = response
        });
    }
}
