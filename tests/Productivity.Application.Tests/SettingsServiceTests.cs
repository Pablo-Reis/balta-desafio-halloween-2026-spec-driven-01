using Microsoft.Extensions.Logging.Abstractions;
using Productivity.Application.Services;
using Productivity.Domain;

namespace Productivity.Application.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly TestDbFactory _factory = new();
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _service = new SettingsService(_factory, NullLogger<SettingsService>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_WithoutSavedValues_ReturnsDefaults()
    {
        Assert.Equal(UserSettings.Default, await _service.GetAsync());
    }

    [Fact]
    public async Task Save_PersistsAndReloadsAllValues()
    {
        var custom = UserSettings.Default with
        {
            StartWithWindows = true,
            Theme = AppTheme.Dark,
            StartHotkey = Hotkey.Parse("Ctrl+Shift+F1"),
            BreakAlertMinutes = 25,
            BreakDurationMinutes = 5
        };

        await _service.SaveAsync(custom);
        await _service.SaveAsync(custom with { Theme = AppTheme.Light });

        var loaded = await _service.GetAsync();
        Assert.Equal(custom with { Theme = AppTheme.Light }, loaded);
    }

    [Fact]
    public async Task Save_WithDuplicatedHotkeys_Throws()
    {
        var invalid = UserSettings.Default with { PauseHotkey = UserSettings.Default.StartHotkey };

        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.SaveAsync(invalid));
        Assert.Contains("atalhos repetidos", ex.Message);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    [InlineData(1441, 5)]
    public async Task Save_WithInvalidMinutes_Throws(int alert, int duration)
    {
        var invalid = UserSettings.Default with { BreakAlertMinutes = alert, BreakDurationMinutes = duration };

        await Assert.ThrowsAsync<DomainException>(() => _service.SaveAsync(invalid));
    }

    [Fact]
    public async Task Save_WithZeroMinutes_DisablesAlertsAndIsAccepted()
    {
        await _service.SaveAsync(UserSettings.Default with { BreakAlertMinutes = 0, BreakDurationMinutes = 0 });

        var loaded = await _service.GetAsync();
        Assert.Equal(0, loaded.BreakAlertMinutes);
        Assert.Equal(0, loaded.BreakDurationMinutes);
    }

    [Fact]
    public async Task Save_RaisesSettingsChanged()
    {
        var raised = false;
        _service.SettingsChanged += () => raised = true;

        await _service.SaveAsync(UserSettings.Default);

        Assert.True(raised);
    }
}
