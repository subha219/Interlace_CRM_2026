using System.ComponentModel;

namespace Domain.Enums;

public enum PaymentMethod
{
    [Description("Cash")]
    Cash = 0,
    [Description("Bank Transfer")]
    BankTransfer = 1,
    [Description("Credit Card")]
    CreditCard = 2,
    [Description("Cheque")]
    Cheque = 3,
    [Description("Online Payment")]
    OnlinePayment = 4,
    [Description("Other")]
    Other = 5
}
