using System.ComponentModel;

namespace Domain.Enums;

public enum CaseChannel
{
    [Description("Email")]
    Email = 0,
    [Description("Phone")]
    Phone = 1,
    [Description("Web")]
    Web = 2,
    [Description("Chat")]
    Chat = 3,
    [Description("Social Media")]
    SocialMedia = 4,
    [Description("Other")]
    Other = 5
}
