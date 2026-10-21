using Application.Features.ImportManager.Commands;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ImportController : BaseApiController
{
    public ImportController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("ImportCustomers")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ApiSuccessResult<ImportCustomersResult>>> ImportCustomersAsync(ImportCustomersRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<ImportCustomersResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ImportCustomersAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ImportLeads")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ApiSuccessResult<ImportResult>>> ImportLeadsAsync(ImportLeadsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<ImportResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ImportLeadsAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ImportContacts")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ApiSuccessResult<ImportResult>>> ImportContactsAsync(ImportContactsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<ImportResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ImportContactsAsync)}",
            Content = response
        });
    }
}
