using System.ComponentModel;

namespace Domain.Enums;

public enum LifecycleStage
{
    [Description("Subscriber")]
    Subscriber = 0,
    [Description("Lead")]
    Lead = 1,
    [Description("Marketing qualified")]
    MarketingQualified = 2,
    [Description("Sales qualified")]
    SalesQualified = 3,
    [Description("Opportunity")]
    Opportunity = 4,
    [Description("Customer")]
    Customer = 5,
    [Description("Evangelist")]
    Evangelist = 6,
    [Description("Other")]
    Other = 7
}
