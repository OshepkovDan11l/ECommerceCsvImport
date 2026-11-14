using ECommerceCsvImport.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
 
builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<IProductImportService, ProductImportService>();

var app = builder.Build();
 
var seedPath = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "csv", "sample_products.csv");
if (File.Exists(seedPath))
{
    var seedResult = app.Services.GetRequiredService<IProductImportService>().ImportFromCsv(seedPath);
    app.Logger.LogInformation("Каталог загружен: {Imported} товаров, ошибок: {Errors}",
        seedResult.ImportedCount, seedResult.ErrorCount);
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Products}/{action=Index}/{id?}");

app.Run(); 
public partial class Program { }
