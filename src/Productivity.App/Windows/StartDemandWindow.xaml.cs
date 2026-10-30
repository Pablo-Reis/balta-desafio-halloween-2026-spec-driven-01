using System.Windows;
using System.Windows.Input;
using Productivity.Domain;

namespace Productivity.App.Windows;

public partial class StartDemandWindow : Window
{
    private static readonly string[] DefaultTypes = { "Desenvolvimento", "Reunião", "Suporte", "Estudo", "Planejamento" };

    public StartDemandWindow(IReadOnlyList<string> knownTypes, Demand? active)
    {
        InitializeComponent();

        foreach (var type in knownTypes.Concat(DefaultTypes).Distinct(StringComparer.OrdinalIgnoreCase))
            TypeBox.Items.Add(type);

        TypeBox.Text = active?.Type ?? knownTypes.FirstOrDefault() ?? DefaultTypes[0];

        if (active is not null)
        {
            WarningText.Text = $"A demanda atual \"{active.Title}\" será finalizada ao iniciar esta.";
            WarningText.Visibility = Visibility.Visible;
        }
    }

    public string DemandTitle { get; private set; } = string.Empty;
    public string DemandType { get; private set; } = string.Empty;
    public string? Details { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Janela aberta por atalho global: força o foco para poder digitar imediatamente.
        Activate();
        Focus();
        TitleBox.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
        }
        else if (e.Key == Key.Enter && !DetailsBox.IsKeyboardFocused)
        {
            e.Handled = true;
            OnConfirm(sender, e);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        var type = TypeBox.Text.Trim();

        if (title.Length == 0 || type.Length == 0)
        {
            ErrorText.Text = title.Length == 0 ? "Informe o que será feito." : "Informe o tipo da demanda.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        DemandTitle = title;
        DemandType = type;
        Details = DetailsBox.Text;
        DialogResult = true;
    }
}
