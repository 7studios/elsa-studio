using Elsa.Studio.Counter.Extensions;
using Elsa.Studio.Dashboard.Extensions;
using Elsa.Studio.Host.Server.HostedServices;
using Elsa.Studio.Shell.Extensions;
using Elsa.Studio.Workflows.Extensions;
using Elsa.Studio.Designer.Extensions;
using Elsa.Studio.Environments.Extensions;

// Build the host.
var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Register the services.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    // Register the root components.
    options.RootComponents.RegisterCustomElements();
});

builder.Services.AddShell();
builder.Services.AddEnvironments(options => configuration.GetSection("Environments").Bind(options));
builder.Services.AddDashboardModule();
builder.Services.AddWorkflowsModule();
builder.Services.AddCounterModule();

// Register the hosted services.
builder.Services.AddHostedService<RunStartupTasksHostedService>();

// Build the application.
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Run the application.
app.Run();