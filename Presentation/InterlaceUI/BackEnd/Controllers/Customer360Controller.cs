using Application.Features.CustomerManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class Customer360Controller : BaseApiController
{
    public Customer360Controller(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetCustomer360")]
    public async Task<ActionResult<ApiSuccessResult<GetCustomer360Result>>> GetCustomer360Async([FromQuery] GetCustomer360Request request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetCustomer360Result>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetCustomer360Async)}",
            Content = response
        });
    }
}
