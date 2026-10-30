using Productivity.Domain;

namespace Productivity.Domain.Tests;

public class DemandTests
{
    private static readonly DateTime T0 = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_CreatesRunningDemandWithStartedEvent()
    {
        var demand = Demand.Start("  Corrigir bug  ", " Desenvolvimento ", null, T0);

        Assert.Equal(DemandStatus.Running, demand.Status);
        Assert.Equal("Corrigir bug", demand.Title);
        Assert.Equal("Desenvolvimento", demand.Type);
        Assert.Equal(DemandEventKind.Started, Assert.Single(demand.Events).Kind);
    }

    [Theory]
    [InlineData("", "Dev")]
    [InlineData("   ", "Dev")]
    [InlineData("Algo", "")]
    public void Start_WithoutTitleOrType_Throws(string title, string type)
    {
        Assert.Throws<DomainException>(() => Demand.Start(title, type, null, T0));
    }

    [Fact]
    public void Start_WithTitleTooLong_Throws()
    {
        Assert.Throws<DomainException>(() => Demand.Start(new string('a', Demand.MaxTitleLength + 1), "Dev", null, T0));
    }

    [Fact]
    public void PauseResumeFinish_FollowsValidTransitions()
    {
        var demand = Demand.Start("x", "Dev", null, T0);

        demand.Pause(T0.AddMinutes(10));
        Assert.Equal(DemandStatus.Paused, demand.Status);

        demand.Resume(T0.AddMinutes(15));
        Assert.Equal(DemandStatus.Running, demand.Status);

        demand.Finish(T0.AddMinutes(30));
        Assert.Equal(DemandStatus.Finished, demand.Status);
        Assert.Equal(T0.AddMinutes(30), demand.FinishedAt);
        Assert.Equal(
            new[] { DemandEventKind.Started, DemandEventKind.Paused, DemandEventKind.Resumed, DemandEventKind.Finished },
            demand.OrderedEvents.Select(e => e.Kind));
    }

    [Fact]
    public void Pause_WhenPaused_Throws()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.Pause(T0.AddMinutes(1));

        Assert.Throws<DomainException>(() => demand.Pause(T0.AddMinutes(2)));
    }

    [Fact]
    public void Resume_WhenRunning_Throws()
    {
        var demand = Demand.Start("x", "Dev", null, T0);

        Assert.Throws<DomainException>(() => demand.Resume(T0.AddMinutes(1)));
    }

    [Fact]
    public void Finish_WhenFinished_Throws()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.Finish(T0.AddMinutes(1));

        Assert.Throws<DomainException>(() => demand.Finish(T0.AddMinutes(2)));
        Assert.Throws<DomainException>(() => demand.Pause(T0.AddMinutes(2)));
    }

    [Fact]
    public void GetWorkedTime_ExcludesPauses()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.Pause(T0.AddMinutes(10));      // trabalhou 10
        demand.Resume(T0.AddMinutes(25));     // pausou 15
        demand.Pause(T0.AddMinutes(45));      // trabalhou 20
        demand.Resume(T0.AddMinutes(50));     // pausou 5
        demand.Finish(T0.AddMinutes(60));     // trabalhou 10

        Assert.Equal(TimeSpan.FromMinutes(40), demand.GetWorkedTime(T0.AddHours(5)));
        Assert.Equal(TimeSpan.FromMinutes(20), demand.GetPausedTime(T0.AddHours(5)));
    }

    [Fact]
    public void GetWorkedTime_WhileRunning_CountsUntilNow()
    {
        var demand = Demand.Start("x", "Dev", null, T0);

        Assert.Equal(TimeSpan.FromMinutes(7), demand.GetWorkedTime(T0.AddMinutes(7)));
    }

    [Fact]
    public void GetWorkedTime_WhilePaused_StopsAtPause()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.Pause(T0.AddMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), demand.GetWorkedTime(T0.AddMinutes(90)));
        Assert.Equal(TimeSpan.FromMinutes(80), demand.GetPausedTime(T0.AddMinutes(90)));
    }

    [Fact]
    public void Finish_WhilePaused_DoesNotCountPauseAsWork()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.Pause(T0.AddMinutes(10));
        demand.Finish(T0.AddMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(10), demand.GetWorkedTime(T0.AddHours(3)));
        Assert.Equal(TimeSpan.FromMinutes(20), demand.GetPausedTime(T0.AddHours(3)));
    }

    [Fact]
    public void Pause_WithClockBeforeLastEvent_NeverRecordsNegativeTime()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.Pause(T0.AddMinutes(-30));

        Assert.Equal(TimeSpan.Zero, demand.GetWorkedTime(T0.AddHours(1)));
        Assert.Equal(T0, demand.LastEvent.OccurredAt);
    }

    [Fact]
    public void UpdateDetails_ChangesFieldsAndValidates()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.UpdateDetails("Novo título", "Reunião", "Detalhes");

        Assert.Equal("Novo título", demand.Title);
        Assert.Equal("Reunião", demand.Type);
        Assert.Equal("Detalhes", demand.Description);
        Assert.Throws<DomainException>(() => demand.UpdateDetails("", "Reunião", null));
    }

    [Fact]
    public void GetTimeSinceLastEvent_ReturnsElapsedSinceLastEvent()
    {
        var demand = Demand.Start("x", "Dev", null, T0);
        demand.Pause(T0.AddMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(3), demand.GetTimeSinceLastEvent(T0.AddMinutes(13)));
    }
}
