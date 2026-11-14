using ECommerceCsvImport.Services;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class CsvLineParserTests
{
    [Fact]
    public void Parse_SimpleLine_Returns12Fields()
    {
        var fields = CsvLineParser.Parse(
            "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15");

        Assert.Equal(12, fields.Count);
        Assert.Equal("1001", fields[0]);
    }

    [Fact]
    public void Parse_EmptyFields_ArePreserved()
    {
        var fields = CsvLineParser.Parse(
            "1011,Блок питания Corsair CV550,,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550");

        Assert.Equal(12, fields.Count);
        Assert.Equal(string.Empty, fields[2]);
        Assert.Equal(string.Empty, fields[7]);
    }

    [Fact]
    public void Parse_CommaInsideQuotes_DoesNotSplit()
    {
        var fields = CsvLineParser.Parse(
            "1012,\"Ноутбук, игровой\",Тестовая строка,59990,RUB,Компьютеры,1,/images/notebook.jpg,true,ASUS,36,VivoBook");

        Assert.Equal(12, fields.Count);
        Assert.Equal("Ноутбук, игровой", fields[1]);
    }

    [Fact]
    public void Parse_EscapedQuotes_AreUnescaped()
    {
        var fields = CsvLineParser.Parse(
            "1013,\"Название \"\"Special\"\"\",Описание,100,RUB,Категория,5,/img.jpg,true,Brand,12,Model");

        Assert.Equal(12, fields.Count);
        Assert.Equal("Название \"Special\"", fields[1]);
    }

    [Fact]
    public void Parse_TrimsFields()
    {
        var fields = CsvLineParser.Parse("  a , b ,c  ");

        Assert.Equal(new[] { "a", "b", "c" }, fields);
    }

    [Fact]
    public void Parse_EmptyLine_ReturnsSingleEmptyField()
    {
        var fields = CsvLineParser.Parse(string.Empty);

        Assert.Single(fields);
        Assert.Equal(string.Empty, fields[0]);
    }
}
