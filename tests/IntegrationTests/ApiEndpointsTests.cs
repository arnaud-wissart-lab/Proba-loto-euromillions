using System.Net;
using System.Net.Http.Json;
using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationTests;

public sealed class ApiEndpointsTests : IClassFixture<ApiEndpointsTests.ApiTestFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(ApiTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStatusShouldReturnEnrichedPayload()
    {
        var response = await _client.GetAsync("/api/status");
        var payload = await response.Content.ReadFromJsonAsync<StatusDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(321, payload.Loto.DrawsCount);
        Assert.Equal(654, payload.EuroMillions.DrawsCount);
        Assert.NotEqual(DateOnly.MinValue, payload.Loto.NextDrawDate);
    }

    [Fact]
    public async Task GetStatsShouldReturnStatsPayload()
    {
        var response = await _client.GetAsync("/api/stats/Loto");
        var payload = await response.Content.ReadFromJsonAsync<GameStatsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Loto", payload.Game);
        Assert.Equal(42, payload.TotalDraws);
        Assert.NotEmpty(payload.MainStats);
        Assert.NotEmpty(payload.BonusStats);
    }

    [Fact]
    public async Task PostGenerateShouldReturnRequestedNumberOfGrids()
    {
        var request = new GenerateGridsRequestDto("Loto", 7, "frequency");

        var response = await _client.PostAsJsonAsync("/api/grids/generate", request);
        var payload = await response.Content.ReadFromJsonAsync<GenerateGridsResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(7, payload.Grids.Count);
        Assert.Equal("frequency", payload.Strategy);
    }

    [Fact]
    public async Task PostGenerateShouldReturnBadRequestWhenPayloadIsInvalid()
    {
        var request = new GenerateGridsRequestDto("unknown", 0, "invalid");

        var response = await _client.PostAsJsonAsync("/api/grids/generate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public sealed class ApiTestFactory : WebApplicationFactory<Program>
    {
        public ApiTestFactory()
        {
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__Postgres",
                "Host=localhost;Database=fake;Username=fake;Password=fake");
            Environment.SetEnvironmentVariable("Database__AutoMigrate", "false");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=fake;Username=fake;Password=fake");
            builder.UseSetting("Database:AutoMigrate", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStatusService>();
                services.RemoveAll<IStatisticsService>();
                services.RemoveAll<IGridGenerationService>();

                services.AddSingleton<IStatusService, FakeStatusService>();
                services.AddSingleton<IStatisticsService, FakeStatisticsService>();
                services.AddSingleton<IGridGenerationService, FakeGridGenerationService>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
            Environment.SetEnvironmentVariable("Database__AutoMigrate", null);
        }
    }

    private sealed class FakeStatusService : IStatusService
    {
        public Task<StatusDto> GetStatusAsync(CancellationToken cancellationToken)
        {
            var payload = new StatusDto(
                DateTimeOffset.UtcNow,
                new GameStatusDto(321, new DateOnly(2026, 2, 18), new DateOnly(2026, 2, 21)),
                new GameStatusDto(654, new DateOnly(2026, 2, 17), new DateOnly(2026, 2, 20)),
                "Fake disclaimer");

            return Task.FromResult(payload);
        }
    }

    private sealed class FakeStatisticsService : IStatisticsService
    {
        public Task<GameStatsDto> GetStatsAsync(LotteryGame game, CancellationToken cancellationToken)
        {
            var payload = new GameStatsDto(
                game.ToString(),
                new DateOnly(2024, 1, 1),
                new DateOnly(2026, 2, 18),
                42,
                new[]
                {
                    new NumberStatDto(1, 6, 14.2857, new DateOnly(2026, 2, 18)),
                    new NumberStatDto(2, 7, 16.6667, new DateOnly(2026, 2, 15))
                },
                game == LotteryGame.Loto
                    ? new[] { new NumberStatDto(1, 8, 19.0476, new DateOnly(2026, 2, 18)) }
                    : new[]
                    {
                        new NumberStatDto(1, 9, 21.4286, new DateOnly(2026, 2, 18)),
                        new NumberStatDto(2, 5, 11.9048, new DateOnly(2026, 2, 17))
                    });

            return Task.FromResult(payload);
        }
    }

    private sealed class FakeGridGenerationService : IGridGenerationService
    {
        public Task<GenerateGridsResponseDto> GenerateAsync(
            LotteryGame game,
            int gridCount,
            GridGenerationStrategy strategy,
            CancellationToken cancellationToken)
        {
            var bonusCount = game == LotteryGame.Loto ? 1 : 2;
            var grids = Enumerable.Range(1, gridCount)
                .Select(index => new GeneratedGridDto(
                    new[] { index, index + 1, index + 2, index + 3, index + 4 },
                    Enumerable.Range(1, bonusCount).ToArray(),
                    50 + index,
                    new[] { index, index + 1 },
                    Enumerable.Range(1, bonusCount).ToArray()))
                .ToArray();

            var payload = new GenerateGridsResponseDto(
                DateTimeOffset.UtcNow,
                game.ToString(),
                strategy.ToApiValue(),
                "Fake disclaimer",
                1_000_000,
                grids,
                null);

            return Task.FromResult(payload);
        }
    }
}
