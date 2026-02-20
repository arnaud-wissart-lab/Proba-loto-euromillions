using System.Net;
using System.Net.Http.Json;
using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace IntegrationTests;

[Collection("api-postgres")]
public sealed class ApiPostgresIntegrationTests : IClassFixture<ApiPostgresIntegrationTests.ApiPostgresFactory>
{
    private const string AdminApiKey = "integration-admin-key";
    private readonly ApiPostgresFactory _factory;

    public ApiPostgresIntegrationTests(ApiPostgresFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStatusShouldReturnDatabaseValues()
    {
        var client = await CreateClientAsync();
        await ResetAndSeedAsync(dbContext =>
        {
            dbContext.Draws.AddRange(
                CreateDraw(LotteryGame.Loto, new DateOnly(2026, 2, 17), [1, 2, 3, 4, 5], [6]),
                CreateDraw(LotteryGame.Loto, new DateOnly(2026, 2, 19), [5, 6, 7, 8, 9], [10]),
                CreateDraw(LotteryGame.EuroMillions, new DateOnly(2026, 2, 18), [1, 2, 3, 4, 5], [1, 2]));

            dbContext.SyncStates.AddRange(
                new SyncStateEntity
                {
                    Game = LotteryGame.Loto,
                    LastSuccessfulSyncAtUtc = new DateTimeOffset(2026, 2, 19, 10, 0, 0, TimeSpan.Zero),
                    LastKnownDrawDate = new DateOnly(2026, 2, 19)
                },
                new SyncStateEntity
                {
                    Game = LotteryGame.EuroMillions,
                    LastSuccessfulSyncAtUtc = new DateTimeOffset(2026, 2, 19, 9, 30, 0, TimeSpan.Zero),
                    LastKnownDrawDate = new DateOnly(2026, 2, 18)
                });
        });

        var response = await client.GetAsync("/api/status");
        var payload = await response.Content.ReadFromJsonAsync<StatusDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Loto.DrawsCount);
        Assert.Equal(1, payload.EuroMillions.DrawsCount);
        Assert.Equal(new DateOnly(2026, 2, 19), payload.Loto.LastDrawDate);
        Assert.Equal(new DateOnly(2026, 2, 18), payload.EuroMillions.LastDrawDate);
        Assert.Equal(new DateTimeOffset(2026, 2, 19, 10, 0, 0, TimeSpan.Zero), payload.LastSyncAt);
    }

    [Fact]
    public async Task GetStatsShouldReturnComputedAggregates()
    {
        var client = await CreateClientAsync();
        await ResetAndSeedAsync(dbContext =>
        {
            dbContext.Draws.AddRange(
                CreateDraw(LotteryGame.Loto, new DateOnly(2026, 2, 15), [1, 2, 3, 4, 5], [6]),
                CreateDraw(LotteryGame.Loto, new DateOnly(2026, 2, 18), [1, 8, 9, 10, 11], [6]));
        });

        var response = await client.GetAsync("/api/stats/Loto");
        var payload = await response.Content.ReadFromJsonAsync<GameStatsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Loto", payload.Game);
        Assert.Equal(2, payload.TotalDraws);
        Assert.Contains(payload.MainStats, item => item.Number == 1 && item.Occurrences == 2);
        Assert.Contains(payload.BonusStats, item => item.Number == 6 && item.Occurrences == 2);
    }

    [Fact]
    public async Task PostGenerateShouldReturnRequestedNumberOfGrids()
    {
        var client = await CreateClientAsync();
        await ResetAndSeedAsync(dbContext =>
        {
            dbContext.Draws.AddRange(
                CreateDraw(LotteryGame.Loto, new DateOnly(2026, 2, 15), [1, 2, 3, 4, 5], [6]),
                CreateDraw(LotteryGame.Loto, new DateOnly(2026, 2, 18), [1, 8, 9, 10, 11], [6]),
                CreateDraw(LotteryGame.Loto, new DateOnly(2026, 2, 19), [2, 3, 12, 13, 14], [2]));
        });

        var request = new GenerateGridsRequestDto("Loto", 6, "frequency");

        var response = await client.PostAsJsonAsync("/api/grids/generate", request);
        var payload = await response.Content.ReadFromJsonAsync<GenerateGridsResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(6, payload.Grids.Count);
        Assert.Equal(payload.Grids.Count, payload.Grids.Select(grid => string.Join('-', grid.MainNumbers) + "|" + string.Join('-', grid.BonusNumbers)).Distinct().Count());
    }

    [Fact]
    public async Task GetAdminSyncRunsShouldRequireApiKey()
    {
        var client = await CreateClientAsync();
        await ResetAndSeedAsync(_ => { });

        var response = await client.GetAsync("/api/admin/sync-runs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminSyncRunsShouldReturnLatestRunsWithApiKey()
    {
        var client = await CreateClientAsync();
        await ResetAndSeedAsync(dbContext =>
        {
            dbContext.SyncRuns.AddRange(
                new SyncRunEntity
                {
                    Game = LotteryGame.Loto,
                    Status = SyncRunStatus.Success,
                    StartedAtUtc = new DateTimeOffset(2026, 2, 19, 8, 0, 0, TimeSpan.Zero),
                    FinishedAtUtc = new DateTimeOffset(2026, 2, 19, 8, 1, 0, TimeSpan.Zero),
                    DrawsUpsertedCount = 12
                },
                new SyncRunEntity
                {
                    Game = LotteryGame.EuroMillions,
                    Status = SyncRunStatus.Fail,
                    StartedAtUtc = new DateTimeOffset(2026, 2, 19, 10, 0, 0, TimeSpan.Zero),
                    FinishedAtUtc = new DateTimeOffset(2026, 2, 19, 10, 2, 0, TimeSpan.Zero),
                    DrawsUpsertedCount = 0,
                    Error = "Erreur test"
                });
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/sync-runs?take=10");
        request.Headers.Add("X-Api-Key", AdminApiKey);

        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<AdminSyncRunDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Count);
        Assert.Equal("EuroMillions", payload.First().Game);
        Assert.Equal("Fail", payload.First().Status);
    }

    [Fact]
    public async Task PostAdminSyncShouldReturnSummaryWithApiKey()
    {
        var client = await CreateClientAsync();
        await ResetAndSeedAsync(_ => { });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/sync");
        request.Headers.Add("X-Api-Key", AdminApiKey);

        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<SyncExecutionSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Games.Count);
        Assert.All(payload.Games, item => Assert.Equal(SyncRunStatus.Success, item.Status));
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        await _factory.EnsureStartedAsync();
        return _factory.CreateClient();
    }

    private async Task ResetAndSeedAsync(Action<LotteryDbContext> seed)
    {
        await _factory.EnsureStartedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LotteryDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """TRUNCATE TABLE draws, email_send_logs, subscriptions, sync_runs, sync_state RESTART IDENTITY CASCADE;""");

        seed(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private static DrawEntity CreateDraw(
        LotteryGame game,
        DateOnly drawDate,
        int[] mainNumbers,
        int[] bonusNumbers) =>
        new()
        {
            Game = game,
            DrawDate = drawDate,
            MainNumbers = mainNumbers,
            BonusNumbers = bonusNumbers,
            Source = "integration-test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    public sealed class ApiPostgresFactory : WebApplicationFactory<Program>
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("probabilites_tests")
            .WithUsername("probaloto")
            .WithPassword("probaloto")
            .Build();
        private readonly SemaphoreSlim _startLock = new(1, 1);
        private bool _started;

        public async Task EnsureStartedAsync()
        {
            if (_started)
            {
                return;
            }

            await _startLock.WaitAsync();
            try
            {
                if (_started)
                {
                    return;
                }

                await _postgres.StartAsync();

                Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
                Environment.SetEnvironmentVariable("Database__AutoMigrate", "true");
                Environment.SetEnvironmentVariable("Admin__ApiKey", AdminApiKey);
                Environment.SetEnvironmentVariable("Subscriptions__PublicBaseUrl", "http://localhost:8080");
                Environment.SetEnvironmentVariable("Subscriptions__TokenSecret", "integration-secret");
                Environment.SetEnvironmentVariable("Smtp__Host", "localhost");
                Environment.SetEnvironmentVariable("Smtp__Port", "2525");
                Environment.SetEnvironmentVariable("Smtp__UseStartTls", "false");
                Environment.SetEnvironmentVariable("HealthChecks__Smtp__Enabled", "false");

                _started = true;
            }
            finally
            {
                _startLock.Release();
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDrawSyncService>();
                services.AddScoped<IDrawSyncService, FakeDrawSyncService>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
                Environment.SetEnvironmentVariable("Database__AutoMigrate", null);
                Environment.SetEnvironmentVariable("Admin__ApiKey", null);
                Environment.SetEnvironmentVariable("Subscriptions__PublicBaseUrl", null);
                Environment.SetEnvironmentVariable("Subscriptions__TokenSecret", null);
                Environment.SetEnvironmentVariable("Smtp__Host", null);
                Environment.SetEnvironmentVariable("Smtp__Port", null);
                Environment.SetEnvironmentVariable("Smtp__UseStartTls", null);
                Environment.SetEnvironmentVariable("HealthChecks__Smtp__Enabled", null);

                _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _startLock.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class FakeDrawSyncService : IDrawSyncService
    {
        public Task<SyncExecutionSummaryDto> SyncAllAsync(string trigger, CancellationToken cancellationToken)
        {
            var startedAtUtc = new DateTimeOffset(2026, 2, 19, 10, 0, 0, TimeSpan.Zero);
            var finishedAtUtc = startedAtUtc.AddSeconds(5);
            var games = new[]
            {
                new GameSyncResultDto(
                    LotteryGame.Loto,
                    SyncRunStatus.Success,
                    3,
                    startedAtUtc,
                    finishedAtUtc,
                    new DateOnly(2026, 2, 19),
                    null),
                new GameSyncResultDto(
                    LotteryGame.EuroMillions,
                    SyncRunStatus.Success,
                    2,
                    startedAtUtc,
                    finishedAtUtc,
                    new DateOnly(2026, 2, 18),
                    null)
            };

            return Task.FromResult(new SyncExecutionSummaryDto(startedAtUtc, finishedAtUtc, games));
        }

        public Task<GameSyncResultDto> SyncGameAsync(LotteryGame game, string trigger, CancellationToken cancellationToken)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            var result = new GameSyncResultDto(
                game,
                SyncRunStatus.Success,
                0,
                startedAtUtc,
                startedAtUtc.AddSeconds(1),
                null,
                null);
            return Task.FromResult(result);
        }
    }
}

[CollectionDefinition("api-postgres", DisableParallelization = true)]
public sealed class ApiPostgresTestGroup;
