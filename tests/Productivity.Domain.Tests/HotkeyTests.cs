using Productivity.Domain;

namespace Productivity.Domain.Tests;

public class HotkeyTests
{
    [Fact]
    public void Parse_ReadsModifiersAndKey()
    {
        var hotkey = Hotkey.Parse("ctrl+alt+s");

        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, hotkey.Modifiers);
        Assert.Equal("S", hotkey.Key);
        Assert.Equal("Ctrl+Alt+S", hotkey.ToString());
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        var original = Hotkey.Create(HotkeyModifiers.Shift | HotkeyModifiers.Win | HotkeyModifiers.Control, "F9");

        Assert.Equal(original, Hotkey.Parse(original.ToString()));
    }

    [Theory]
    [InlineData("S")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Enter")]
    [InlineData("Ctrl+F13")]
    [InlineData("")]
    public void Parse_WithInvalidText_Throws(string text)
    {
        Assert.Throws<DomainException>(() => Hotkey.Parse(text));
    }

    [Theory]
    [InlineData("Ctrl+A", 0x41)]
    [InlineData("Ctrl+5", 0x35)]
    [InlineData("Alt+F1", 0x70)]
    [InlineData("Alt+F12", 0x7B)]
    public void VirtualKey_MatchesWindowsCodes(string text, int expected)
    {
        Assert.Equal(expected, Hotkey.Parse(text).VirtualKey);
    }

    [Fact]
    public void TryParse_WithInvalidText_ReturnsFriendlyError()
    {
        Assert.False(Hotkey.TryParse("A", out var hotkey, out var error));
        Assert.Null(hotkey);
        Assert.Contains("modificadora", error);
    }
}
