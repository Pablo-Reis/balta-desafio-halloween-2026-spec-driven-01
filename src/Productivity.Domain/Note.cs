namespace Productivity.Domain;

public sealed class Note
{
    public const int MaxTextLength = 2000;

    private Note()
    {
        Text = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid DemandId { get; private set; }
    public string Text { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public NoteSource Source { get; private set; }

    public static Note Create(Guid demandId, string text, NoteSource source, DateTime now)
    {
        text = (text ?? string.Empty).Trim();

        if (text.Length == 0)
            throw new DomainException("Digite o texto da anotação.");
        if (text.Length > MaxTextLength)
            throw new DomainException($"A anotação deve ter no máximo {MaxTextLength} caracteres.");

        return new Note
        {
            Id = Guid.NewGuid(),
            DemandId = demandId,
            Text = text,
            CreatedAt = now,
            Source = source
        };
    }
}
