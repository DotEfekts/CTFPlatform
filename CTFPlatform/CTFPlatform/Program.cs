using System.Configuration;
using System.Net;
using Auth0.AspNetCore.Authentication;
using CTFPlatform.Components;
using CTFPlatform.Models;
using CTFPlatform.Models.Settings;
using CTFPlatform.Utilities;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<BlazorCtfPlatformContext>(options =>
    options
        .UseLazyLoadingProxies()
        .UseSqlite(
            builder.Configuration.GetConnectionString("BlazorCtfPlatformContext") ?? 
            throw new InvalidOperationException(
                "Connection string 'BlazorCtfPlatformContext' not found.")));

builder.Services.AddSingleton<IStoredSettingsManager<ApplicationSettings>, DbContextStoredSettingsManager<ApplicationSettings>>();

builder.Services.AddSingleton<IStoredSettingsManager<VpnInfo>, DbContextStoredSettingsManager<VpnInfo>>();
builder.Services.AddSingleton<IVpnCertificateManager, AppVpnCertificateManager>();

if (!Directory.Exists(builder.Configuration["UploadDirectory"]))
    throw new ConfigurationErrorsException("Invalid upload storage directory.");
if (!Directory.Exists(builder.Configuration["ManifestDirectory"]))
    throw new ConfigurationErrorsException("Invalid manifest storage directory.");

var uploadDirectory = new DirectoryInfo(builder.Configuration["UploadDirectory"]!);
var manifestDirectory = new DirectoryInfo(builder.Configuration["ManifestDirectory"]!);
if(uploadDirectory.IsSubdirectoryOf(manifestDirectory) || manifestDirectory.IsSubdirectoryOf(uploadDirectory))
    throw new ConfigurationErrorsException("Manifest and upload directory cannot share paths.");

builder.Services
    .AddAuth0WebAppAuthentication(options => {
        options.Domain = builder.Configuration["Auth0:Domain"] ?? throw new NullReferenceException();
        options.ClientId = builder.Configuration["Auth0:ClientId"] ?? throw new NullReferenceException();
        options.OpenIdConnectEvents ??= new OpenIdConnectEvents();
        options.OpenIdConnectEvents.OnTokenValidated += async e =>
        {
            await using var context = e.HttpContext.RequestServices.GetRequiredService<BlazorCtfPlatformContext>();
            if(e.Principal == null)
                e.Fail("No claims principle provided.");
            else
                context.CreateOrUpdateUser(e.Principal);
        };
    });

builder.Services.AddHangfire(configuration =>
{
    configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    configuration.UseSimpleAssemblyNameTypeSerializer();
    configuration.UseRecommendedSerializerSettings();
    configuration.UseInMemoryStorage();
});
builder.Services.AddHangfireServer();

builder.Services.AddCascadingAuthenticationState(); 
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.Services.AddScoped<IInstanceManager, TerraformInstanceManager>();
builder.Services.AddSingleton<ICleanupManager, TerraformCleanupManager>();
builder.Services.AddBrowserTimeProvider();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Auth0ManagementTokenProvider>();

if (builder.Configuration["ForwardNetwork"] != null || builder.Configuration["ForwardProxy"] != null)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = int.TryParse(builder.Configuration["ForwardLimit"], out var limit) ? limit : 2;
        
        if (builder.Configuration["ForwardNetwork"] != null)
        {
            var range = builder.Configuration["ForwardNetwork"]!.Split('/');
            options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse(range[0]), int.Parse(range[1])));
        }
        
        if(builder.Configuration["ForwardProxy"] != null)
            options.KnownProxies.Add(IPAddress.Parse(builder.Configuration["ForwardProxy"]!));
    });
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseMigrationsEndPoint();
    app.UseForwardedHeaders();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/Account/Login", async (HttpContext httpContext, string returnUrl = "/") =>
{
    var authenticationProperties = new LoginAuthenticationPropertiesBuilder()
        .WithRedirectUri(returnUrl)
        .Build();

    await httpContext.ChallengeAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
});

app.MapGet("/Account/Logout", async (HttpContext httpContext, bool login = false) =>
{
    var authenticationProperties = new LogoutAuthenticationPropertiesBuilder()
        .WithRedirectUri(login ? "/Account/Login" : "/")
        .Build();

    await httpContext.SignOutAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CTFPlatform.Client._Imports).Assembly);

app.MapControllers();

app.MapHangfireDashboard("/cron", new DashboardOptions
{
    Authorization = [ new HangfireAuthorization() ]
});

RecurringJob.AddOrUpdate("clear-instances", 
    () => app.Services.GetRequiredService<ICleanupManager>().Clean(), Cron.Minutely);

app.Run();