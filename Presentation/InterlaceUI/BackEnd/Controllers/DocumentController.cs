using Application.Features.RecordDocumentManager;
using Infrastructure.FileDocumentManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class DocumentController : BaseApiController
{
    private static readonly string[] RelatedTypes = { "Lead", "Customer", "SupportCase", "Quotation", "Invoice" };

    public DocumentController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize]
    [HttpPost("Upload")]
    [RequestSizeLimit(30_000_000)]
    public async Task<ActionResult<ApiSuccessResult<AttachDocumentResult>>> UploadAsync(
        IFormFile file, [FromForm] string relatedType, [FromForm] string relatedId, [FromForm] string? relatedName, [FromForm] string? description,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("Invalid file.");
        if (!RelatedTypes.Contains(relatedType)) return BadRequest("Unknown record type.");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var result = await _sender.Send(new AttachDocumentRequest
        {
            OriginalFileName = Path.GetFileName(file.FileName),
            Extension = Path.GetExtension(file.FileName).TrimStart('.'),
            Data = stream.ToArray(),
            RelatedType = relatedType,
            RelatedId = relatedId,
            RelatedName = relatedName,
            Description = description,
            UserId = CurrentUserId,
            UserName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim()
        }, cancellationToken);

        return Success(result, nameof(UploadAsync));
    }

    [Authorize]
    [HttpGet("GetDocumentList")]
    public async Task<ActionResult<ApiSuccessResult<GetRecordDocumentListResult>>> GetDocumentListAsync([FromQuery] GetRecordDocumentListRequest request, CancellationToken cancellationToken) =>
        Success(await _sender.Send(request, cancellationToken), nameof(GetDocumentListAsync));

    [Authorize]
    [HttpGet("Download")]
    public async Task<IActionResult> DownloadAsync([FromQuery] string id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetRecordDocumentFileRequest { Id = id }, cancellationToken);
        if (result?.Data == null) return NotFound("Document not found.");

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Data, FileDocumentHelper.GetMimeType(result.Extension ?? string.Empty), result.OriginalName);
    }

    [Authorize]
    [HttpPost("DeleteDocument")]
    public async Task<ActionResult<ApiSuccessResult<AttachDocumentResult>>> DeleteDocumentAsync(DeleteRecordDocumentRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(DeleteDocumentAsync));
    }
}
