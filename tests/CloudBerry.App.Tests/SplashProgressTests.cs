using CloudBerry.App.ViewModels;

namespace CloudBerry.App.Tests;

public sealed class SplashProgressTests
{
    [Fact]
    public void ProgressNeverRewindsAndRejectsInvalidValues()
    {
        var model = new SplashViewModel();
        model.ProgressValue = 65;
        model.ProgressValue = 20;
        Assert.Equal(65, model.ProgressValue);
        model.ProgressValue = double.NaN;
        model.ProgressValue = double.PositiveInfinity;
        Assert.Equal(65, model.ProgressValue);
        model.ProgressValue = 150;
        Assert.Equal(100, model.ProgressValue);
        Assert.Equal("100%", model.ProgressLabel);
    }
}
