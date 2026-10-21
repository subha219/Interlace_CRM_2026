using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationManager;

public class QuotationService
{
    private readonly ICommandRepository<Quotation> _quotationRepository;
    private readonly ICommandRepository<QuotationItem> _quotationItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public QuotationService(
        ICommandRepository<Quotation> quotationRepository,
        ICommandRepository<QuotationItem> quotationItemRepository,
        IUnitOfWork unitOfWork
        )
    {
        _quotationRepository = quotationRepository;
        _quotationItemRepository = quotationItemRepository;
        _unitOfWork = unitOfWork;
    }

    public void Recalculate(string quotationId)
    {
        var quotation = _quotationRepository
            .GetQuery()
            .IsDeletedEqualTo()
            .Where(x => x.Id == quotationId)
            .Include(x => x.Tax)
            .SingleOrDefault();

        if (quotation == null)
            return;

        var quotationItems = _quotationItemRepository
            .GetQuery()
            .IsDeletedEqualTo()
            .Where(x => x.QuotationId == quotationId)
            .ToList();

        var subtotal = quotationItems.Sum(x => x.Total ?? 0);
        var discountPercent = Math.Clamp(quotation.DiscountPercent ?? 0, 0, 100);
        quotation.DiscountAmount = Math.Round(subtotal * discountPercent / 100, 2);
        quotation.BeforeTaxAmount = subtotal - quotation.DiscountAmount;

        var taxPercentage = quotation.Tax?.Percentage ?? 0;
        quotation.TaxAmount = (quotation.BeforeTaxAmount ?? 0) * taxPercentage / 100;

        quotation.AfterTaxAmount = (quotation.BeforeTaxAmount ?? 0) + (quotation.TaxAmount ?? 0);

        _quotationRepository.Update(quotation);
        _unitOfWork.Save();
    }
}
