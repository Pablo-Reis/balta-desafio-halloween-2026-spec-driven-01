using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Productivity.App.Support;
using Productivity.Domain;

namespace Productivity.App.Views;

public partial class DemandsView : UserControl
{
    private readonly AppController _controller;
    private Guid? _selectedId;

    public DemandsView(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
    }

    private sealed record DemandRow(Guid Id, string Title, string Subtitle, string Summary);

    private sealed record TimelineRow(string Time, string Label, string Detail);

    private sealed record NoteRow(string When, string Text);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _controller.DemandsChanged += OnDemandsChanged;
        UiGuard.Run(LoadListAsync);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _controller.DemandsChanged -= OnDemandsChanged;

    private void OnDemandsChanged() => UiGuard.Run(async () =>
    {
        await LoadListAsync();
        if (_selectedId is { } id)
            await LoadDetailAsync(id, reloadFields: false);
    });

    private async Task LoadListAsync()
    {
        var demands = await _controller.Demands.ListAsync();
        var now = _controller.Clock.UtcNow;

        DemandList.SelectionChanged -= OnDemandSelected;
        DemandList.ItemsSource = demands.Select(d => new DemandRow(
            d.Id,
            d.Title,
            $"{d.Type} · {Format.DateTimeMinutes(d.CreatedAt)}",
            $"{Format.StatusLabel(d.Status)} · {Format.Short(d.GetWorkedTime(now))} trabalhados")).ToList();

        if (_selectedId is { } id)
            DemandList.SelectedItem = ((IEnumerable<DemandRow>)DemandList.ItemsSource).FirstOrDefault(r => r.Id == id);
        DemandList.SelectionChanged += OnDemandSelected;
    }

    private void OnDemandSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DemandList.SelectedItem is DemandRow row)
        {
            _selectedId = row.Id;
            UiGuard.Run(() => LoadDetailAsync(row.Id, reloadFields: true), Window.GetWindow(this));
        }
    }

    private async Task LoadDetailAsync(Guid id, bool reloadFields)
    {
        var demand = await _controller.Demands.GetByIdAsync(id);
        if (demand is null)
            return;

        var events = await _controller.Events.ListByDemandAsync(id);
        var notes = await _controller.Notes.ListByDemandAsync(id);
        var now = _controller.Clock.UtcNow;

        EmptyText.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;

        if (reloadFields)
        {
            TypeBox.ItemsSource = await _controller.Demands.ListTypesAsync();
            TypeBox.Text = demand.Type;
            TitleBox.Text = demand.Title;
            DescriptionBox.Text = demand.Description;
            SaveStatus.Text = string.Empty;
        }

        SummaryText.Text =
            $"{Format.StatusLabel(demand.Status)} · trabalhado: {Format.Duration(demand.GetWorkedTime(now))} · pausas: {Format.Duration(demand.GetPausedTime(now))}";

        Timeline.ItemsSource = events.Select((e, i) => new TimelineRow(
            Format.DateTimeSeconds(e.OccurredAt),
            Format.EventLabel(e.Kind),
            TimelineDetail(events, i))).ToList();

        NotesList.ItemsSource = notes.Select(n => new NoteRow(Format.DateTimeSeconds(n.CreatedAt), n.Text)).ToList();
        NoNotesText.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string TimelineDetail(IReadOnlyList<DemandEvent> events, int index)
    {
        if (index == 0)
            return string.Empty;

        var elapsed = Format.Duration(events[index].OccurredAt - events[index - 1].OccurredAt);
        var previous = events[index - 1].Kind;
        return previous == DemandEventKind.Paused ? $"descansou {elapsed}" : $"trabalhou {elapsed}";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => UiGuard.Run(async () =>
    {
        if (_selectedId is not { } id)
            return;

        await _controller.Demands.UpdateDetailsAsync(id, TitleBox.Text, TypeBox.Text, DescriptionBox.Text);
        SaveStatus.Text = "Alterações salvas.";
    }, Window.GetWindow(this));

    private void OnNewNoteKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnAddNoteClick(sender, e);
    }

    private void OnAddNoteClick(object sender, RoutedEventArgs e) => UiGuard.Run(async () =>
    {
        if (_selectedId is not { } id)
            return;

        await _controller.Notes.AddAsync(id, NewNoteBox.Text, NoteSource.Application);
        NewNoteBox.Clear();
        await LoadDetailAsync(id, reloadFields: false);
    }, Window.GetWindow(this));
}
