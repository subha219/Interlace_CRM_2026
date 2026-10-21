using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Application.Features.SalesOrderManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationManager.Commands;

public class ConvertQuotationToSalesOrderResult
{
    public SalesOrder? Data { get; set; }
}

public class ConvertQuotationToSalesOrderRequest : IRequest<ConvertQuotationToSalesOrderResult>
{
    public string? QuotationId { get; init; }
    public string? CreatedById { get; init; }
}

public class ConvertQuotationToSalesOrderValidator : AbstractValidator<ConvertQuotationToSalesOrderRequest>
{
    public ConvertQuotationToSalesOrderValidator()
    {
        RuleFor(x => x.QuotationId).NotEmpty();
    }
}

public class ConvertQuotationToSalesOrderHandler : IRequestHandler<ConvertQuotationToSalesOrderRequest, ConvertQuotationToSalesOrderResult>
{
    private readonly ICommandRepository<Quotation> _quotationRepository;
    private readonly ICommandRepository<SalesOrder> _salesOrderRepository;
    private readonly ICommandRepository<SalesOrderItem> _salesOrderItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly SalesOrderService _salesOrderService;

    public ConvertQuotationToSalesOrderHandler(
        ICommandRepository<Quotation> quotationRepository,
        ICommandRepository<SalesOrder> salesOrderRepository,
        ICommandRepository<SalesOrderItem> salesOrderItemRepository,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService,
        SalesOrderService salesOrderService
        )
    {
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
        _salesOrderItemRepository = salesOrderItemRepository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
        _salesOrderService = salesOrderService;
    }

    public async Task<ConvertQuotationToSalesOrderResult> Handle(ConvertQuotationToSalesOrderRequest request, CancellationToken cancellationToken)
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

        if (!string.IsNullOrEmpty(quotation.ConvertedSalesOrderId))
        {
            throw new Exception("This quotation has already been converted to a sales order.");
        }

        var salesOrder = new SalesOrder
        {
            CreatedById = request.CreatedById,
            Number = _numberSequenceService.GenerateNumber(nameof(SalesOrder), "", "SO"),
            OrderDate = DateTime.Now,
            OrderStatus = SalesOrderStatus.Draft,
            Description = quotation.Description,
            CustomerId = quotation.CustomerId,
            TaxId = quotation.TaxId
        };

        await _salesOrderRepository.CreateAsync(salesOrder, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        QuotationPolicy.EnsureCanProceed(quotation, QuotationStatus.Converted);
        var netFactor = QuotationPolicy.NetFactor(quotation);

        foreach (var item in quotation.QuotationItemList.Where(x => !x.IsDeleted))
        {
            await _salesOrderItemRepository.CreateAsync(new SalesOrderItem
            {
                CreatedById = request.CreatedById,
                SalesOrderId = salesOrder.Id,
                ProductId = item.ProductId,
                Summary = item.Summary,
                UnitPrice = Math.Round((item.UnitPrice ?? 0) * netFactor, 4),
                Quantity = item.Quantity,
                Total = Math.Round((item.Total ?? 0) * netFactor, 2)
            }, cancellationToken);
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        _salesOrderService.Recalculate(salesOrder.Id);

        quotation.QuotationStatus = QuotationStatus.Converted;
        quotation.ConvertedSalesOrderId = salesOrder.Id;
        quotation.UpdatedById = request.CreatedById;

        _quotationRepository.Update(quotation);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new ConvertQuotationToSalesOrderResult
        {
            Data = salesOrder
        };
    }
}
