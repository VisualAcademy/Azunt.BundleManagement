using Azunt.BundleManagement;
using Azunt.Web.Components;
using Azunt.Web.Components.Pages.Bundles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Local verification uses a shared EF Core In-Memory database.
builder.Services.AddDependencyInjectionContainerForBundleApp(
    BundleServicesRegistrationExtensions.RepositoryMode.EfCoreInMemory);

var app = builder.Build();

await BundleSeedData.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
