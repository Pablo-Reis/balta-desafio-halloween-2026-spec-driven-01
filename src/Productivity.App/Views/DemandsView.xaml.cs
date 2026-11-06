using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Productivity.App.Support;
using Productivity.Application.Services;
using Productivity.Domain;

namespace Productivity.App.Views;

public partial class DemandsView : UserControl
{
    private readonly AppController _controller;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Guid? _selectedId;
    private bool _syncingFilters;
    private int _loadVersion;

    public DemandsView(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            UiGuard.Run(LoadListAsync);
        };
    }

    private sealed record DayHeader(DateOnly Day, string Label, string Summary);

    private sealed record DemandRow(Guid Id, string Title, string Subtitle, string Summary, DayHeader Header);

    private sealed record TimelineRow(string Time, string Label, string Detail);

    private sealed record NoteRow(string When, string Text);

    private bool HasFilter => SearchBox.Text.Trim().Length > 0 || FromPicker.SelectedDate is not null || ToPicker.SelectedDate is not null;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _controller.DemandsChanged += OnDemandsChanged;
        UpdateSearchChrome();
        UiGuard.Run(LoadListAsync);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _searchTimer.Stop();
        _controller.DemandsChanged -= OnDemandsChanged;
    }

    private void OnDemandsChanged() => UiGuard.Run(async () =>
    {
        await LoadListAsync();
        if (_selectedId is { } id)
            await LoadDetailAsync(id, reloadFields: false);
    });

    // --- Filtros -----------------------------------------------------------------------------------------------

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchChrome();
        if (_syncingFilters)
            return;

        // Espera o usuário parar de digitar antes de consultar.
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
        {
            e.Handled = true;
            SearchBox.Clear();
        }
    }

    private void OnDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingFilters)
            return;

        UpdateChips();
        UiGuard.Run(LoadListAsync);
    }

    private void OnQuickRangeToggled(object sender, RoutedEventArgs e)
    {
        if (_syncingFilters)
            return; // mudança feita pelo próprio código (UpdateChips)

        var chip = (ToggleButton)sender;
        var days = int.Parse((string)chip.Tag);
        var today = Today();

        _syncingFilters = true;
        FromPicker.SelectedDate = chip.IsChecked == true ? today.AddDays(-(days - 1)).ToDateTime(TimeOnly.MinValue) : null;
        ToPicker.SelectedDate = chip.IsChecked == true ? today.ToDateTime(TimeOnly.MinValue) : null;
        _syncingFilters = false;

        UpdateChips();
        UiGuard.Run(LoadListAsync);
    }

    private void OnClearFiltersClick(object sender, RoutedEventArgs e)
    {
        _syncingFilters = true;
        SearchBox.Clear();
        FromPicker.SelectedDate = null;
        ToPicker.SelectedDate = null;
        _syncingFilters = false;

        UpdateChips();
        UiGuard.Run(LoadListAsync);
    }

    private void UpdateSearchChrome()
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Marca o chip cujo período coincide exatamente com as datas escolhidas.</summary>
    private void UpdateChips()
    {
        var today = Today();
        var from = FromPicker.SelectedDate is { } f ? DateOnly.FromDateTime(f) : (DateOnly?)null;
        var to = ToPicker.SelectedDate is { } t ? DateOnly.FromDateTime(t) : (DateOnly?)null;

        var wasSyncing = _syncingFilters;
        _syncingFilters = true;
        foreach (var chip in new[] { TodayChip, WeekChip, MonthChip })
        {
            var days = int.Parse((string)chip.Tag);
            chip.IsChecked = from == today.AddDays(-(days - 1)) && to == today;
        }

        _syncingFilters = wasSyncing;
    }

    private DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_controller.Clock.UtcNow, _controller.Clock.TimeZone));

    // --- Lista agrupada por dia --------------------------------------------------------------------------------

    private async Task LoadListAsync()
    {
        var filter = new DemandFilter(
            SearchBox.Text,
            FromPicker.SelectedDate is { } from ? DateOnly.FromDateTime(from) : null,
            ToPicker.SelectedDate is { } to ? DateOnly.FromDateTime(to) : null);

        if (filter.From > filter.To)
        {
            FilterError.Text = "A data inicial é maior que a data final.";
            FilterError.Visibility = Visibility.Visible;
            return;
        }

        FilterError.Visibility = Visibility.Collapsed;

        var version = ++_loadVersion;
        var groups = await _controller.Demands.SearchAsync(filter);
        if (version != _loadVersion)
            return; // chegou uma consulta mais nova

        var now = _controller.Clock.UtcNow;
        var today = Today();
        var rows = new List<DemandRow>();

        foreach (var group in groups)
        {
            var header = new DayHeader(
                group.Day,
                Format.DayLabel(group.Day, today),
                $"{Format.Count(group.Demands.Count, "demanda", "demandas")} · {Format.Short(group.Worked)}");

            rows.AddRange(group.Demands.Select(d => new DemandRow(
                d.Id,
                d.Title,
                $"{d.Type} · {Format.TimeOfDay(d.CreatedAt)}",
                $"{Format.StatusLabel(d.Status)} · {Format.Short(d.GetWorkedTime(now))} trabalhados",
                header)));
        }

        var view = new ListCollectionView(rows);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DemandRow.Header)));

        DemandList.SelectionChanged -= OnDemandSelected;
        DemandList.ItemsSource = view;
        if (_selectedId is { } id)
            DemandList.SelectedItem = rows.FirstOrDefault(r => r.Id == id);
        DemandList.SelectionChanged += OnDemandSelected;

        ShowResultState(rows.Count, groups.Count);
    }

    private void ShowResultState(int demandCount, int dayCount)
    {
        var empty = demandCount == 0;
        NoResults.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        DemandList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ResultCount.Text = empty
            ? string.Empty
            : $"{Format.Count(demandCount, "demanda", "demandas")} em {Format.Count(dayCount, "dia", "dias")}";
        NoResultsText.Text = HasFilter
            ? "Nenhuma demanda encontrada para os filtros aplicados."
            : $"Nenhuma demanda ainda. Use {_controller.Settings.StartHotkey} para começar.";
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


