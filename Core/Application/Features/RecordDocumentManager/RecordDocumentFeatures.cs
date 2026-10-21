using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Common.Services.FileDocumentManager;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.RecordDocumentManager;

public class AttachDocumentResult { public RecordDocument? Data { get; init; } }

public class AttachDocumentRequest : IRequest<AttachDocumentResult>
{
    public string? OriginalFileName { get; init; }
    public string? Extension { get; init; }
    public byte[]? Data { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? RelatedName { get; init; }
    public string? Description { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
}

public class AttachDocumentValidator : AbstractValidator<AttachDocumentRequest>
{
    public AttachDocumentValidator()
    {
        RuleFor(x => x.OriginalFileName).NotEmpty();
        RuleFor(x => x.Extension).NotEmpty();
        RuleFor(x => x.Data).NotEmpty();
        RuleFor(x => x.RelatedType).NotEmpty();
        RuleFor(x => x.RelatedId).NotEmpty();
    }
}

public class AttachDocumentHandler : IRequestHandler<AttachDocumentRequest, AttachDocumentResult>
{
    private readonly IFileDocumentService _files;
    private readonly ICommandRepository<RecordDocument> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AttachDocumentHandler(IFileDocumentService files, ICommandRepository<RecordDocument> repository, IUnitOfWork unitOfWork)
    {
        _files = files;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AttachDocumentResult> Handle(AttachDocumentRequest request, CancellationToken cancellationToken)
    {
        var stored = await _files.UploadAsync(request.OriginalFileName, request.Extension, request.Data, request.Data!.Length, request.Description, request.UserId, cancellationToken);

        var entity = new RecordDocument
        {
            CreatedById = request.UserId,
            RelatedType = request.RelatedType,
            RelatedId = request.RelatedId,
            RelatedName = request.RelatedName,
            GeneratedName = stored,
            OriginalName = request.OriginalFileName,
            Description = request.Description,
            FileSize = request.Data.Length,
            UploadedByName = request.UserName
        };

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new AttachDocumentResult { Data = entity };
    }
}

public class GetRecordDocumentListResult { public List<RecordDocument>? Data { get; init; } }

public class GetRecordDocumentListRequest : IRequest<GetRecordDocumentListResult>
{
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public int Top { get; init; } = 300;
}

public class GetRecordDocumentListHandler : IRequestHandler<GetRecordDocumentListRequest, GetRecordDocumentListResult>
{
    private readonly IQueryContext _context;
    public GetRecordDocumentListHandler(IQueryContext context) { _context = context; }

    public async Task<GetRecordDocumentListResult> Handle(GetRecordDocumentListRequest request, CancellationToken cancellationToken)
    {
        var query = _context.RecordDocument.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.RelatedType)) query = query.Where(x => x.RelatedType == request.RelatedType);
        if (!string.IsNullOrEmpty(request.RelatedId)) query = query.Where(x => x.RelatedId == request.RelatedId);

        return new GetRecordDocumentListResult
        {
            Data = await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(request.Top, 1, 1000)).ToListAsync(cancellationToken)
        };
    }
}

public class GetRecordDocumentFileResult
{
    public byte[]? Data { get; init; }
    public string? OriginalName { get; init; }
    public string? Extension { get; init; }
}

public class GetRecordDocumentFileRequest : IRequest<GetRecordDocumentFileResult?>
{
    public string? Id { get; init; }
}

public class GetRecordDocumentFileHandler : IRequestHandler<GetRecordDocumentFileRequest, GetRecordDocumentFileResult?>
{
    private readonly IQueryContext _context;
    private readonly IFileDocumentService _files;

    public GetRecordDocumentFileHandler(IQueryContext context, IFileDocumentService files)
    {
        _context = context;
        _files = files;
    }

    public async Task<GetRecordDocumentFileResult?> Handle(GetRecordDocumentFileRequest request, CancellationToken cancellationToken)
    {
        var doc = await _context.RecordDocument.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, cancellationToken);
        if (doc?.GeneratedName == null) return null;

        return new GetRecordDocumentFileResult
        {
            Data = await _files.GetFileAsync(doc.GeneratedName, cancellationToken),
            OriginalName = doc.OriginalName,
            Extension = Path.GetExtension(doc.GeneratedName)
        };
    }
}

public class DeleteRecordDocumentRequest : IRequest<AttachDocumentResult>
{
    public string? Id { get; init; }
    public string? UserId { get; set; }
}

public class DeleteRecordDocumentValidator : AbstractValidator<DeleteRecordDocumentRequest>
{
    public DeleteRecordDocumentValidator() { RuleFor(x => x.Id).NotEmpty(); }
}

public class DeleteRecordDocumentHandler : IRequestHandler<DeleteRecordDocumentRequest, AttachDocumentResult>
{
    private readonly ICommandRepository<RecordDocument> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRecordDocumentHandler(ICommandRepository<RecordDocument> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AttachDocumentResult> Handle(DeleteRecordDocumentRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Document not found.");
        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new AttachDocumentResult { Data = entity };
    }
}
