using DeskPulse;
using Xunit;

namespace DeskPulse.Tests;

public sealed class UserExperiencePreferenceTests
{
    [Fact]
    public void NewSettingsEnableStartupSplashAndUse24HourTime()
    {
        var settings = new AppSettings();

        Assert.True(settings.ShowStartupSplash);
        Assert.False(settings.Use12HourTime);
    }

    [Fact]
    public void ClonePreservesUserExperiencePreferences()
    {
        var settings = new AppSettings
        {
            ShowStartupSplash = false,
            Use12HourTime = true
        };

        var clone = settings.Clone();

        Assert.False(clone.ShowStartupSplash);
        Assert.True(clone.Use12HourTime);
    }

    [Fact]
    public void DoubleClickingActiveGroupingUngroups()
    {
        Assert.Equal("None", ViewLogForm.ToggleHeaderGrouping("Folder", "Folder"));
    }

    [Fact]
    public void DoubleClickingDifferentGroupingSwitchesGrouping()
    {
        Assert.Equal("Application", ViewLogForm.ToggleHeaderGrouping("Folder", "Application"));
    }
}
