namespace EventHub.Notifications.IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public EventHubDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<EventHubDbContext>().UseSqlServer(ConnectionString).Options);
}

[CollectionDefinition("NotificationsDatabase")]
public sealed class NotificationsDatabaseCollection : ICollectionFixture<DatabaseFixture> { }
