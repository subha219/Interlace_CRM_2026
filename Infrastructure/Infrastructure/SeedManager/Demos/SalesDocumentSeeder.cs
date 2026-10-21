using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Application.Features.NumberSequenceManager;
using Application.Features.QuotationManager;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SeedManager.Demos;

public class SalesDocumentSeeder
{
    private readonly QuotationService _quotationService;
    private readonly InvoiceService _invoiceService;
    private readonly ICommandRepository<Quotation> _quotationRepository;
    private readonly ICommandRepository<QuotationItem> _quotationItemRepository;
    private readonly ICommandRepository<Invoice> _invoiceRepository;
    private readonly ICommandRepository<InvoiceItem> _invoiceItemRepository;
    private readonly ICommandRepository<Payment> _paymentRepository;
    private readonly ICommandRepository<Customer> _customerRepository;
    private readonly ICommandRepository<Tax> _taxRepository;
    private readonly ICommandRepository<Product> _productRepository;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly IUnitOfWork _unitOfWork;

    public SalesDocumentSeeder(
        QuotationService quotationService,
        InvoiceService invoiceService,
        ICommandRepository<Quotation> quotationRepository,
        ICommandRepository<QuotationItem> quotationItemRepository,
        ICommandRepository<Invoice> invoiceRepository,
        ICommandRepository<InvoiceItem> invoiceItemRepository,
        ICommandRepository<Payment> paymentRepository,
        ICommandRepository<Customer> customerRepository,
        ICommandRepository<Tax> taxRepository,
        ICommandRepository<Product> productRepository,
        NumberSequenceService numberSequenceService,
        IUnitOfWork unitOfWork
    )
    {
        _quotationService = quotationService;
        _invoiceService = invoiceService;
        _quotationRepository = quotationRepository;
        _quotationItemRepository = quotationItemRepository;
        _invoiceRepository = invoiceRepository;
        _invoiceItemRepository = invoiceItemRepository;
        _paymentRepository = paymentRepository;
        _customerRepository = customerRepository;
        _taxRepository = taxRepository;
        _productRepository = productRepository;
        _numberSequenceService = numberSequenceService;
        _unitOfWork = unitOfWork;
    }

    public async Task GenerateDataAsync()
    {
        var random = new Random();
        var customers = await _customerRepository.GetQuery().Where(x => !x.IsDeleted).Select(x => x.Id).ToListAsync();
        var taxes = await _taxRepository.GetQuery().Where(x => !x.IsDeleted).Select(x => x.Id).ToListAsync();
        var products = await _productRepository.GetQuery().Where(x => !x.IsDeleted).ToListAsync();

        if (customers.Count == 0 || taxes.Count == 0 || products.Count == 0)
        {
            return;
        }

        var today = DateTime.Now.Date;

        //>>> quotations: spread over the last 3 months
        for (int i = 0; i < 12; i++)
        {
            var quotationDate = today.AddDays(-random.Next(1, 90));
            var quotation = new Quotation
            {
                Number = _numberSequenceService.GenerateNumber(nameof(Quotation), "", "QUO"),
                QuotationDate = quotationDate,
                ValidUntil = quotationDate.AddDays(30),
                QuotationStatus = (QuotationStatus)random.Next(0, 4),
                CustomerId = customers[random.Next(customers.Count)],
                TaxId = taxes[random.Next(taxes.Count)],
            };
            await _quotationRepository.CreateAsync(quotation);

            int itemCount = random.Next(2, 5);
            for (int j = 0; j < itemCount; j++)
            {
                var qty = random.Next(1, 5);
                var product = products[random.Next(products.Count)];
                await _quotationItemRepository.CreateAsync(new QuotationItem
                {
                    QuotationId = quotation.Id,
                    ProductId = product.Id,
                    Summary = product.Number,
                    UnitPrice = product.UnitPrice,
                    Quantity = qty,
                    Total = product.UnitPrice * qty
                });
            }

            await _unitOfWork.SaveAsync();
            _quotationService.Recalculate(quotation.Id);
        }

        //>>> invoices: some paid, some partially paid, some overdue
        for (int i = 0; i < 15; i++)
        {
            var invoiceDate = today.AddDays(-random.Next(1, 120));
            var invoice = new Invoice
            {
                Number = _numberSequenceService.GenerateNumber(nameof(Invoice), "", "INV"),
                InvoiceDate = invoiceDate,
                DueDate = invoiceDate.AddDays(30),
                InvoiceStatus = InvoiceStatus.Sent,
                CustomerId = customers[random.Next(customers.Count)],
                TaxId = taxes[random.Next(taxes.Count)],
            };
            await _invoiceRepository.CreateAsync(invoice);

            int itemCount = random.Next(2, 5);
            for (int j = 0; j < itemCount; j++)
            {
                var qty = random.Next(1, 5);
                var product = products[random.Next(products.Count)];
                await _invoiceItemRepository.CreateAsync(new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    ProductId = product.Id,
                    Summary = product.Number,
                    UnitPrice = product.UnitPrice,
                    Quantity = qty,
                    Total = product.UnitPrice * qty
                });
            }

            await _unitOfWork.SaveAsync();
            _invoiceService.Recalculate(invoice.Id);

            var saved = await _invoiceRepository.GetQuery().AsNoTracking().FirstAsync(x => x.Id == invoice.Id);
            var total = saved.AfterTaxAmount ?? 0;
            var scenario = random.Next(0, 3); //>>> 0 = unpaid, 1 = partially paid, 2 = fully paid

            if (scenario > 0 && total > 0)
            {
                var amount = scenario == 2 ? total : Math.Round(total * 0.4, 2);
                var paymentDate = invoiceDate.AddDays(random.Next(1, 20));
                if (paymentDate > today)
                {
                    paymentDate = today;
                }

                await _paymentRepository.CreateAsync(new Payment
                {
                    Number = _numberSequenceService.GenerateNumber(nameof(Payment), "", "PAY"),
                    InvoiceId = invoice.Id,
                    PaymentDate = paymentDate,
                    Amount = amount,
                    Method = (PaymentMethod)random.Next(0, Enum.GetNames(typeof(PaymentMethod)).Length),
                    Reference = "REF-" + random.Next(10000, 99999),
                    Description = scenario == 2 ? "Full payment" : "Advance payment"
                });
                await _unitOfWork.SaveAsync();
                _invoiceService.Recalculate(invoice.Id);
            }
        }
    }
}
