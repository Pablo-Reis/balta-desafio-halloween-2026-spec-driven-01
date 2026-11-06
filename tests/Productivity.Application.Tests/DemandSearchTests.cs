using Microsoft.Extensions.Logging.Abstractions;
using Productivity.Application.Services;
using Productivity.Domain;

namespace Productivity.Application.Tests;

public sealed class DemandSearchTests : IDisposable
{
    private readonly TestDbFactory _factory = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc) };
    private readonly DemandService _service;

    public DemandSearchTests()
    {
        _service = new DemandService(_factory, _clock, NullLogger<DemandService>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>Cria e finaliza uma demanda no instante informado, trabalhando por <paramref name="minutes"/>.</summary>
    private async Task<Demand> SeedAsync(string title, DateTime startUtc, int minutes, string type = "Dev")
    {
        _clock.UtcNow = startUtc;
        var demand = await _service.StartAsync(title, type, null, finishCurrent: true);
        _clock.Advance(TimeSpan.FromMinutes(minutes));
        await _service.FinishAsync();
        return demand;
    }

    private static DateTime Utc(int month, int day, int hour = 10) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Search_WithoutFilter_ReturnsEverythingGroupedByDayNewestFirst()
    {
        await SeedAsync("Segunda", Utc(11, 2), 30);
        await SeedAsync("Terça A", Utc(11, 3, 9), 20);
        await SeedAsync("Terça B", Utc(11, 3, 15), 10);

        var groups = await _service.SearchAsync(new DemandFilter());

        Assert.Equal(new[] { new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 2) }, groups.Select(g => g.Day));
        Assert.Equal(new[] { "Terça B", "Terça A" }, groups[0].Demands.Select(d => d.Title));
        Assert.Equal(new[] { "Segunda" }, groups[1].Demands.Select(d => d.Title));
    }

    [Fact]
    public async Task Search_SumsWorkedTimePerDay_IgnoringPauses()
    {
        await SeedAsync("A", Utc(11, 3, 9), 20);
        _clock.UtcNow = Utc(11, 3, 12);
        await _service.StartAsync("B", "Dev", null, finishCurrent: true);
        _clock.Advance(TimeSpan.FromMinutes(10));
        await _service.PauseAsync();
        _clock.Advance(TimeSpan.FromMinutes(45)); // pausa não conta
        await _service.ResumeAsync();
        _clock.Advance(TimeSpan.FromMinutes(5));
        await _service.FinishAsync();

        var groups = await _service.SearchAsync(new DemandFilter());

        var day = Assert.Single(groups);
        Assert.Equal(TimeSpan.FromMinutes(35), day.Worked);
    }

    [Fact]
    public async Task Search_ByTitle_IgnoresCaseAndAccents()
    {
        await SeedAsync("Integração SPC", Utc(11, 2), 10);
        await SeedAsync("Reunião de equipe", Utc(11, 3), 10);

        Assert.Equal("Integração SPC", Assert.Single(Flatten(await _service.SearchAsync(new DemandFilter("integracao")))).Title);
        Assert.Equal("Integração SPC", Assert.Single(Flatten(await _service.SearchAsync(new DemandFilter("INTEGRAÇÃO")))).Title);
        Assert.Equal("Reunião de equipe", Assert.Single(Flatten(await _service.SearchAsync(new DemandFilter("  reuniao  ")))).Title);
    }

    [Fact]
    public async Task Search_ByTitle_DoesNotMatchTypeOrDescription()
    {
        var demand = await SeedAsync("Relatório", Utc(11, 2), 10, type: "Suporte");
        await _service.UpdateDetailsAsync(demand.Id, "Relatório", "Suporte", "menciona planilha");

        Assert.Empty(await _service.SearchAsync(new DemandFilter("suporte")));
        Assert.Empty(await _service.SearchAsync(new DemandFilter("planilha")));
    }

    [Fact]
    public async Task Search_WithNoMatch_ReturnsEmptyList()
    {
        await SeedAsync("Algo", Utc(11, 2), 10);

        Assert.Empty(await _service.SearchAsync(new DemandFilter("inexistente")));
    }

    [Fact]
    public async Task Search_ByDateRange_IncludesBothEndDays()
    {
        await SeedAsync("D1", Utc(11, 1), 5);
        await SeedAsync("D2", Utc(11, 2, 0), 5);   // 00:00 do dia 2 entra
        await SeedAsync("D3", Utc(11, 3, 23), 5);  // 23:00 do dia 3 entra
        await SeedAsync("D4", Utc(11, 4), 5);

        var groups = await _service.SearchAsync(new DemandFilter(From: new DateOnly(2026, 11, 2), To: new DateOnly(2026, 11, 3)));

        Assert.Equal(new[] { "D3", "D2" }, Flatten(groups).Select(d => d.Title));
    }

    [Fact]
    public async Task Search_WithOnlyFrom_ReturnsThatDayOnward()
    {
        await SeedAsync("Antiga", Utc(10, 20), 5);
        await SeedAsync("Nova", Utc(11, 3), 5);

        var groups = await _service.SearchAsync(new DemandFilter(From: new DateOnly(2026, 11, 1)));

        Assert.Equal("Nova", Assert.Single(Flatten(groups)).Title);
    }

    [Fact]
    public async Task Search_WithOnlyTo_ReturnsUpToThatDay()
    {
        await SeedAsync("Antiga", Utc(10, 20), 5);
        await SeedAsync("Nova", Utc(11, 3), 5);

        var groups = await _service.SearchAsync(new DemandFilter(To: new DateOnly(2026, 10, 31)));

        Assert.Equal("Antiga", Assert.Single(Flatten(groups)).Title);
    }

    [Fact]
    public async Task Search_CombinesTitleAndDate()
    {
        await SeedAsync("Relatório A", Utc(11, 2), 5);
        await SeedAsync("Relatório B", Utc(11, 3), 5);
        await SeedAsync("Outro", Utc(11, 3, 15), 5);

        var groups = await _service.SearchAsync(new DemandFilter("relatorio", new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 3)));

        Assert.Equal("Relatório B", Assert.Single(Flatten(groups)).Title);
    }

    [Fact]
    public async Task Search_WithInvertedDates_Throws()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.SearchAsync(new DemandFilter(From: new DateOnly(2026, 11, 5), To: new DateOnly(2026, 11, 1))));

        Assert.Contains("data inicial", ex.Message);
    }

    [Fact]
    public async Task Search_GroupsByLocalDay_NotUtcDay()
    {
        _clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone("teste-3", TimeSpan.FromHours(-3), "teste", "teste");
        await SeedAsync("Madrugada UTC", new DateTime(2026, 11, 3, 1, 30, 0, DateTimeKind.Utc), 5); // 22:30 do dia 2 (local)
        await SeedAsync("Manhã", new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc), 5);          // 09:00 do dia 3 (local)

        var groups = await _service.SearchAsync(new DemandFilter());

        Assert.Equal(new[] { new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 2) }, groups.Select(g => g.Day));
        Assert.Equal("Madrugada UTC", Assert.Single(groups[1].Demands).Title);
    }

    [Fact]
    public async Task Search_DateFilterUsesLocalDay()
    {
        _clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone("teste-3", TimeSpan.FromHours(-3), "teste", "teste");
        await SeedAsync("Noite do dia 2", new DateTime(2026, 11, 3, 1, 30, 0, DateTimeKind.Utc), 5); // dia 2 local

        var day2 = await _service.SearchAsync(new DemandFilter(From: new DateOnly(2026, 11, 2), To: new DateOnly(2026, 11, 2)));
        var day3 = await _service.SearchAsync(new DemandFilter(From: new DateOnly(2026, 11, 3), To: new DateOnly(2026, 11, 3)));

        Assert.Single(Flatten(day2));
        Assert.Empty(day3);
    }

    [Fact]
    public async Task Search_RespectsTakeLimit_KeepingNewestDemands()
    {
        await SeedAsync("Velha", Utc(11, 1), 5);
        await SeedAsync("Meio", Utc(11, 2), 5);
        await SeedAsync("Nova", Utc(11, 3), 5);

        var groups = await _service.SearchAsync(new DemandFilter(), take: 2);

        Assert.Equal(new[] { "Nova", "Meio" }, Flatten(groups).Select(d => d.Title));
    }

    [Fact]
    public async Task Search_IncludesActiveDemand_WithWorkedTimeUntilNow()
    {
        _clock.UtcNow = Utc(11, 5, 8);
        await _service.StartAsync("Em andamento", "Dev", null);
        _clock.Advance(TimeSpan.FromMinutes(25));

        var groups = await _service.SearchAsync(new DemandFilter());

        var day = Assert.Single(groups);
        Assert.Equal(TimeSpan.FromMinutes(25), day.Worked);
        Assert.Equal(DemandStatus.Running, Assert.Single(day.Demands).Status);
    }

    private static IReadOnlyList<Demand> Flatten(IReadOnlyList<DemandDayGroup> groups) =>
        groups.SelectMany(g => g.Demands).ToList();
}
