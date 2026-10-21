using Application.Features.QuotationApprovalManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>
/// Discounts, approval requests and revisions are open to quotation users (see ModulePermissions action rules);
/// deciding and changing the policy needs the QuotationApprovals role.
/// </summary>
[Route("api/[controller]")]
public class QuotationApprovalController : BaseApiController
{
    public QuotationApprovalController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetPricing")]
    public async Task<ActionResult<ApiSuccessResult<GetQuotationPricingResult>>> GetPricingAsync([FromQuery] GetQuotationPricingRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetPricingAsync));

    [Authorize][HttpPost("SetDiscount")]
    public async Task<ActionResult<ApiSuccessResult<SetQuotationDiscountResult>>> SetDiscountAsync(SetQuotationDiscountRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SetDiscountAsync));
    }

    [Authorize][HttpPost("RequestApproval")]
    public async Task<ActionResult<ApiSuccessResult<RequestQuotationApprovalResult>>> RequestApprovalAsync(RequestQuotationApprovalRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(RequestApprovalAsync));
    }

    [Authorize][HttpPost("CreateRevision")]
    public async Task<ActionResult<ApiSuccessResult<CreateQuotationRevisionResult>>> CreateRevisionAsync(CreateQuotationRevisionRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(CreateRevisionAsync));
    }

    [Authorize][HttpGet("ListPending")]
    public async Task<ActionResult<ApiSuccessResult<ListPendingApprovalsResult>>> ListPendingAsync(CancellationToken ct) =>
        Success(await _sender.Send(new ListPendingApprovalsRequest(), ct), nameof(ListPendingAsync));

    [Authorize][HttpPost("Decide")]
    public async Task<ActionResult<ApiSuccessResult<DecideQuotationApprovalResult>>> DecideAsync(DecideQuotationApprovalRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DecideAsync));
    }

    [Authorize][HttpGet("GetPolicy")]
    public async Task<ActionResult<ApiSuccessResult<GetSalesPolicyResult>>> GetPolicyAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetSalesPolicyRequest(), ct), nameof(GetPolicyAsync));

    [Authorize][HttpPost("SavePolicy")]
    public async Task<ActionResult<ApiSuccessResult<SaveSalesPolicyResult>>> SavePolicyAsync(SaveSalesPolicyRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SavePolicyAsync));
    }
}
