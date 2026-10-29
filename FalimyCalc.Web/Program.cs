using FalimyCalc.Data;
using FalimyCalc.Data.Services;
using FalimyCalc.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor ─────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── API Controllers (used by the mobile sync endpoints) ────────────────────
builder.Services.AddControllers();

// ── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// ── Application Services ───────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<AiAdvisorService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<FalimyCalc.Web.Services.ExportService>();
builder.Services.AddScoped<FalimyCalc.Web.Services.UserSessionService>();

var app = builder.Build();

// ── Database Auto-Migration & Schema Verification ──────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = dbFactory.CreateDbContext();
    db.Database.Migrate();

    db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'IsJoint')
BEGIN
    ALTER TABLE BankAccounts ADD IsJoint BIT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'JointMemberGlobalIds')
BEGIN
    ALTER TABLE BankAccounts ADD JointMemberGlobalIds NVARCHAR(1000) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FinancialGoals')
BEGIN
    CREATE TABLE FinancialGoals (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        GlobalId UNIQUEIDENTIFIER NOT NULL UNIQUE,
        RowVersion BIGINT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeviceId NVARCHAR(100) NULL,
        Name NVARCHAR(150) NOT NULL,
        TargetAmount DECIMAL(18,2) NOT NULL,
        CurrentAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TargetDate DATETIME2 NULL,
        Colour NVARCHAR(30) NULL,
        Icon NVARCHAR(50) NULL,
        Category NVARCHAR(100) NULL,
        MemberGlobalId UNIQUEIDENTIFIER NULL,
        BankAccountId INT NULL,
        AccountGlobalId UNIQUEIDENTIFIER NULL,
        Status NVARCHAR(30) NOT NULL DEFAULT 'Active',
        Note NVARCHAR(1000) NULL
    );
    CREATE INDEX IX_FinancialGoals_MemberGlobalId ON FinancialGoals(MemberGlobalId);
    CREATE INDEX IX_FinancialGoals_AccountGlobalId ON FinancialGoals(AccountGlobalId);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GoalContributions')
BEGIN
    CREATE TABLE GoalContributions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        GlobalId UNIQUEIDENTIFIER NOT NULL UNIQUE,
        RowVersion BIGINT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeviceId NVARCHAR(100) NULL,
        GoalGlobalId UNIQUEIDENTIFIER NOT NULL,
        FinancialGoalId INT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        TransactionDate DATETIME2 NOT NULL,
        Type NVARCHAR(20) NOT NULL DEFAULT 'Deposit',
        Note NVARCHAR(500) NULL,
        AccountGlobalId UNIQUEIDENTIFIER NULL,
        AccountName NVARCHAR(100) NULL
    );
    CREATE INDEX IX_GoalContributions_GoalGlobalId ON GoalContributions(GoalGlobalId);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecurringTransactions')
BEGIN
    CREATE TABLE RecurringTransactions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        GlobalId UNIQUEIDENTIFIER NOT NULL UNIQUE,
        RowVersion BIGINT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeviceId NVARCHAR(100) NULL,
        Name NVARCHAR(150) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Type NVARCHAR(20) NOT NULL DEFAULT 'Expense',
        Frequency NVARCHAR(30) NOT NULL DEFAULT 'Monthly',
        NextDueDate DATETIME2 NOT NULL,
        LastPaidDate DATETIME2 NULL,
        CategoryId INT NULL,
        CategoryGlobalId UNIQUEIDENTIFIER NULL,
        BankAccountId INT NULL,
        AccountGlobalId UNIQUEIDENTIFIER NULL,
        MemberGlobalId UNIQUEIDENTIFIER NULL,
        Icon NVARCHAR(50) NULL,
        Colour NVARCHAR(30) NULL,
        Status NVARCHAR(30) NOT NULL DEFAULT 'Active',
        AutoLog BIT NOT NULL DEFAULT 0,
        Note NVARCHAR(1000) NULL,
        BillingCycleDay INT NULL
    );
    CREATE INDEX IX_RecurringTransactions_MemberGlobalId ON RecurringTransactions(MemberGlobalId);
    CREATE INDEX IX_RecurringTransactions_NextDueDate ON RecurringTransactions(NextDueDate);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Loans')
BEGIN
    CREATE TABLE Loans (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        GlobalId UNIQUEIDENTIFIER NOT NULL UNIQUE,
        RowVersion BIGINT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeviceId NVARCHAR(100) NULL,
        Name NVARCHAR(150) NOT NULL,
        Lender NVARCHAR(150) NULL,
        LoanType NVARCHAR(50) NOT NULL DEFAULT 'Personal Loan',
        PrincipalAmount DECIMAL(18,2) NOT NULL,
        InterestRate DECIMAL(8,3) NOT NULL,
        TenureMonths INT NOT NULL,
        EmiAmount DECIMAL(18,2) NOT NULL,
        OutstandingBalance DECIMAL(18,2) NOT NULL,
        TotalPaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        EmisPaid INT NOT NULL DEFAULT 0,
        StartDate DATETIME2 NOT NULL,
        NextDueDate DATETIME2 NOT NULL,
        Status NVARCHAR(30) NOT NULL DEFAULT 'Active',
        Colour NVARCHAR(30) NULL,
        Icon NVARCHAR(50) NULL,
        Note NVARCHAR(1000) NULL,
        MemberGlobalId UNIQUEIDENTIFIER NULL,
        AccountGlobalId UNIQUEIDENTIFIER NULL,
        BankAccountId INT NULL
    );
    CREATE INDEX IX_Loans_MemberGlobalId ON Loans(MemberGlobalId);
    CREATE INDEX IX_Loans_AccountGlobalId ON Loans(AccountGlobalId);
    CREATE INDEX IX_Loans_NextDueDate ON Loans(NextDueDate);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoanPayments')
BEGIN
    CREATE TABLE LoanPayments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        GlobalId UNIQUEIDENTIFIER NOT NULL UNIQUE,
        RowVersion BIGINT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL,
        ModifiedAt DATETIME2 NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeviceId NVARCHAR(100) NULL,
        LoanGlobalId UNIQUEIDENTIFIER NOT NULL,
        LoanId INT NULL,
        PaymentDate DATETIME2 NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PrincipalComponent DECIMAL(18,2) NOT NULL DEFAULT 0,
        InterestComponent DECIMAL(18,2) NOT NULL DEFAULT 0,
        PaymentType NVARCHAR(30) NOT NULL DEFAULT 'EMI',
        AccountGlobalId UNIQUEIDENTIFIER NULL,
        AccountName NVARCHAR(100) NULL,
        Note NVARCHAR(500) NULL
    );
    CREATE INDEX IX_LoanPayments_LoanGlobalId ON LoanPayments(LoanGlobalId);
    CREATE INDEX IX_LoanPayments_PaymentDate ON LoanPayments(PaymentDate);
END
");
}

// ── Middleware pipeline ────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

// ── Endpoints ──────────────────────────────────────────────────────────────
app.MapControllers();                          // API routes (sync endpoints)
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
