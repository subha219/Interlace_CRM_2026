using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Repositories;


public interface IEntityDbSet
{
    public DbSet<Token> Token { get; set; }
    public DbSet<Todo> Todo { get; set; }
    public DbSet<TodoItem> TodoItem { get; set; }
    public DbSet<Company> Company { get; set; }
    public DbSet<FileImage> FileImage { get; set; }
    public DbSet<FileDocument> FileDocument { get; set; }

    public DbSet<NumberSequence> NumberSequence { get; set; }
    public DbSet<CustomerGroup> CustomerGroup { get; set; }
    public DbSet<CustomerCategory> CustomerCategory { get; set; }
    public DbSet<VendorGroup> VendorGroup { get; set; }
    public DbSet<VendorCategory> VendorCategory { get; set; }
    public DbSet<Customer> Customer { get; set; }
    public DbSet<Vendor> Vendor { get; set; }
    public DbSet<UnitMeasure> UnitMeasure { get; set; }
    public DbSet<ProductGroup> ProductGroup { get; set; }
    public DbSet<Product> Product { get; set; }
    public DbSet<CustomerContact> CustomerContact { get; set; }
    public DbSet<VendorContact> VendorContact { get; set; }
    public DbSet<Tax> Tax { get; set; }
    public DbSet<SalesOrder> SalesOrder { get; set; }
    public DbSet<SalesOrderItem> SalesOrderItem { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrder { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItem { get; set; }

    public DbSet<Campaign> Campaign { get; set; }
    public DbSet<Budget> Budget { get; set; }
    public DbSet<Expense> Expense { get; set; }
    public DbSet<Lead> Lead { get; set; }
    public DbSet<LeadContact> LeadContact { get; set; }
    public DbSet<LeadActivity> LeadActivity { get; set; }
    public DbSet<SalesTeam> SalesTeam { get; set; }
    public DbSet<SalesRepresentative> SalesRepresentative { get; set; }
    public DbSet<AuditLog> AuditLog { get; set; }
    public DbSet<Notification> Notification { get; set; }
    public DbSet<Payment> Payment { get; set; }
    public DbSet<InvoiceItem> InvoiceItem { get; set; }
    public DbSet<Invoice> Invoice { get; set; }
    public DbSet<QuotationItem> QuotationItem { get; set; }
    public DbSet<Quotation> Quotation { get; set; }
    public DbSet<SupportCaseComment> SupportCaseComment { get; set; }
    public DbSet<SupportCase> SupportCase { get; set; }
    public DbSet<Activity> Activity { get; set; }
    public DbSet<EmailTemplate> EmailTemplate { get; set; }
    public DbSet<EmailLog> EmailLog { get; set; }
    public DbSet<EmailSetting> EmailSetting { get; set; }
    public DbSet<RecordDocument> RecordDocument { get; set; }
    public DbSet<SavedView> SavedView { get; set; }
    public DbSet<LeadStageHistory> LeadStageHistory { get; set; }
    public DbSet<SalesTarget> SalesTarget { get; set; }
    public DbSet<ApiKey> ApiKey { get; set; }
    public DbSet<SalesPolicy> SalesPolicy { get; set; }
    public DbSet<SlaPolicy> SlaPolicy { get; set; }
    public DbSet<SupportCategory> SupportCategory { get; set; }
    public DbSet<KbArticle> KbArticle { get; set; }
    public DbSet<Contract> Contract { get; set; }
    public DbSet<ChannelMessage> ChannelMessage { get; set; }
    public DbSet<MessageTemplate> MessageTemplate { get; set; }
    public DbSet<ChannelOptOut> ChannelOptOut { get; set; }
    public DbSet<CallLog> CallLog { get; set; }
    public DbSet<ChatSession> ChatSession { get; set; }
    public DbSet<IntegrationSetting> IntegrationSetting { get; set; }
    public DbSet<AgentSetting> AgentSetting { get; set; }
    public DbSet<AgentRun> AgentRun { get; set; }
    public DbSet<AgentProposal> AgentProposal { get; set; }
    public DbSet<ChatMessage> ChatMessage { get; set; }
    public DbSet<SignRequest> SignRequest { get; set; }
    public DbSet<EmailMessage> EmailMessage { get; set; }
    public DbSet<CalendarConnection> CalendarConnection { get; set; }
    public DbSet<CalendarEventLink> CalendarEventLink { get; set; }
    public DbSet<ReportSchedule> ReportSchedule { get; set; }
    public DbSet<AiSetting> AiSetting { get; set; }
    public DbSet<AiUsageLog> AiUsageLog { get; set; }
    public DbSet<PortalSession> PortalSession { get; set; }
    public DbSet<ReportDefinition> ReportDefinition { get; set; }
    public DbSet<WorkflowRule> WorkflowRule { get; set; }
    public DbSet<WorkflowRun> WorkflowRun { get; set; }
    public DbSet<PipelineStageSetting> PipelineStageSetting { get; set; }
    public DbSet<WebhookEndpoint> WebhookEndpoint { get; set; }
    public DbSet<WebhookDelivery> WebhookDelivery { get; set; }
    public DbSet<CustomFieldDefinition> CustomFieldDefinition { get; set; }
    public DbSet<CustomFieldValue> CustomFieldValue { get; set; }
}

