using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceCsvImport.Controllers;


public class ProductsController : Controller
{
    private const string DefaultCsvPath = "/csv/new_products.csv";

    private readonly IProductRepository _repository;
    private readonly IProductImportService _importService;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        IProductRepository repository,
        IProductImportService importService,
        IWebHostEnvironment environment)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }


    [HttpGet]
    public IActionResult Index()
    {
        return View(new ProductsIndexViewModel { Products = _repository.GetAll() });
    }
 
    [HttpGet]
    public IActionResult Import()
    {
        return View(new ImportViewModel { FilePath = DefaultCsvPath });
    }
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Import(string filePath)
    {
        var resolvedPath = ResolveFilePath(filePath);

        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            var emptyPathResult = new ImportResult();
            emptyPathResult.Errors.Add(new ImportError
            {
                RowNumber = 0,
                Message = "Укажите путь к CSV-файлу"
            });
            return View("Import", new ImportViewModel
            {
                FilePath = filePath ?? string.Empty,
                Result = emptyPathResult
            });
        }

        var result = _importService.ImportFromCsv(resolvedPath);
        return View("Import", new ImportViewModel
        {
            FilePath = filePath ?? string.Empty,
            Result = result
        });
    }
 
    private string? ResolveFilePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        if (Path.IsPathRooted(trimmed) && !trimmed.StartsWith('/') && !trimmed.StartsWith('\\'))
            return trimmed;

        trimmed = trimmed.TrimStart('/', '\\');
        return Path.Combine(_environment.WebRootPath ?? string.Empty, trimmed);
    }
}
