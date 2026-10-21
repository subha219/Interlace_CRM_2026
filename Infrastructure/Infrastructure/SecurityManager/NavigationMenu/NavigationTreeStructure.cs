using Application.Common.Services.SecurityManager;
using System.Text.Json;

namespace Infrastructure.SecurityManager.NavigationMenu;






public class JsonStructureItem
{
    public string? URL { get; set; }
    public string? Name { get; set; }
    public bool IsModule { get; set; }
    public List<JsonStructureItem> Children { get; set; } = new List<JsonStructureItem>();
}

public static class NavigationTreeStructure
{

    public static readonly string JsonStructure = """
    [
        {
            "URL": "#",
            "Name": "Dashboards",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/Dashboards/MyWork",
                    "Name": "My Work",
                    "IsModule": false
                },
                {
                    "URL": "/Dashboards/CustomerHealth",
                    "Name": "Customer Health",
                    "IsModule": false
                },
                {
                    "URL": "/Dashboards/SupportDashboard",
                    "Name": "Support",
                    "IsModule": false
                },
                {
                    "URL": "/Reports/ReportBuilder",
                    "Name": "Reports",
                    "IsModule": false
                },
                {
                    "URL": "/Dashboards/DataQuality",
                    "Name": "Data Quality",
                    "IsModule": false
                },
                {
                    "URL": "/Dashboards/InsightsDashboard",
                    "Name": "Insights",
                    "IsModule": false
                },
                {
                    "URL": "/Dashboards/DefaultDashboard",
                    "Name": "Default",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Sales Pipeline",
            "IsModule": true,
            "Icon": "icon-layout",
            "Children": [
                {
                    "URL": "/Leads/LeadList",
                    "Name": "Lead",
                    "IsModule": false
                },
                {
                    "URL": "/Pipelines/PipelineBoard",
                    "Name": "Pipeline Board",
                    "IsModule": false
                },
                {
                    "URL": "/Pipelines/SalesForecast",
                    "Name": "Sales Forecast",
                    "IsModule": false
                },
                {
                    "URL": "/LeadContacts/LeadContactList",
                    "Name": "Lead Contact",
                    "IsModule": false
                },
                {
                    "URL": "/LeadActivities/LeadActivityList",
                    "Name": "Lead Activity",
                    "IsModule": false
                },
                {
                    "URL": "/SalesTargets/Leaderboard",
                    "Name": "Targets & Leaderboard",
                    "IsModule": false
                },
                {
                    "URL": "/Leads/Duplicates",
                    "Name": "Lead Duplicates",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Engage",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/Activities/ActivityList",
                    "Name": "Activities & Calendar",
                    "IsModule": false
                },
                {
                    "URL": "/Emails/EmailCenter",
                    "Name": "Email",
                    "IsModule": false
                },
                {
                    "URL": "/Documents/DocumentList",
                    "Name": "Documents",
                    "IsModule": false
                },
                {
                    "URL": "/Notifications/NotificationList",
                    "Name": "Notification",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Marketing",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/Campaigns/CampaignList",
                    "Name": "Campaign",
                    "IsModule": false
                },
                {
                    "URL": "/Budgets/BudgetList",
                    "Name": "Budget",
                    "IsModule": false
                },
                {
                    "URL": "/Expenses/ExpenseList",
                    "Name": "Expense",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Sales Teams",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/SalesTeams/SalesTeamList",
                    "Name": "Sales Team",
                    "IsModule": false
                },
                {
                    "URL": "/SalesRepresentatives/SalesRepresentativeList",
                    "Name": "Sales Representative",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Support",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/SupportCases/SupportCaseList",
                    "Name": "Support Case",
                    "IsModule": false
                },
                {
                    "URL": "/SupportCaseComments/SupportCaseCommentList",
                    "Name": "Case Comment",
                    "IsModule": false
                },
                {
                    "URL": "/KnowledgeBase/ArticleList",
                    "Name": "Knowledge Base",
                    "IsModule": false
                },
                {
                    "URL": "/Messaging/Inbox",
                    "Name": "WhatsApp & SMS",
                    "IsModule": false
                },
                {
                    "URL": "/Messaging/Templates",
                    "Name": "Message Templates",
                    "IsModule": false
                },
                {
                    "URL": "/Calls/CallLog",
                    "Name": "Calls",
                    "IsModule": false
                },
                {
                    "URL": "/Chats/ChatInbox",
                    "Name": "Live Chat",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Third Party",
            "IsModule": true,
            "Icon": "icon-layers",
            "Children": [
                {
                    "URL": "/CustomerGroups/CustomerGroupList",
                    "Name": "Customer Group",
                    "IsModule": false
                },
                {
                    "URL": "/CustomerCategories/CustomerCategoryList",
                    "Name": "Customer Category",
                    "IsModule": false
                },
                {
                    "URL": "/Customers/CustomerList",
                    "Name": "Customer",
                    "IsModule": false
                },
                {
                    "URL": "/Customers/Customer360",
                    "Name": "Customer 360",
                    "IsModule": false
                },
                {
                    "URL": "/Customers/Duplicates",
                    "Name": "Customer Duplicates",
                    "IsModule": false
                },
                {
                    "URL": "/CustomerContacts/CustomerContactList",
                    "Name": "Customer Contact",
                    "IsModule": false
                },
                {
                    "URL": "/VendorGroups/VendorGroupList",
                    "Name": "Vendor Group",
                    "IsModule": false
                },
                {
                    "URL": "/VendorCategories/VendorCategoryList",
                    "Name": "Vendor Category",
                    "IsModule": false
                },
                {
                    "URL": "/Vendors/VendorList",
                    "Name": "Vendor",
                    "IsModule": false
                },
                {
                    "URL": "/VendorContacts/VendorContactList",
                    "Name": "Vendor Contact",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Sales",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/SalesOrders/SalesOrderList",
                    "Name": "Sales Order",
                    "IsModule": false
                },
                {
                    "URL": "/Contracts/ContractList",
                    "Name": "Contract",
                    "IsModule": false
                },
                {
                    "URL": "/Signatures/SignatureList",
                    "Name": "E-Signatures",
                    "IsModule": false
                },
                {
                    "URL": "/Quotations/QuotationList",
                    "Name": "Quotation",
                    "IsModule": false
                },
                {
                    "URL": "/QuotationApprovals/ApprovalList",
                    "Name": "Quotation Approvals",
                    "IsModule": false
                },
                {
                    "URL": "/Invoices/InvoiceList",
                    "Name": "Invoice",
                    "IsModule": false
                },
                {
                    "URL": "/Payments/PaymentList",
                    "Name": "Payment",
                    "IsModule": false
                },
                {
                    "URL": "/SalesReports/SalesReportList",
                    "Name": "Sales Report",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Purchase",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/PurchaseOrders/PurchaseOrderList",
                    "Name": "Purchase Order",
                    "IsModule": false
                },
                {
                    "URL": "/PurchaseReports/PurchaseReportList",
                    "Name": "Purchase Report",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Inventory",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/UnitMeasures/UnitMeasureList",
                    "Name": "Unit Measure",
                    "IsModule": false
                },
                {
                    "URL": "/ProductGroups/ProductGroupList",
                    "Name": "Product Group",
                    "IsModule": false
                },
                {
                    "URL": "/Products/ProductList",
                    "Name": "Product",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Utilities",
            "IsModule": true,
            "Children": [   
                {
                    "URL": "/Todos/TodoList",
                    "Name": "Todo",
                    "IsModule": false
                },
                {
                    "URL": "/TodoItems/TodoItemList",
                    "Name": "Todo Item",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Membership",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/Users/UserList",
                    "Name": "Users",
                    "IsModule": false
                },
                {
                    "URL": "/Roles/RoleList",
                    "Name": "Roles",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Profiles",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/Profiles/MyProfile",
                    "Name": "My Profile",
                    "IsModule": false
                },
                {
                    "URL": "/Profiles/GoogleCalendar",
                    "Name": "Calendar Sync",
                    "IsModule": false
                },
                {
                    "URL": "/Profiles/GoogleMail",
                    "Name": "Gmail Sync",
                    "IsModule": false
                },
                {
                    "URL": "/Profiles/TwoFactor",
                    "Name": "Two-Factor Security",
                    "IsModule": false
                }
            ]
        },
        {
            "URL": "#",
            "Name": "Settings",
            "IsModule": true,
            "Children": [
                {
                    "URL": "/Companies/MyCompany",
                    "Name": "My Company",
                    "IsModule": false
                },
                {
                    "URL": "/Taxs/TaxList",
                    "Name": "Tax",
                    "IsModule": false
                },
                {
                    "URL": "/Settings/EmailSettings",
                    "Name": "Email Settings",
                    "IsModule": false
                },
                {
                    "URL": "/Integrations/Connections",
                    "Name": "Connections",
                    "IsModule": false
                },
                {
                    "URL": "/AiGovernance/AiSettings",
                    "Name": "AI Governance",
                    "IsModule": false
                },
                {
                    "URL": "/Agents/AgentCenter",
                    "Name": "AI Agents",
                    "IsModule": false
                },
                {
                    "URL": "/SupportSettings/Categories",
                    "Name": "Support Categories",
                    "IsModule": false
                },
                {
                    "URL": "/SupportSettings/SlaPolicies",
                    "Name": "Support SLA",
                    "IsModule": false
                },
                {
                    "URL": "/Workflows/WorkflowList",
                    "Name": "Workflow Rules",
                    "IsModule": false
                },
                {
                    "URL": "/PipelineSettings/StageList",
                    "Name": "Pipeline Stages",
                    "IsModule": false
                },
                {
                    "URL": "/CustomFields/CustomFieldList",
                    "Name": "Custom Fields",
                    "IsModule": false
                },
                {
                    "URL": "/Integrations/IntegrationList",
                    "Name": "Integrations",
                    "IsModule": false
                },
                {
                    "URL": "/NumberSequences/NumberSequenceList",
                    "Name": "Number Sequence",
                    "IsModule": false
                },
                {
                    "URL": "/AuditLogs/AuditLogList",
                    "Name": "Audit Log",
                    "IsModule": false
                },
                {
                    "URL": "/Imports/ImportCustomers",
                    "Name": "Import Data",
                    "IsModule": false
                }
            ]
        }
    ]
    """;

    public static List<MenuNavigationTreeNodeDto> GetCompleteMenuNavigationTreeNode()
    {
        var json = JsonStructure;

        var menus = JsonSerializer.Deserialize<List<JsonStructureItem>>(json);

        List<MenuNavigationTreeNodeDto> nodes = new List<MenuNavigationTreeNodeDto>();

        var index = 1;
        void AddNodes(List<JsonStructureItem> menuItems, string? parentId = null)
        {
            foreach (var item in menuItems)
            {
                var nodeId = index.ToString();
                if (item.IsModule)
                {
                    nodes.Add(new MenuNavigationTreeNodeDto(nodeId, item.Name ?? "", param_hasChild: true, param_expanded: false));
                }
                else
                {
                    nodes.Add(new MenuNavigationTreeNodeDto(nodeId, item.Name ?? "", parentId, item.URL) { Roles = PageAccess.For(item.URL) });
                }

                index++;

                if (item.Children != null && item.Children.Count > 0)
                {
                    AddNodes(item.Children, nodeId);
                }
            }
        }

        if (menus != null) AddNodes(menus);

        return nodes;
    }

    public static string GetFirstSegmentFromUrlPath(string? path)
    {
        var result = string.Empty;
        if (path != null && path.Contains("/"))
        {
            string[] parts = path.Split("/");
            if (parts.Length > 2)
            {
                result = parts[1];
            }
        }
        return result;
    }

    public static List<string> GetCompleteFirstMenuNavigationSegment()
    {
        var json = JsonStructure;
        var menus = JsonSerializer.Deserialize<List<JsonStructureItem>>(json);
        var result = new List<string>();

        if (menus != null)
        {
            foreach (var item in menus)
            {
                ProcessMenuItem(item, result);
            }
        }

        return result;
    }

    private static void ProcessMenuItem(JsonStructureItem item, List<string> result)
    {
        if (!string.IsNullOrEmpty(item.URL) && item.URL != "#")
        {
            var segment = GetFirstSegmentFromUrlPath(item.URL);
            if (!string.IsNullOrEmpty(segment) && !result.Contains(segment))
            {
                result.Add(segment);
            }
        }

        if (item.Children != null)
        {
            foreach (var child in item.Children)
            {
                ProcessMenuItem(child, result);
            }
        }
    }


}

