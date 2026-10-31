using CloudBerry.App.Services;

namespace CloudBerry.App.Tests;

public sealed class PdfRenderBudgetTests
{
    [Theory]
    [InlineData(600, 900)]
    [InlineData(1200, 600)]
    [InlineData(50000, 500)]
    [InlineData(100, 10000)]
    public void RenderBudgetPreservesPageShapeAndBoundsAllocation(double width, double height)
    {
        var size = PdfSpreadRenderer.CalculateRenderSize(width, height, 2400);
        Assert.InRange(size.Width, 1, 4096);
        Assert.InRange(size.Height, 1, 2400);
        Assert.InRange((long)size.Width * size.Height, 1, 8_010_000);
        Assert.InRange(Math.Abs((double)size.Width / size.Height - width / height) / (width / height), 0, 0.02);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, -1)]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.PositiveInfinity)]
    public void RejectsInvalidPageGeometry(double width, double height) =>
        Assert.Throws<System.IO.InvalidDataException>(() => PdfSpreadRenderer.CalculateRenderSize(width, height, 1200));
}
