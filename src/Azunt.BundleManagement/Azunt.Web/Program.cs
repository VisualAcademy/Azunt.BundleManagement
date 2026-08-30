using Azunt.BundleManagement;
using Azunt.Web.Components;
using Azunt.Web.Components.Pages.Bundles;

var builder = WebApplication.CreateBuilder(args);

// MVC is enabled so the same Bundle CRUD component can be hosted from
// the DotNetNote Area at /DotNetNote/Bundles/.
builder.Services.AddControllersWithViews();

// Existing Blazor Web App support for /Bundles.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Component Tag Helper support for interactive Razor components embedded
// in MVC views. A dedicated hub path is mapped below to avoid colliding
// with the Interactive Server endpoints used by the Blazor Web App.
builder.Services.AddServerSideBlazor();

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

// Attribute-routed API controllers.
app.MapControllers();

// Conventional MVC Area route. This maps /DotNetNote/Bundles/
// to Areas/DotNetNote/Controllers/BundlesController.Index.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Existing Blazor Web App routes such as /Bundles remain available.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// MVC-hosted interactive Razor components use a separate circuit endpoint.
// Keeping this path distinct from the Blazor Web App endpoint prevents
// ambiguous hub endpoint matches when both hosting models coexist.
app.MapBlazorHub("/dotnetnote-blazor");

// .NET 10 static web assets, including framework scripts.
app.MapStaticAssets();

app.Run();
