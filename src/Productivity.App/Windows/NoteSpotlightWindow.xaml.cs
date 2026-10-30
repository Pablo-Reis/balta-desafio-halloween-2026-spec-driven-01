using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Productivity.App.Windows;

/// <summary>Campo central estilo spotlight para anotação rápida (RF-19, RF-20).</summary>
public partial class NoteSpotlightWindow : Window
{
    private bool _finished;

    public NoteSpotlightWindow(string demandTitle)
    {
        InitializeComponent();
        ContextText.Text = $"Anotação para: {demandTitle}";
    }

    public string NoteText { get; private set; } = string.Empty;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + (area.Height - ActualHeight) / 2 - area.Height * 0.1;

        Activate();
        Focus();
        NoteBox.Focus();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) =>
        Placeholder.Visibility = NoteBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(false);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (NoteBox.Text.Trim().Length > 0)
            {
                NoteText = NoteBox.Text.Trim();
                Finish(true);
            }
        }
    }

    // Clicar fora cancela, como no Spotlight.
    private void OnDeactivated(object? sender, EventArgs e) => Finish(false);

    private void Finish(bool saved)
    {
        if (_finished)
            return;

        _finished = true;
        DialogResult = saved;
    }
}
