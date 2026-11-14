using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class RepositoryTests
{
    private static Product NewProduct(string sku = "1001") => new()
    {
        Sku = sku,
        Name = "Ноутбук Lenovo IdeaPad 3",
        Price = 45990m,
        Brand = "Lenovo"
    };

    [Fact]
    public void Add_AddsProduct_GetAllContainsIt()
    {
        var repository = new InMemoryProductRepository();

        repository.Add(NewProduct());

        var all = repository.GetAll();
        Assert.Single(all);
        Assert.Equal("1001", all[0].Sku);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public void GetBySku_ExistingSku_ReturnsProduct()
    {
        var repository = new InMemoryProductRepository();
        repository.Add(NewProduct());

        var found = repository.GetBySku("1001");

        Assert.NotNull(found);
        Assert.Equal("1001", found!.Sku);
    }

    [Theory]
    [InlineData(" 1001 ")]
    [InlineData("1001")]
    public void GetBySku_IgnoresSpaces(string query)
    {
        var repository = new InMemoryProductRepository();
        repository.Add(NewProduct());

        Assert.NotNull(repository.GetBySku(query));
    }

    [Fact]
    public void GetBySku_IgnoresCase()
    {
        var repository = new InMemoryProductRepository();
        repository.Add(NewProduct("ab-1"));

        Assert.NotNull(repository.GetBySku("AB-1"));
    }

    [Fact]
    public void EmptyRepository_HasNoProducts()
    {
        var repository = new InMemoryProductRepository();

        Assert.Equal(0, repository.Count);
        Assert.Empty(repository.GetAll());
        Assert.Null(repository.GetBySku("1001"));
    }

    [Fact]
    public void Add_Null_Throws()
    {
        var repository = new InMemoryProductRepository();

        Assert.Throws<ArgumentNullException>(() => repository.Add(null!));
    }

    [Fact]
    public void GetAll_ReturnsSnapshot()
    {
        var repository = new InMemoryProductRepository();
        repository.Add(NewProduct());

        var snapshot = repository.GetAll();
        repository.Add(NewProduct("1002"));

        Assert.Single(snapshot);
        Assert.Equal(2, repository.Count);
    }

    [Fact]
    public void Clear_RemovesAllProducts()
    {
        var repository = new InMemoryProductRepository();
        repository.Add(NewProduct());

        repository.Clear();

        Assert.Equal(0, repository.Count);
    }
}
