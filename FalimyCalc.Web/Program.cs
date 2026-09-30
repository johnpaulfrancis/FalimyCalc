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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Application Services ───────────────────────────────────────────────────
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<SyncService>();

var app = builder.Build();

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
