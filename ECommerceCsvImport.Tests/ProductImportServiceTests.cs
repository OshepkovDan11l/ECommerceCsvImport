using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class ProductImportServiceTests
{
    private const string Header =
        "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";

    private const string Laptop =
        "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15";

    private const string Mouse =
        "1002,Мышь Logitech M185,Беспроводная мышь с USB-приемником,1290,RUB,Периферия,10,/images/mouse.jpg,true,Logitech,12,M185";

    private static string TestData(string name) =>
        Path.Combine(AppContext.BaseDirectory, "testdata", name);

    private static (InMemoryProductRepository repo, ProductImportService service) Create()
    {
        var repo = new InMemoryProductRepository();
        return (repo, new ProductImportService(repo));
    }

    private static void AssertSingleRowError(ImportResult result, string message)
    {
        Assert.Equal(0, result.ImportedCount);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains(message));
    }

    // ---------- Тесты 1–2: успешный импорт ----------

    [Fact]
    public void Import_SingleValidRow_Succeeds()
    {
        var (repo, service) = Create();

        var result = service.ImportFromLines(new[] { Header, Laptop });

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);

        var product = repo.GetBySku("1001");
        Assert.NotNull(product);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product!.Name);
        Assert.Equal(45990m, product.Price);
        Assert.Equal("Lenovo", product.Brand);
        Assert.Equal(24, product.WarrantyMonths);
        Assert.Equal("IdeaPad3-15", product.ModelNumber);
    }

    [Fact]
    public void Import_MultipleValidRows_Succeeds()
    {
        var (repo, service) = Create();

        var result = service.ImportFromLines(new[] { Header, Laptop, Mouse });

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(2, repo.Count);
    }

    // ---------- Тесты 3–6: файл и заголовок ----------

    [Fact]
    public void ImportFromCsv_FileNotFound_ReturnsError()
    {
        var (_, service) = Create();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");

        var result = service.ImportFromCsv(path);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Файл не найден", result.Errors[0].Message);
    }

    [Fact]
    public void ImportFromCsv_EmptyFile_ReturnsNoDataError()
    {
        var (_, service) = Create();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(path, string.Empty);

        try
        {
            var result = service.ImportFromCsv(path);

            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_HeaderOnly_ReturnsNoDataError()
    {
        var (_, service) = Create();

        var result = service.ImportFromLines(new[] { Header });

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);
    }

    [Fact]
    public void Import_InvalidHeader_ReturnsError()
    {
        var (_, service) = Create();

        var result = service.ImportFromLines(new[]
        {
            "sku,name,description,price,currency,category_name,stock,image_url,is_active"
        });

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Неверные заголовки CSV"));
    }

    // ---------- Тест 7: количество полей ----------

    [Fact]
    public void Import_WrongFieldCount_ReturnsError()
    {
        var (_, service) = Create();

        var result = service.ImportFromLines(new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3" });

        Assert.Equal(1, result.TotalRows);
        AssertSingleRowError(result, "Неверное количество полей в строке");
    }

    // ---------- Тесты 8–16: валидация полей ----------

    [Fact]
    public void Import_EmptySku_ReturnsError()
    {
        var (_, service) = Create();
        var line = ",Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15";

        AssertSingleRowError(service.ImportFromLines(new[] { Header, line }),
            "Поле sku не может быть пустым");
    }

    [Fact]
    public void Import_EmptyName_ReturnsError()
    {
        var (_, service) = Create();
        var line = "1001,,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15";

        AssertSingleRowError(service.ImportFromLines(new[] { Header, line }),
            "Поле name не может быть пустым");
    }

    [Theory]
    [InlineData("-500")]
    [InlineData("abc")]
    [InlineData("0")]
    public void Import_InvalidPrice_ReturnsError(string price)
    {
        var (_, service) = Create();
        var line = $"1003,Клавиатура A4Tech Bloody V3M,Механическая игровая клавиатура,{price},RUB,Периферия,7,/images/keyboard.jpg,true,A4Tech,24,V3M";

        AssertSingleRowError(service.ImportFromLines(new[] { Header, line }),
            "Поле price должно быть числом больше 0");
    }

    [Fact]
    public void Import_NegativeStock_ReturnsError()
    {
        var (_, service) = Create();
        var line = "1004,Монитор Dell S2722QC,Игровой монитор 27 дюймов,29990,RUB,Компьютеры,-5,/images/monitor.jpg,true,Dell,24,S2722QC";

        AssertSingleRowError(service.ImportFromLines(new[] { Header, line }),
            "Остаток на складе должен быть целым числом не меньше 0");
    }

    [Fact]
    public void Import_InvalidIsActive_ReturnsError()
    {
        var (_, service) = Create();
        var line = "1007,Webcam Logitech C920,Веб-камера Full HD,4990,RUB,Периферия,6,/images/webcam.jpg,yes,Logitech,24,C920";

        AssertSingleRowError(service.ImportFromLines(new[] { Header, line }),
            "Поле is_active должно иметь значение true, false, 1 или 0");
    }

    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("False", false)]
    [InlineData("0", false)]
    public void Import_ValidIsActiveValues_Accepted(string value, bool expected)
    {
        var (repo, service) = Create();
        var line = $"1007,Webcam Logitech C920,Веб-камера,4990,RUB,Периферия,6,/images/webcam.jpg,{value},Logitech,24,C920";

        var result = service.ImportFromLines(new[] { Header, line });

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(expected, repo.GetBySku("1007")!.IsActive);
    }

    [Fact]
    public void Import_MissingBrand_ReturnsError()
    {
        var (_, service) = Create();
        var line = "1005,Видеокарта NVIDIA GeForce RTX 3060,,24990,RUB,Компьютеры,4,/images/gpu.jpg,true,,36,RTX3060";

        AssertSingleRowError(service.ImportFromLines(new[] { Header, line }),
            "Поле brand обязательно для варианта «Электроника»");
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("61")]
    [InlineData("abc")]
    public void Import_WarrantyOutOfRange_ReturnsError(string warranty)
    {
        var (_, service) = Create();
        var line = $"1006,Накопитель SSD Samsung 870 EVO 1TB,Внутренний твердотельный накопитель,5990,RUB,Компьютеры,12,/images/ssd.jpg,true,Samsung,{warranty},870EVO-1T";

        AssertSingleRowError(service.ImportFromLines(new[] { Header, line }),
            "Гарантия должна быть целым числом от 0 до 60 месяцев");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("60")]
    public void Import_WarrantyBoundaries_Accepted(string warranty)
    {
        var (_, service) = Create();
        var line = $"1006,Накопитель SSD,Описание,5990,RUB,Компьютеры,12,/images/ssd.jpg,true,Samsung,{warranty},870EVO-1T";

        Assert.Equal(1, service.ImportFromLines(new[] { Header, line }).ImportedCount);
    }

    // ---------- Тесты 17–18: дубликаты ----------

    [Fact]
    public void Import_DuplicateSkuInsideFile_SecondRowSkipped()
    {
        var (repo, service) = Create();
        var duplicate = "1001,Ноутбук ASUS VivoBook,Другой ноутбук,52990,RUB,Компьютеры,7,/images/asus.jpg,true,ASUS,24,VivoBook";

        var result = service.ImportFromLines(new[] { Header, Laptop, duplicate });

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Артикул товара уже существует", result.Errors[0].Message);
        Assert.Equal(3, result.Errors[0].RowNumber);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", repo.GetBySku("1001")!.Name);
    }

    [Fact]
    public void Import_DuplicateSkuWithRepository_NewProductNotAdded()
    {
        var (repo, service) = Create();
        repo.Add(new Product { Sku = "1001", Name = "Старый товар", Brand = "X" });
        var line = "1001,Ноутбук ASUS VivoBook,Другой ноутбук,52990,RUB,Компьютеры,7,/images/asus.jpg,true,ASUS,24,VivoBook";

        var result = service.ImportFromLines(new[] { Header, line });

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, repo.Count);
        Assert.Contains(result.Errors, e => e.Message.Contains("Артикул товара уже существует"));
    }

    [Fact]
    public void Import_SameFileTwice_SecondRunReportsDuplicates()
    {
        var (repo, service) = Create();
        var lines = new[] { Header, Laptop, Mouse };

        service.ImportFromLines(lines);
        var second = service.ImportFromLines(lines);

        Assert.Equal(0, second.ImportedCount);
        Assert.Equal(2, second.ErrorCount);
        Assert.Equal(2, repo.Count);
    }

    // ---------- Тест 19: несколько ошибок в одной строке ----------

    [Fact]
    public void Import_MultipleErrorsInOneRow_ReportsAll()
    {
        var (repo, service) = Create();
        // 12 полей: пустые sku, name, brand, model_number; плохие price, stock, is_active, warranty.
        var line = ",,Некорректная строка,-10,RUB,Компьютеры,-5,/images/x.jpg,yes,,61,";

        var result = service.ImportFromLines(new[] { Header, line });

        Assert.Equal(0, result.ImportedCount);
        Assert.True(result.ErrorCount >= 4);
        Assert.Equal(0, repo.Count);
    }

    // ---------- Тесты 20–23: необязательные поля и нормализация ----------

    [Fact]
    public void Import_EmptyOptionalFields_Allowed()
    {
        var (repo, service) = Create();
        var line = "1011,Блок питания Corsair CV550,,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550";

        var result = service.ImportFromLines(new[] { Header, line });

        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);
        var product = repo.GetBySku("1011")!;
        Assert.Equal(string.Empty, product.Description);
        Assert.Equal(string.Empty, product.ImageUrl);
    }

    [Fact]
    public void Import_EmptyCurrency_ReplacedWithRub()
    {
        var (repo, service) = Create();
        var line = "1011,Блок питания Corsair CV550,,4990,,Компьютеры,9,,true,Corsair,60,CV550";

        service.ImportFromLines(new[] { Header, line });

        Assert.Equal("RUB", repo.GetBySku("1011")!.Currency);
    }

    [Fact]
    public void Import_EmptyCategory_ReplacedWithDefault()
    {
        var (repo, service) = Create();
        var line = "1011,Блок питания Corsair CV550,,4990,RUB,,9,,true,Corsair,60,CV550";

        service.ImportFromLines(new[] { Header, line });

        Assert.Equal("Без категории", repo.GetBySku("1011")!.CategoryName);
    }

    [Fact]
    public void Import_Cyrillic_PreservedCorrectly()
    {
        var (repo, service) = Create();

        service.ImportFromLines(new[] { Header, Laptop });

        var product = repo.GetBySku("1001")!;
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
        Assert.Equal("Бюджетный ноутбук для работы и учебы", product.Description);
        Assert.Equal("Компьютеры", product.CategoryName);
    }

    // ---------- Дополнительно: пустые строки, номера строк, кавычки ----------

    [Fact]
    public void Import_BlankLines_SkippedAndNotCounted()
    {
        var (_, service) = Create();

        var result = service.ImportFromLines(new[] { Header, "", Laptop, "   ", Mouse });

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
    }

    [Fact]
    public void Import_ErrorRowNumber_IsPhysicalLineNumber()
    {
        var (_, service) = Create();
        var bad = "1003,Клавиатура,Описание,-500,RUB,Периферия,7,/i.jpg,true,A4Tech,24,V3M";

        var result = service.ImportFromLines(new[] { Header, bad });

        Assert.Equal(2, result.Errors[0].RowNumber);
    }

    [Fact]
    public void Import_QuotedFieldWithComma_Imported()
    {
        var (repo, service) = Create();
        var line = "1012,\"Ноутбук, игровой\",Тест,59990,RUB,Компьютеры,1,/i.jpg,true,ASUS,36,VivoBook";

        var result = service.ImportFromLines(new[] { Header, line });

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("Ноутбук, игровой", repo.GetBySku("1012")!.Name);
    }

    // ---------- Файловые тесты на CSV из testdata ----------

    [Fact]
    public void ImportFromCsv_ValidElectronics_Imports2()
    {
        var (_, service) = Create();

        var result = service.ImportFromCsv(TestData("valid_electronics.csv"));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void ImportFromCsv_InvalidPrice_ErrorOnRow2()
    {
        var (_, service) = Create();

        var result = service.ImportFromCsv(TestData("invalid_price.csv"));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Contains("Поле price должно быть числом больше 0", result.Errors[0].Message);
    }

    [Fact]
    public void ImportFromCsv_DuplicateSku_KeepsFirst()
    {
        var (repo, service) = Create();

        var result = service.ImportFromCsv(TestData("duplicate_sku.csv"));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", repo.GetBySku("1001")!.Name);
    }

    [Theory]
    [InlineData("missing_brand.csv", "Поле brand обязательно для варианта «Электроника»")]
    [InlineData("warranty_out_of_range.csv", "Гарантия должна быть целым числом от 0 до 60 месяцев")]
    [InlineData("bad_active.csv", "Поле is_active должно иметь значение true, false, 1 или 0")]
    public void ImportFromCsv_InvalidFiles_ReportExpectedError(string file, string message)
    {
        var (_, service) = Create();

        var result = service.ImportFromCsv(TestData(file));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(message, result.Errors[0].Message);
    }

    [Fact]
    public void ImportFromCsv_EmptyTestDataFile_ReportsNoData()
    {
        var (_, service) = Create();

        var result = service.ImportFromCsv(TestData("empty_file.csv"));

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);
    }
}
