using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.InvoiceManager.Commands;

public class UpdateInvoiceResult
{
    public Invoice? Data { get; set; }
}

public class UpdateInvoiceRequest : IRequest<UpdateInvoiceResult>
{
    public string? Id { get; init; }
    public DateTime? InvoiceDate { get; init; }
    public string? InvoiceStatus { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Description { get; init; }
    public string? CustomerId { get; init; }
    public string? TaxId { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateInvoiceValidator : AbstractValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.InvoiceDate).NotEmpty();
        RuleFor(x => x.InvoiceStatus).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.TaxId).NotEmpty();
    }
}

public class UpdateInvoiceHandler : IRequestHandler<UpdateInvoiceRequest, UpdateInvoiceResult>
{
    private readonly ICommandRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceService _invoiceService;

    public UpdateInvoiceHandler(
        ICommandRepository<Invoice> repository,
        InvoiceService invoiceService,
        IUnitOfWork unitOfWork
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
    }

    public async Task<UpdateInvoiceResult> Handle(UpdateInvoiceRequest request, CancellationToken cancellationToken)
    {

        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.UpdatedById;

        entity.InvoiceDate = request.InvoiceDate;
        entity.InvoiceStatus = (InvoiceStatus)int.Parse(request.InvoiceStatus!);
        entity.DueDate = request.DueDate;
        entity.Description = request.Description;
        entity.CustomerId = request.CustomerId;
        entity.TaxId = request.TaxId;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        _invoiceService.Recalculate(entity.Id);

        return new UpdateInvoiceResult
        {
            Data = entity
        };
    }
}

