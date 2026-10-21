namespace Infrastructure.SecurityManager.NavigationMenu;

/// <summary>
/// Pages that need more than the role of their menu section. All dashboards live under "Dashboards", so on their own
/// they would show every user every dashboard. A user must hold the section role AND at least one role listed here.
/// Pages that are not listed only need the section role. The list travels to the browser with the menu.
/// </summary>
public static class PageAccess
{
    public static readonly IReadOnlyDictionary<string, string[]> Pages = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["/Dashboards/CustomerHealth"] = new[] { "Contracts" },
        ["/Dashboards/SupportDashboard"] = new[] { "SupportCases" },
        ["/Dashboards/DataQuality"] = new[] { "Leads", "Customers", "CustomerContacts", "SupportCases" },
        ["/Dashboards/InsightsDashboard"] = new[] { "Pipelines", "Quotations", "Invoices", "Payments" },
        ["/Dashboards/DefaultDashboard"] = new[] { "Leads", "Pipelines", "Campaigns", "Budgets", "Expenses", "SalesOrders", "Invoices", "PurchaseOrders" },
    };

    public static string[]? For(string? url) => url != null && Pages.TryGetValue(url, out var roles) ? roles : null;
}
