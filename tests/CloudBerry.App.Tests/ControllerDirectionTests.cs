using System.Windows.Input;
using CloudBerry.App.Services;

namespace CloudBerry.App.Tests;

public sealed class ControllerDirectionTests
{
    [Theory]
    [InlineData(1, FocusNavigationDirection.Up)]
    [InlineData(2, FocusNavigationDirection.Down)]
    [InlineData(4, FocusNavigationDirection.Left)]
    [InlineData(8, FocusNavigationDirection.Right)]
    public void DpadPressesAndHoldsRepeat(ushort button, FocusNavigationDirection expected)
    {
        var input = new ControllerDirectionRepeater();
        Assert.Equal(expected, input.Read(button, 0, 0, 0));
        Assert.Null(input.Read(button, 0, 0, 100));
        Assert.Equal(expected, input.Read(button, 0, 0, 320));
        Assert.Null(input.Read(button, 0, 0, 400));
        Assert.Equal(expected, input.Read(button, 0, 0, 430));
    }

    [Fact]
    public void StickUsesDominantAxisAndReleaseResetsRepeat()
    {
        var input = new ControllerDirectionRepeater();
        Assert.Null(input.Read(0, 2000, 1000, 0));
        Assert.Equal(FocusNavigationDirection.Right, input.Read(0, 14000, 10000, 10));
        Assert.Null(input.Read(0, 0, 0, 20));
        Assert.Equal(FocusNavigationDirection.Right, input.Read(0, 14000, 10000, 30));
        Assert.Equal(FocusNavigationDirection.Left, input.Read(0, short.MinValue, 0, 40));
        Assert.Equal(FocusNavigationDirection.Up, input.Read(1, short.MinValue, 0, 50));
    }
}
