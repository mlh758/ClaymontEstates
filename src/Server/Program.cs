using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Server.Components;
using Server.Components.Account;
using Server.Data;
using Polly;
using Radzen;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<BulkEmailService>();
builder.Services.AddScoped<EmergencyContactService>();
builder.Services.AddSingleton<HtmlSanitizationService>();
builder.Services.AddSingleton<EmailOutboxService>();
builder.Services.AddResiliencePipeline("email-smtp", builder =>
{
    builder.AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(5),
        MaxDelay = TimeSpan.FromMinutes(1),
    });
});
builder.Services.AddHostedService<EmailOutboxBackgroundService>();
builder.Services.AddHostedService<BulkEmailBackgroundService>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Roles.OfficerPolicy, policy =>
        policy.RequireRole(Roles.OfficerRoles));

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var emailConfig = builder.Configuration.GetSection("Email");
var fluentEmail = builder.Services
    .AddFluentEmail(emailConfig["FromAddress"], emailConfig["FromName"]);

if (builder.Environment.IsDevelopment())
{
    fluentEmail.AddSmtpSender(emailConfig["SmtpHost"]!, int.Parse(emailConfig["SmtpPort"]!));
}
else
{
    fluentEmail.AddSmtpSender(new System.Net.Mail.SmtpClient(emailConfig["SmtpHost"]!)
    {
        Port = int.Parse(emailConfig["SmtpPort"]!),
        Credentials = new System.Net.NetworkCredential(
            emailConfig["SmtpUsername"], emailConfig["SmtpPassword"]),
        EnableSsl = true,
        Timeout = 5000
    });
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2
            }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapGet("/api/documents/{id:int}/download", async (int id, DocumentService docService) =>
{
    var document = await docService.GetByIdAsync(id);
    if (document is null)
        return Results.NotFound();

    var path = docService.GetPhysicalPath(document);
    if (path is null)
        return Results.NotFound();

    return Results.File(path, document.ContentType, document.FileName);
}).RequireAuthorization();

// Apply migrations and seed roles on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var userService = scope.ServiceProvider.GetRequiredService<UserService>();
    await userService.EnsureRolesAsync();
}

app.Run();
