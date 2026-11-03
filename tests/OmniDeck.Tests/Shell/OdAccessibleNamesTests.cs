using System.Globalization;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using OmniDeck.App.Legal;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.Core.Accessibility;
using OmniDeck.Core.Localization;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Calendar;
using OmniDeck.Modules.Timer;
using OmniDeck.Modules.Weather;
using OmniDeck.Tests.TestSupport;
using Wpf.Ui.Controls;

namespace OmniDeck.Tests.Shell;

/// <summary>What screen readers hear for the parts WPF-UI leaves unnamed, for rows and for list items.</summary>
public sealed class OdAccessibleNamesTests
{
    [Fact]
    public void TemplateButtons_GetNames_ByCommandOrByPart() => OdSta.Run(() =>
    {
        var minimize = new System.Windows.Controls.Button { Command = ApplicationCommands.Close };
        AutomationProperties.SetAutomationId(minimize, "TitleBarMinimizeButton");

        Assert.Equal("[A11y_ScrollUp]", NameOf(new RepeatButton { Command = ScrollBar.LineUpCommand }));
        Assert.Equal("[A11y_Decrease]", NameOf(new RepeatButton { Command = Slider.DecreaseLarge }));
        Assert.Equal("[A11y_PageDown]", NameOf(new RepeatButton { Name = "PageDown" }));
        Assert.Equal("[A11y_Increase]", NameOf(new RepeatButton { Name = "PART_InlineIncrementButton" }));
        Assert.Equal("[A11y_Clear]", NameOf(new System.Windows.Controls.Button { Name = "PART_ClearButton" }));
        Assert.Equal("[A11y_Minimize]", NameOf(minimize));
        Assert.Null(OdAutomationNames.NameFor(new System.Windows.Controls.Button { Name = "Elsewhere" }));
        return Task.CompletedTask;
    });

    [Fact]
    public void ARowsButton_IsNamedAfterItsLabel_AndItsRow() => OdSta.Run(() =>
    {
        var open = new System.Windows.Controls.Button { DataContext = new { Title = "Clock" } };
        var start = new System.Windows.Controls.Button { DataContext = new { Name = "Tea", ToggleText = "Pause" } };
        var unnamed = new System.Windows.Controls.Button { DataContext = new { Title = "" } };

        open.SetBinding(AutomationProperties.NameProperty, Row(new OdLocRowExtension("Shell_Open") { Path = "Title" }));
        start.SetBinding(AutomationProperties.NameProperty, Row(new OdLocRowExtension { LabelPath = "ToggleText", Path = "Name" }));
        unnamed.SetBinding(AutomationProperties.NameProperty, Row(new OdLocRowExtension("Shell_Open") { Path = "Title" }));

        Assert.Equal("Open, Clock", AutomationProperties.GetName(open));
        Assert.Equal("Pause, Tea", AutomationProperties.GetName(start));
        Assert.Equal("Open", AutomationProperties.GetName(unnamed));
        return Task.CompletedTask;
    });

    [Fact]
    public void StatusLines_ArePoliteLiveRegions_UnlessTheySayOtherwise() => OdSta.Run(() =>
    {
        var polite = new OdStatusText { Text = "Saved" };
        var assertive = new OdStatusText();
        AutomationProperties.SetLiveSetting(assertive, AutomationLiveSetting.Assertive);
        assertive.Text = "Not accepted";

        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(polite));
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(assertive));
        Assert.Equal("Not accepted", assertive.Text);
        return Task.CompletedTask;
    });

    [Fact]
    public void ListItems_AreNamedAsTheyRead()
    {
        var date = new DateOnly(2026, 11, 3);
        var longDate = date.ToString("D", CultureInfo.CurrentCulture);

        Assert.Equal(longDate + ", today", new OdCalendarDay(date, "3", IsInPeriod: true, IsToday: true, [], More: null).ToString());
        Assert.Equal(longDate, new OdCalendarDay(date, "3", IsInPeriod: false, IsToday: false, [], More: null).ToString());
        Assert.Equal("1 PM: 29°, Partly cloudy, 78% chance of rain", new OdWeatherHour("1 PM", "29°", SymbolRegular.WeatherPartlyCloudyDay24, "Partly cloudy", "78%").ToString());
        Assert.Equal("Tue 4: 30° / 24°, Rain, 90% chance of rain", new OdWeatherDay("Tue 4", "30° / 24°", SymbolRegular.WeatherRain24, "Rain", "90%").ToString());
        Assert.Equal("Lap 3: 0:12.34, total 1:02.11", new OdLapViewModel("Lap 3", "0:12.34", "1:02.11").ToString());
        Assert.Equal("2+2 = 4", new OdCalcHistoryEntry("2+2", "4").ToString());
        Assert.Equal("5 min", new OdTimerPreset(5, "5 min").ToString());
        Assert.Equal("YouTube, Gmail", new OdWebProfileGroup("google", ["youtube", "gmail"], "YouTube, Gmail").ToString());
        Assert.Equal("License agreement", new OdLegalPage("License agreement", "# EULA").ToString());
        Assert.Equal("Weather", new OdPaletteEntry("Weather", "Information", "WeatherSunny24", () => Task.CompletedTask).ToString());
        Assert.Equal("Green", OdAccentSwatch.All()[5].ToString());
    }

    private static BindingBase Row(OdLocRowExtension extension) => (BindingBase)extension.ProvideValue(new NoServices());

    private static string? NameOf(ButtonBase button) => OdAutomationNames.NameFor(button)?.Path.Path;

    /// <summary>A markup extension provided outside XAML: the binding comes back as it is.</summary>
    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
