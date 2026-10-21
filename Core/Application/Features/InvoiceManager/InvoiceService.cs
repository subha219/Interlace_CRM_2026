using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.InvoiceManager;

public class InvoiceService
{
    private readonly ICommandRepository<Invoice> _invoiceRepository;
    private readonly ICommandRepository<InvoiceItem> _invoiceItemRepository;
    private readonly ICommandRepository<Payment> _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InvoiceService(
        ICommandRepository<Invoice> invoiceRepository,
        ICommandRepository<InvoiceItem> invoiceItemRepository,
        ICommandRepository<Payment> paymentRepository,
        IUnitOfWork unitOfWork
        )
    {
        _invoiceRepository = invoiceRepository;
        _invoiceItemRepository = invoiceItemRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public void Recalculate(string invoiceId)
    {
        var invoice = _invoiceRepository
            .GetQuery()
            .IsDeletedEqualTo()
            .Where(x => x.Id == invoiceId)
            .Include(x => x.Tax)
            .SingleOrDefault();

        if (invoice == null)
            return;

        var invoiceItems = _invoiceItemRepository
            .GetQuery()
            .IsDeletedEqualTo()
            .Where(x => x.InvoiceId == invoiceId)
            .ToList();

        invoice.BeforeTaxAmount = invoiceItems.Sum(x => x.Total ?? 0);

        var taxPercentage = invoice.Tax?.Percentage ?? 0;
        invoice.TaxAmount = (invoice.BeforeTaxAmount ?? 0) * taxPercentage / 100;

        invoice.AfterTaxAmount = (invoice.BeforeTaxAmount ?? 0) + (invoice.TaxAmount ?? 0);

        var amountPaid = _paymentRepository
            .GetQuery()
            .IsDeletedEqualTo()
            .Where(x => x.InvoiceId == invoiceId)
            .Sum(x => x.Amount ?? 0);

        invoice.AmountPaid = amountPaid;
        invoice.BalanceDue = (invoice.AfterTaxAmount ?? 0) - amountPaid;
        invoice.InvoiceStatus = ResolveStatus(invoice);

        _invoiceRepository.Update(invoice);
        _unitOfWork.Save();
    }

    /// <summary>
    /// Keeps the invoice status in line with its payments and due date.
    /// Cancelled and Draft invoices are never changed by due dates, only by the user.
    /// </summary>
    public static InvoiceStatus? ResolveStatus(Invoice invoice)
    {
        var current = invoice.InvoiceStatus;

        if (current == InvoiceStatus.Cancelled)
        {
            return current;
        }

        var total = invoice.AfterTaxAmount ?? 0;
        var paid = invoice.AmountPaid ?? 0;

        if (total > 0 && paid >= total)
        {
            return InvoiceStatus.Paid;
        }

        if (paid > 0)
        {
            return InvoiceStatus.PartiallyPaid;
        }

        //>>> nothing is paid (anymore)
        if (current == InvoiceStatus.Paid || current == InvoiceStatus.PartiallyPaid)
        {
            current = InvoiceStatus.Sent;
        }

        var pastDue = invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.Now.Date;

        if (current == InvoiceStatus.Sent && pastDue)
        {
            return InvoiceStatus.Overdue;
        }

        if (current == InvoiceStatus.Overdue && !pastDue)
        {
            return InvoiceStatus.Sent;
        }

        return current;
    }
}
