using Application.Common.Repositories;
using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Configurations;
using Infrastructure.SecurityManager.AspNetIdentity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DataAccessManager.EFCore.Contexts;


public class DataContext : IdentityDbContext<ApplicationUser>, IEntityDbSet
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }

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
    public DbSet<CalendarConnection> CalendarConnection { get; set; }
    public DbSet<MailConnection> MailConnection { get; set; }
    public DbSet<EmailMessage> EmailMessage { get; set; }
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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new TokenConfiguration());
        modelBuilder.ApplyConfiguration(new TodoConfiguration());
        modelBuilder.ApplyConfiguration(new TodoItemConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new FileImageConfiguration());
        modelBuilder.ApplyConfiguration(new FileDocumentConfiguration());


        modelBuilder.ApplyConfiguration(new NumberSequenceConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerGroupConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new VendorGroupConfiguration());
        modelBuilder.ApplyConfiguration(new VendorCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new VendorConfiguration());
        modelBuilder.ApplyConfiguration(new UnitMeasureConfiguration());
        modelBuilder.ApplyConfiguration(new ProductGroupConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerContactConfiguration());
        modelBuilder.ApplyConfiguration(new VendorContactConfiguration());
        modelBuilder.ApplyConfiguration(new TaxConfiguration());
        modelBuilder.ApplyConfiguration(new SalesOrderConfiguration());
        modelBuilder.ApplyConfiguration(new SalesOrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseOrderConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseOrderItemConfiguration());


        modelBuilder.ApplyConfiguration(new BudgetConfiguration());
        modelBuilder.ApplyConfiguration(new CampaignConfiguration());
        modelBuilder.ApplyConfiguration(new ExpenseConfiguration());
        modelBuilder.ApplyConfiguration(new LeadConfiguration());
        modelBuilder.ApplyConfiguration(new LeadContactConfiguration());
        modelBuilder.ApplyConfiguration(new LeadActivityConfiguration());
        modelBuilder.ApplyConfiguration(new SalesTeamConfiguration());
        modelBuilder.ApplyConfiguration(new SalesRepresentativeConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceItemConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new QuotationItemConfiguration());
        modelBuilder.ApplyConfiguration(new QuotationConfiguration());
        modelBuilder.ApplyConfiguration(new SupportCaseCommentConfiguration());
        modelBuilder.ApplyConfiguration(new SupportCaseConfiguration());
        modelBuilder.ApplyConfiguration(new ActivityConfiguration());
        modelBuilder.ApplyConfiguration(new EmailTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new EmailLogConfiguration());
        modelBuilder.ApplyConfiguration(new EmailSettingConfiguration());
        modelBuilder.ApplyConfiguration(new RecordDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new SavedViewConfiguration());
        modelBuilder.ApplyConfiguration(new LeadStageHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new SalesTargetConfiguration());
        modelBuilder.ApplyConfiguration(new ApiKeyConfiguration());
        modelBuilder.ApplyConfiguration(new SalesPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new SlaPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new SupportCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new KbArticleConfiguration());
        modelBuilder.ApplyConfiguration(new ContractConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelMessageConfiguration());
        modelBuilder.ApplyConfiguration(new MessageTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelOptOutConfiguration());
        modelBuilder.ApplyConfiguration(new CallLogConfiguration());
        modelBuilder.ApplyConfiguration(new ChatSessionConfiguration());
        modelBuilder.ApplyConfiguration(new IntegrationSettingConfiguration());
        modelBuilder.ApplyConfiguration(new AgentSettingConfiguration());
        modelBuilder.ApplyConfiguration(new AgentRunConfiguration());
        modelBuilder.ApplyConfiguration(new AgentProposalConfiguration());
        modelBuilder.ApplyConfiguration(new ChatMessageConfiguration());
        modelBuilder.ApplyConfiguration(new SignRequestConfiguration());
        modelBuilder.ApplyConfiguration(new CalendarConnectionConfiguration());
        modelBuilder.ApplyConfiguration(new MailConnectionConfiguration());
        modelBuilder.ApplyConfiguration(new EmailMessageConfiguration());
        modelBuilder.ApplyConfiguration(new CalendarEventLinkConfiguration());
        modelBuilder.ApplyConfiguration(new ReportScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new AiSettingConfiguration());
        modelBuilder.ApplyConfiguration(new AiUsageLogConfiguration());
        modelBuilder.ApplyConfiguration(new PortalSessionConfiguration());
        modelBuilder.ApplyConfiguration(new ReportDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowRuleConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowRunConfiguration());
        modelBuilder.ApplyConfiguration(new PipelineStageSettingConfiguration());
        modelBuilder.ApplyConfiguration(new WebhookEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new WebhookDeliveryConfiguration());
        modelBuilder.ApplyConfiguration(new CustomFieldDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new CustomFieldValueConfiguration());

    }

}


