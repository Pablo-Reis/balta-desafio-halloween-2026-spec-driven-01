namespace Productivity.Domain;

public sealed class DemandEvent
{
    private DemandEvent()
    {
    }

    internal DemandEvent(Guid demandId, int sequence, DemandEventKind kind, DateTime occurredAt)
    {
        Id = Guid.NewGuid();
        DemandId = demandId;
        Sequence = sequence;
        Kind = kind;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid DemandId { get; private set; }
    public int Sequence { get; private set; }
    public DemandEventKind Kind { get; private set; }
    public DateTime OccurredAt { get; private set; }
}
