namespace Productivity.Domain;

/// <summary>Configuração persistida como par chave/valor.</summary>
public sealed class AppSetting
{
    private AppSetting()
    {
        Key = string.Empty;
        Value = string.Empty;
    }

    public string Key { get; private set; }
    public string Value { get; private set; }

    public static AppSetting Create(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("A chave da configuração é obrigatória.");

        return new AppSetting { Key = key.Trim(), Value = value ?? string.Empty };
    }

    public void ChangeValue(string value) => Value = value ?? string.Empty;
}
