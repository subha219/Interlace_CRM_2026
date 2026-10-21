using Application.Features.ContractManager;
using Application.Features.CustomerHealthManager;
using Application.Features.SecurityManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Contracts and renewals (role Contracts) and the customer health board (see ModulePermissions).</summary>
[Route("api/[controller]")]
public class ContractController : BaseApiController
{
    public ContractController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetContracts")]
    public async Task<ActionResult<ApiSuccessResult<GetContractsResult>>> GetContractsAsync([FromQuery] GetContractsRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetContractsAsync));

    [Authorize][HttpPost("SaveContract")]
    public async Task<ActionResult<ApiSuccessResult<SaveContractResult>>> SaveContractAsync(SaveContractRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveContractAsync));
    }

    [Authorize][HttpPost("DeleteContract")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> DeleteContractAsync(DeleteContractRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteContractAsync));
    }

    [Authorize][HttpPost("RenewContract")]
    public async Task<ActionResult<ApiSuccessResult<RenewContractResult>>> RenewContractAsync(RenewContractRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(RenewContractAsync));
    }

    [Authorize][HttpGet("GetHealth")]
    public async Task<ActionResult<ApiSuccessResult<GetCustomerHealthResult>>> GetHealthAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetCustomerHealthRequest(), ct), nameof(GetHealthAsync));
}
