namespace Productivity.Domain;

/// <summary>Demanda cronometrada. Todo o tempo é derivado dos eventos (início, pausas, retomadas e fim).</summary>
public sealed class Demand
{
    public const int MaxTitleLength = 200;
    public const int MaxTypeLength = 80;
    public const int MaxDescriptionLength = 4000;

    private readonly List<DemandEvent> _events = new();

    private Demand()
    {
        Title = string.Empty;
        Type = string.Empty;
        Description = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Type { get; private set; }
    public string Description { get; private set; }
    public DemandStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    public IReadOnlyCollection<DemandEvent> Events => _events;

    public IReadOnlyList<DemandEvent> OrderedEvents => _events.OrderBy(e => e.Sequence).ToList();

    public DemandEvent LastEvent => _events.OrderBy(e => e.Sequence).Last();

    public static Demand Start(string title, string type, string? description, DateTime now)
    {
        var demand = new Demand
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Status = DemandStatus.Running
        };
        demand.ApplyDetails(title, type, description);
        demand.AddEvent(DemandEventKind.Started, now);
        return demand;
    }

    public void Pause(DateTime now)
    {
        if (Status != DemandStatus.Running)
            throw new DomainException("Só é possível pausar uma demanda que esteja em andamento.");

        AddEvent(DemandEventKind.Paused, now);
        Status = DemandStatus.Paused;
    }

    public void Resume(DateTime now)
    {
        if (Status != DemandStatus.Paused)
            throw new DomainException("Só é possível retomar uma demanda que esteja pausada.");

        AddEvent(DemandEventKind.Resumed, now);
        Status = DemandStatus.Running;
    }

    public void Finish(DateTime now)
    {
        if (Status == DemandStatus.Finished)
            throw new DomainException("A demanda já foi finalizada.");

        var at = AddEvent(DemandEventKind.Finished, now);
        Status = DemandStatus.Finished;
        FinishedAt = at;
    }

    public void UpdateDetails(string title, string type, string? description) => ApplyDetails(title, type, description);

    /// <summary>Intervalos trabalhados. Pausas nunca entram (RF-06).</summary>
    public IReadOnlyList<WorkSegment> GetWorkSegments(DateTime now)
    {
        var segments = new List<WorkSegment>();
        DateTime? open = null;

        foreach (var e in OrderedEvents)
        {
            switch (e.Kind)
            {
                case DemandEventKind.Started:
                case DemandEventKind.Resumed:
                    open = e.OccurredAt;
                    break;
                case DemandEventKind.Paused:
                case DemandEventKind.Finished:
                    if (open is { } start)
                        segments.Add(new WorkSegment(start, e.OccurredAt));
                    open = null;
                    break;
            }
        }

        if (open is { } running)
            segments.Add(new WorkSegment(running, now > running ? now : running));

        return segments;
    }

    public TimeSpan GetWorkedTime(DateTime now) =>
        GetWorkSegments(now).Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);

    /// <summary>Tempo total em pausa/descanso.</summary>
    public TimeSpan GetPausedTime(DateTime now)
    {
        var total = TimeSpan.Zero;
        DateTime? pausedAt = null;

        foreach (var e in OrderedEvents)
        {
            switch (e.Kind)
            {
                case DemandEventKind.Paused:
                    pausedAt = e.OccurredAt;
                    break;
                case DemandEventKind.Resumed:
                case DemandEventKind.Finished:
                    if (pausedAt is { } p)
                        total += e.OccurredAt - p;
                    pausedAt = null;
                    break;
            }
        }

        if (pausedAt is { } still)
            total += now > still ? now - still : TimeSpan.Zero;

        return total;
    }

    /// <summary>Tempo decorrido desde o último evento (trabalho contínuo ou descanso atual).</summary>
    public TimeSpan GetTimeSinceLastEvent(DateTime now)
    {
        var elapsed = now - LastEvent.OccurredAt;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    private DateTime AddEvent(DemandEventKind kind, DateTime now)
    {
        // Proteção contra relógio alterado: nunca registra evento antes do anterior.
        var at = _events.Count > 0 && now < LastEvent.OccurredAt ? LastEvent.OccurredAt : now;
        _events.Add(new DemandEvent(Id, _events.Count, kind, at));
        return at;
    }

    private void ApplyDetails(string title, string type, string? description)
    {
        title = (title ?? string.Empty).Trim();
        type = (type ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();

        if (title.Length == 0)
            throw new DomainException("Informe o que será feito na demanda.");
        if (type.Length == 0)
            throw new DomainException("Informe o tipo da demanda.");
        if (title.Length > MaxTitleLength)
            throw new DomainException($"A descrição da demanda deve ter no máximo {MaxTitleLength} caracteres.");
        if (type.Length > MaxTypeLength)
            throw new DomainException($"O tipo da demanda deve ter no máximo {MaxTypeLength} caracteres.");
        if (description.Length > MaxDescriptionLength)
            throw new DomainException($"O detalhamento deve ter no máximo {MaxDescriptionLength} caracteres.");

        Title = title;
        Type = type;
        Description = description;
    }
}
