namespace InterlaceUI.BackEnd.Common.Security;

/// <summary>
/// Maps API controllers/actions to the roles allowed to call them.
/// Roles are the existing module roles (the first URL segment of each menu page, e.g. "Leads", "Quotations").
/// Rules are enforced on the server by <see cref="ModuleAuthorizationFilter"/>:
///   - write calls (anything other than GET/HEAD/OPTIONS) require one of the module roles;
///   - read calls are open to any signed-in user (pages need cross-module lookups such as customers or products)
///     unless the controller is marked strict, in which case reads need the module role too.
/// An empty role list means "any signed-in user".
/// </summary>
public static class ModulePermissions
{
    public sealed record Rule(string[] Roles, bool StrictRead = false);

    private static readonly string[] Open = Array.Empty<string>();

    /// <summary>Action-level rules, keyed "Controller.Action"; they win over controller rules.</summary>
    public static readonly IReadOnlyDictionary<string, Rule> ActionRules = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase)
    {
        // self-service security actions
        ["Security.Login"] = new(Open),
        ["Security.Logout"] = new(Open),
        ["Security.Register"] = new(Open),
        ["Security.ConfirmEmail"] = new(Open),
        ["Security.ForgotPassword"] = new(Open),
        ["Security.ForgotPasswordConfirmation"] = new(Open),
        ["Security.RefreshToken"] = new(Open),
        ["Security.ValidateToken"] = new(Open),
        ["Security.GetMyProfileList"] = new(Open),
        ["Security.UpdateMyProfile"] = new(Open),
        ["Security.UpdateMyProfilePassword"] = new(Open),
        ["Security.UpdateMyProfileAvatar"] = new(Open),
        ["Security.GetTwoFactorSetup"] = new(Open),
        ["Security.EnableTwoFactor"] = new(Open),
        ["Security.DisableTwoFactor"] = new(Open),

        // user and role administration
        ["Security.GetRoleList"] = new(new[] { "Users", "Roles" }, true),
        ["Security.GetUserList"] = new(new[] { "Users", "Roles" }, true),
        ["Security.CreateUser"] = new(new[] { "Users" }, true),
        ["Security.UpdateUser"] = new(new[] { "Users" }, true),
        ["Security.DeleteUser"] = new(new[] { "Users" }, true),
        ["Security.UpdatePasswordUser"] = new(new[] { "Users" }, true),
        ["Security.GetUserRoles"] = new(new[] { "Users", "Roles" }, true),
        ["Security.UpdateUserRole"] = new(new[] { "Roles", "Users" }, true),

        ["Chat.CreateCase"] = new(new[] { "SupportCases" }, true),
        ["Contract.GetHealth"] = new(new[] { "Contracts" }, true),

        // dashboard data: each call needs the module whose numbers it returns
        ["Dashboard.GetCRMDashboard"] = new(new[] { "Leads", "Pipelines", "Campaigns", "Budgets", "Expenses" }, true),
        ["Dashboard.GetCardsDashboard"] = new(new[] { "SalesOrders", "Invoices", "PurchaseOrders" }, true),
        ["Dashboard.GetSalesDashboard"] = new(new[] { "SalesOrders", "Invoices", "Quotations" }, true),
        ["Dashboard.GetPurchaseDashboard"] = new(new[] { "PurchaseOrders" }, true),
        ["Dashboard.GetLeadPipelineFunnel"] = new(new[] { "Leads", "Pipelines" }, true),
        ["Dashboard.GetSalesTeamLeadClosing"] = new(new[] { "Leads", "Pipelines" }, true),
        ["Dashboard.GetCampaignByStatus"] = new(new[] { "Campaigns" }, true),
        ["Dashboard.GetLeadActivityByType"] = new(new[] { "LeadActivities", "Leads" }, true),
        ["SupportInsight.ReplyToCustomer"] = new(new[] { "SupportCases" }, true),
        ["Ai.GetGovernance"] = new(new[] { "AiGovernance" }, true),
        ["Ai.SaveSettings"] = new(new[] { "AiGovernance" }, true),
        ["Knowledge.SaveCategory"] = new(new[] { "SupportSettings" }, true),
        ["Knowledge.DeleteCategory"] = new(new[] { "SupportSettings" }, true),
        ["Knowledge.AssignCategory"] = new(new[] { "SupportCases" }, true),
        ["Knowledge.GetCaseCategory"] = new(new[] { "SupportCases" }, true),
        ["Knowledge.SaveArticle"] = new(new[] { "KnowledgeBase" }, true),
        ["Knowledge.DeleteArticle"] = new(new[] { "KnowledgeBase" }, true),
        ["SupportInsight.GetSla"] = new(new[] { "SupportSettings", "SupportCases" }, true),
        ["SupportInsight.SaveSla"] = new(new[] { "SupportSettings" }, true),
        ["SupportInsight.GetCsat"] = new(new[] { "SupportCases" }, true),
        ["SupportInsight.RecordCsat"] = new(new[] { "SupportCases" }, true),
        ["SupportInsight.SendSurvey"] = new(new[] { "SupportCases" }, true),
        ["CustomField.SaveDefinition"] = new(new[] { "CustomFields" }, true),
        ["CustomField.DeleteDefinition"] = new(new[] { "CustomFields" }, true),

        ["QuotationApproval.SetDiscount"] = new(new[] { "Quotations" }, true),
        ["QuotationApproval.RequestApproval"] = new(new[] { "Quotations" }, true),
        ["QuotationApproval.CreateRevision"] = new(new[] { "Quotations" }, true),
        ["QuotationApproval.GetPricing"] = new(new[] { "Quotations" }, true),

        // POST endpoints that only read or that every user needs
        ["Assistant.Ask"] = new(Open),
        ["Notification.MarkNotificationRead"] = new(Open),
    };

    /// <summary>Controller-level rules, keyed by controller name without the "Controller" suffix.</summary>
    public static readonly IReadOnlyDictionary<string, Rule> ControllerRules = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase)
    {
        ["Agent"] = new(new[] { "Agents" }, true),
        ["Connection"] = new(new[] { "Integrations" }, true),
        ["Activity"] = new(new[] { "Activities" }),
        ["Assistant"] = new(Open),
        ["AuditLog"] = new(new[] { "AuditLogs" }, true),
        ["Budget"] = new(new[] { "Budgets" }),
        ["Report"] = new(Open),
        ["SupportInsight"] = new(new[] { "SupportCases" }, true),
        ["Portal"] = new(Open),
        ["Calendar"] = new(new[] { "Profiles" }, true),
        ["MailSync"] = new(new[] { "Profiles" }, true),
        ["Sso"] = new(Open),
        ["Messaging"] = new(new[] { "Messaging" }, true),
        ["Call"] = new(new[] { "Calls" }, true),
        ["Chat"] = new(new[] { "Chats" }, true),
        ["Signature"] = new(new[] { "Signatures", "Contracts", "Quotations" }, true),
        ["PublicSign"] = new(Open),
        ["PublicChat"] = new(Open),
        ["ChannelWebhook"] = new(Open),
        ["Ai"] = new(Open),
        ["Contract"] = new(new[] { "Contracts" }, true),
        ["Knowledge"] = new(new[] { "KnowledgeBase", "SupportCases" }, true),
        ["Workflow"] = new(new[] { "Workflows" }, true),
        ["PipelineSetting"] = new(new[] { "PipelineSettings" }, true),
        ["QuotationApproval"] = new(new[] { "QuotationApprovals" }, true),
        ["Integration"] = new(new[] { "Integrations" }, true),
        ["Public"] = new(Open),
        ["CustomField"] = new(Open),
        ["SalesTarget"] = new(new[] { "SalesTargets" }, true),
        ["MyWork"] = new(new[] { "Dashboards" }, true),
        ["Bulk"] = new(new[] { "Customers", "Leads" }),
        ["Campaign"] = new(new[] { "Campaigns" }),
        ["Company"] = new(new[] { "Companies" }),
        ["Customer"] = new(new[] { "Customers" }),
        ["Customer360"] = new(new[] { "Customers" }, true),
        ["CustomerCategory"] = new(new[] { "CustomerCategories" }),
        ["CustomerContact"] = new(new[] { "CustomerContacts", "Customers" }),
        ["CustomerGroup"] = new(new[] { "CustomerGroups" }),
        ["Dashboard"] = new(new[] { "Dashboards" }, true),
        ["DataQuality"] = new(new[] { "Leads", "Customers", "CustomerContacts", "SupportCases" }, true),
        ["Document"] = new(new[] { "Documents" }),
        ["Duplicate"] = new(new[] { "Customers", "Leads" }, true),
        ["Email"] = new(new[] { "Emails" }),
        ["Expense"] = new(new[] { "Expenses" }),
        ["FileDocument"] = new(Open),
        ["FileImage"] = new(Open),
        ["Import"] = new(new[] { "Imports" }, true),
        ["Insights"] = new(new[] { "Pipelines", "Quotations", "Invoices", "Payments" }, true),
        ["Invoice"] = new(new[] { "Invoices", "Quotations" }),
        ["InvoiceItem"] = new(new[] { "Invoices", "Quotations" }),
        ["Lead"] = new(new[] { "Leads", "Pipelines" }),
        ["LeadActivity"] = new(new[] { "LeadActivities", "Leads" }),
        ["LeadContact"] = new(new[] { "LeadContacts", "Leads" }),
        ["Notification"] = new(Open),
        ["NumberSequence"] = new(new[] { "NumberSequences" }),
        ["Payment"] = new(new[] { "Payments", "Invoices" }),
        ["Product"] = new(new[] { "Products" }),
        ["ProductGroup"] = new(new[] { "ProductGroups" }),
        ["PurchaseOrder"] = new(new[] { "PurchaseOrders" }),
        ["PurchaseOrderItem"] = new(new[] { "PurchaseOrders" }),
        ["Quotation"] = new(new[] { "Quotations" }),
        ["QuotationItem"] = new(new[] { "Quotations" }),
        ["SalesOrder"] = new(new[] { "SalesOrders", "Quotations" }),
        ["SalesOrderItem"] = new(new[] { "SalesOrders", "Quotations" }),
        ["SalesRepresentative"] = new(new[] { "SalesRepresentatives" }),
        ["SalesTeam"] = new(new[] { "SalesTeams" }),
        ["Search"] = new(Open),
        ["SavedView"] = new(Open),
        ["Setting"] = new(new[] { "Settings" }, true),
        ["SupportCase"] = new(new[] { "SupportCases", "SupportCaseComments" }),
        ["SupportCaseComment"] = new(new[] { "SupportCaseComments", "SupportCases" }),
        ["Tax"] = new(new[] { "Taxs" }),
        ["Todo"] = new(new[] { "Todos", "TodoItems" }),
        ["TodoItem"] = new(new[] { "TodoItems", "Todos" }),
        ["UnitMeasure"] = new(new[] { "UnitMeasures" }),
        ["Vendor"] = new(new[] { "Vendors" }),
        ["VendorCategory"] = new(new[] { "VendorCategories" }),
        ["VendorContact"] = new(new[] { "VendorContacts", "Vendors" }),
        ["VendorGroup"] = new(new[] { "VendorGroups" }),
        // controllers whose rules are defined per action
        ["Security"] = new(Open),
    };

    public static bool IsRead(string? httpMethod) =>
        HttpMethods.IsGet(httpMethod ?? "") || HttpMethods.IsHead(httpMethod ?? "") || HttpMethods.IsOptions(httpMethod ?? "");

    /// <summary>
    /// Returns the roles required for this call, or null when any signed-in user may call it.
    /// </summary>
    public static string[]? RequiredRoles(string controller, string action, string? httpMethod)
    {
        var read = IsRead(httpMethod);

        if (ActionRules.TryGetValue($"{controller}.{action}", out var actionRule))
        {
            if (actionRule.Roles.Length == 0) return null;
            if (read && !actionRule.StrictRead) return null;
            return actionRule.Roles;
        }

        if (ControllerRules.TryGetValue(controller, out var rule))
        {
            if (rule.Roles.Length == 0) return null;
            if (read && !rule.StrictRead) return null;
            return rule.Roles;
        }

        //>>> a controller without a rule is a mistake; unit tests fail when one is missing.
        //>>> Until then fail closed for writes and stay open for reads.
        return read ? null : new[] { "__unmapped_controller__" };
    }
}
