using Productivity.Domain;

namespace Productivity.Domain.Tests;

public class NoteAndSettingTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Note_Create_TrimsText()
    {
        var note = Note.Create(Guid.NewGuid(), "  revisar PR  ", NoteSource.Hotkey, Now);

        Assert.Equal("revisar PR", note.Text);
        Assert.Equal(NoteSource.Hotkey, note.Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Note_Create_WithEmptyText_Throws(string text)
    {
        Assert.Throws<DomainException>(() => Note.Create(Guid.NewGuid(), text, NoteSource.Application, Now));
    }

    [Fact]
    public void Note_Create_WithTextTooLong_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Note.Create(Guid.NewGuid(), new string('a', Note.MaxTextLength + 1), NoteSource.Application, Now));
    }

    [Fact]
    public void AppSetting_Create_WithoutKey_Throws()
    {
        Assert.Throws<DomainException>(() => AppSetting.Create(" ", "x"));
    }

    [Fact]
    public void AppSetting_ChangeValue_UpdatesValue()
    {
        var setting = AppSetting.Create("Theme", "Dark");
        setting.ChangeValue("Light");

        Assert.Equal("Light", setting.Value);
    }
}
