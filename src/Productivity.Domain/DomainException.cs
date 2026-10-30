namespace Productivity.Domain;

/// <summary>Violação de regra de negócio. A mensagem é amigável e pode ser exibida ao usuário.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
