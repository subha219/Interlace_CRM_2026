using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationManager.Commands;

public class ConvertQuotationToInvoiceResult
{
    public Invoice? Data { get; set; }
}

public class ConvertQuotationToInvoiceRequest : IRequest<ConvertQuotationToInvoiceResult>
{
    public string? QuotationId { get; init; }
    public int? PaymentTermDays { get; init; }
    public string? CreatedById { get; init; }
}

public class ConvertQuotationToInvoiceValidator : AbstractValidator<ConvertQuotationToInvoiceRequest>
{
    public ConvertQuotationToInvoiceValidator()
    {
        RuleFor(x => x.QuotationId).NotEmpty();
    }
}

public class ConvertQuotationToInvoiceHandler : IRequestHandler<ConvertQuotationToInvoiceRequest, ConvertQuotationToInvoiceResult>
{
    private readonly ICommandRepository<Quotation> _quotationRepository;
    private readonly ICommandRepository<Invoice> _invoiceRepository;
    private readonly ICommandRepository<InvoiceItem> _invoiceItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly InvoiceService _invoiceService;

    public ConvertQuotationToInvoiceHandler(
        ICommandRepository<Quotation> quotationRepository,
        ICommandRepository<Invoice> invoiceRepository,
        ICommandRepository<InvoiceItem> invoiceItemRepository,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService,
        InvoiceService invoiceService
        )
    {
        _quotationRepository = quotationRepository;
        _invoiceRepository = invoiceRepository;
        _invoiceItemRepository = invoiceItemRepository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
        _invoiceService = invoiceService;
    }

    public async Task<ConvertQuotationToInvoiceResult> Handle(ConvertQuotationToInvoiceRequest request, CancellationToken cancellationToken)
    {
        var quotation = await _quotationRepository
            .GetQuery()
            .Include(x => x.QuotationItemList)
            .Where(x => !x.IsDeleted && x.Id == request.QuotationId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quotation == null)
        {
            throw new Exception($"Entity not found: {request.QuotationId}");
        }

        if (!string.IsNullOrEmpty(quotation.ConvertedInvoiceId))
        {
            throw new Exception("This quotation has already been converted to an invoice.");
        }

        var termDays = request.PaymentTermDays ?? 30;

        var invoice = new Invoice
        {
            CreatedById = request.CreatedById,
            Number = _numberSequenceService.GenerateNumber(nameof(Invoice), "", "INV"),
            InvoiceDate = DateTime.Now,
            DueDate = DateTime.Now.AddDays(termDays),
            InvoiceStatus = InvoiceStatus.Draft,
            Description = quotation.Description,
            CustomerId = quotation.CustomerId,
            TaxId = quotation.TaxId,
            QuotationId = quotation.Id
        };

        await _invoiceRepository.CreateAsync(invoice, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        QuotationPolicy.EnsureCanProceed(quotation, QuotationStatus.Converted);
        var netFactor = QuotationPolicy.NetFactor(quotation);

        foreach (var item in quotation.QuotationItemList.Where(x => !x.IsDeleted))
        {
            await _invoiceItemRepository.CreateAsync(new InvoiceItem
            {
                CreatedById = request.CreatedById,
                InvoiceId = invoice.Id,
                ProductId = item.ProductId,
                Summary = item.Summary,
                UnitPrice = Math.Round((item.UnitPrice ?? 0) * netFactor, 4),
                Quantity = item.Quantity,
                Total = Math.Round((item.Total ?? 0) * netFactor, 2)
            }, cancellationToken);
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        _invoiceService.Recalculate(invoice.Id);

        quotation.QuotationStatus = QuotationStatus.Converted;
        quotation.ConvertedInvoiceId = invoice.Id;
        quotation.UpdatedById = request.CreatedById;

        _quotationRepository.Update(quotation);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new ConvertQuotationToInvoiceResult
        {
            Data = invoice
        };
    }
}
