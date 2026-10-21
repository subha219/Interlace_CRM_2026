namespace Infrastructure.SecurityManager.Roles;

public record JobProfile(string Key, string Title, string FirstName, string LastName, string[] Roles)
{
    public string Email => $"{Key}@root.com";
}

/// <summary>
/// Ready-made bundles of module roles for common jobs. A "role" in this system is the first part of a menu address
/// (for example Leads or SupportCases); a job profile is just a sensible set of them.
/// Every name here must exist in the navigation menu; a unit test checks that.
/// </summary>
public static class JobProfiles
{
    /// <summary>What everybody gets: their own work, notes and files.</summary>
    public static readonly string[] Basics = { "Profiles", "Dashboards", "Notifications", "Todos", "TodoItems", "Activities", "Documents", "Emails" };

    private static string[] With(params string[] extra) => Basics.Concat(extra).Distinct().ToArray();

    public static readonly IReadOnlyList<JobProfile> All = new List<JobProfile>
    {
        new("salesrep", "Sales representative", "Sales", "Rep", With(
            "Leads", "Pipelines", "LeadContacts", "LeadActivities", "Customers", "CustomerContacts", "Quotations", "SalesOrders", "Contracts", "Signatures",
            "Messaging", "Calls", "Chats", "Reports", "Products")),

        new("salesmanager", "Sales manager", "Sales", "Manager", With(
            "Leads", "Pipelines", "LeadContacts", "LeadActivities", "Customers", "CustomerContacts", "CustomerGroups", "CustomerCategories",
            "Quotations", "QuotationApprovals", "SalesOrders", "Invoices", "Payments", "Contracts", "Signatures", "SalesTargets", "SalesTeams", "SalesRepresentatives",
            "SalesReports", "Reports", "Campaigns", "Imports", "PipelineSettings", "Workflows", "CustomFields", "Agents", "Messaging", "Calls", "Chats", "Products", "ProductGroups", "UnitMeasures")),

        new("supportagent", "Support agent", "Support", "Agent", With(
            "SupportCases", "SupportCaseComments", "KnowledgeBase", "Customers", "CustomerContacts", "Contracts", "Messaging", "Calls", "Chats")),

        new("supportmanager", "Support manager", "Support", "Manager", With(
            "SupportCases", "SupportCaseComments", "KnowledgeBase", "SupportSettings", "Customers", "CustomerContacts", "Contracts", "Signatures",
            "Messaging", "Calls", "Chats", "Reports", "Workflows", "Imports", "Agents")),

        new("marketing", "Marketing", "Marketing", "User", With(
            "Campaigns", "Budgets", "Leads", "LeadContacts", "Customers", "CustomerContacts", "CustomerGroups", "CustomerCategories", "Messaging", "Reports", "Imports")),

        new("finance", "Finance", "Finance", "User", With(
            "Invoices", "Payments", "SalesOrders", "PurchaseOrders", "SalesReports", "PurchaseReports", "Budgets", "Expenses", "Taxs", "Customers", "Vendors",
            "VendorGroups", "VendorCategories", "VendorContacts", "Contracts", "Quotations", "Products", "ProductGroups", "UnitMeasures", "Reports"))
    };
}
