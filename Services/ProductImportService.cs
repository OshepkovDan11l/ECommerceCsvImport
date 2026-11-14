using System.Globalization;
using ECommerceCsvImport.Models;

namespace ECommerceCsvImport.Services;
 
public class ProductImportService : IProductImportService
{ 
    private const string DefaultCurrency = "RUB";
    private const string DefaultCategory = "Без категории";
    private const int MinWarrantyMonths = 0;
    private const int MaxWarrantyMonths = 60;

    private static readonly string[] RequiredColumns =
    {
        "sku", "name", "description", "price", "currency", "category_name",
        "stock", "image_url", "is_active", "brand", "warranty_months", "model_number"
    };
     
    private const string MsgFileNotFound = "Файл не найден";
    private const string MsgNoData = "Данные для импорта отсутствуют";
    private const string MsgBadHeader = "Неверные заголовки CSV";
    private const string MsgBadFieldCount = "Неверное количество полей в строке";
    private const string MsgSkuEmpty = "Поле sku не может быть пустым";
    private const string MsgNameEmpty = "Поле name не может быть пустым";
    private const string MsgPrice = "Поле price должно быть числом больше 0";
    private const string MsgStock = "Остаток на складе должен быть целым числом не меньше 0";
    private const string MsgIsActive = "Поле is_active должно иметь значение true, false, 1 или 0";
    private const string MsgBrand = "Поле brand обязательно для варианта «Электроника»";
    private const string MsgWarranty = "Гарантия должна быть целым числом от 0 до 60 месяцев";
    private const string MsgDuplicate = "Артикул товара уже существует";

    private readonly IProductRepository _repository;

    public ProductImportService(IProductRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
     
    public ImportResult ImportFromCsv(string filePath)
    {
        var result = new ImportResult();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = MsgFileNotFound });
            return result;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            result.Errors.Add(new ImportError
            {
                RowNumber = 0,
                Message = $"Не удалось прочитать файл: {ex.Message}"
            });
            return result;
        }

        return ImportFromLines(lines);
    }
     
    public ImportResult ImportFromLines(IEnumerable<string> lines)
    {
        var result = new ImportResult();
        var allLines = lines?.ToList() ?? new List<string>();
         
        int headerIndex = allLines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        if (headerIndex < 0)
        {
            AddError(result, 0, MsgNoData);
            return result;
        }

        int headerRow = headerIndex + 1;
        var headerFields = CsvLineParser.Parse(allLines[headerIndex].TrimStart('﻿'));

        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerFields.Count; i++)
        {
            var name = headerFields[i].Trim();
            if (!columnMap.ContainsKey(name))
                columnMap[name] = i;
        }

        if (RequiredColumns.Any(c => !columnMap.ContainsKey(c)))
        {
            AddError(result, headerRow, MsgBadHeader);
            return result;
        }

        int expectedFieldCount = headerFields.Count;
        var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = headerIndex + 1; index < allLines.Count; index++)
        {
            var line = allLines[index];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            result.TotalRows++;
            int rowNumber = index + 1;

            var values = CsvLineParser.Parse(line);
            if (values.Count != expectedFieldCount)
            {
                AddError(result, rowNumber, MsgBadFieldCount);
                continue;
            }

            string GetValue(string columnName) =>
                columnMap.TryGetValue(columnName, out int idx) && idx < values.Count
                    ? values[idx]
                    : string.Empty;

            var rowErrors = new List<ImportError>();

            string sku = GetValue("sku").Trim();
            if (string.IsNullOrWhiteSpace(sku))
                rowErrors.Add(NewError(rowNumber, MsgSkuEmpty));

            string name = GetValue("name").Trim();
            if (string.IsNullOrWhiteSpace(name))
                rowErrors.Add(NewError(rowNumber, MsgNameEmpty));

            if (!TryParseDecimal(GetValue("price"), out decimal price) || price <= 0)
                rowErrors.Add(NewError(rowNumber, MsgPrice));

            if (!TryParseInt(GetValue("stock"), out int stock) || stock < 0)
                rowErrors.Add(NewError(rowNumber, MsgStock));

            if (!TryParseBool(GetValue("is_active"), out bool isActive))
                rowErrors.Add(NewError(rowNumber, MsgIsActive));

            string brand = GetValue("brand").Trim();
            if (string.IsNullOrWhiteSpace(brand))
                rowErrors.Add(NewError(rowNumber, MsgBrand));

            if (!TryParseInt(GetValue("warranty_months"), out int warranty)
                || warranty < MinWarrantyMonths || warranty > MaxWarrantyMonths)
                rowErrors.Add(NewError(rowNumber, MsgWarranty));

            if (rowErrors.Count > 0)
            {
                result.Errors.AddRange(rowErrors);
                continue;
            }
 
            if (_repository.GetBySku(sku) != null || importedSkus.Contains(sku))
            {
                AddError(result, rowNumber, MsgDuplicate);
                continue;
            }

            _repository.Add(new Product
            {
                Sku = sku,
                Name = name,
                Description = GetValue("description").Trim(),
                Price = price,
                Currency = NormalizeCurrency(GetValue("currency")),
                CategoryName = NormalizeCategory(GetValue("category_name")),
                StockQuantity = stock,
                ImageUrl = GetValue("image_url").Trim(),
                IsActive = isActive,
                Brand = brand,
                WarrantyMonths = warranty,
                ModelNumber = GetValue("model_number").Trim()
            });
            importedSkus.Add(sku);
            result.ImportedCount++;
        }

        if (result.TotalRows == 0 && result.Errors.Count == 0)
            AddError(result, 0, MsgNoData);

        return result;
    }

    private static ImportError NewError(int rowNumber, string message) =>
        new() { RowNumber = rowNumber, Message = message };

    private static void AddError(ImportResult result, int rowNumber, string message) =>
        result.Errors.Add(NewError(rowNumber, message));

    private static string NormalizeCurrency(string value) =>
        string.IsNullOrWhiteSpace(value) ? DefaultCurrency : value.Trim().ToUpperInvariant();

    private static string NormalizeCategory(string value) =>
        string.IsNullOrWhiteSpace(value) ? DefaultCategory : value.Trim();
     
    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value.Trim().Replace(',', '.'),
            NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseBool(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
                result = true;
                return true;
            case "false":
            case "0":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }
}
