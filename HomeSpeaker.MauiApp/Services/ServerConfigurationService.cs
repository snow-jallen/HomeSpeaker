namespace HomeSpeaker.MauiApp.Services;

public class ServerConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Nickname { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public interface IServerConfigurationService
{
    Task<List<ServerConfiguration>> GetServersAsync();

    Task AddServerAsync(ServerConfiguration server);

    Task UpdateServerAsync(ServerConfiguration server);

    Task DeleteServerAsync(string serverId);

    Task<ServerConfiguration?> GetDefaultServerAsync();
}

public class ServerConfigurationService : IServerConfigurationService
{
    private readonly string _configFilePath;
    private List<ServerConfiguration> _servers = new();

    public ServerConfigurationService()
    {
        _configFilePath = Path.Combine(FileSystem.AppDataDirectory, "servers.json");
        LoadServersAsync().Wait();
    }

    private async Task LoadServersAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                _servers = System.Text.Json.JsonSerializer.Deserialize<List<ServerConfiguration>>(json) ?? new();
            }
        }
        catch
        {
            _servers = new();
        }
    }

    private async Task SaveServersAsync()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_servers, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_configFilePath, json);
    }

    public Task<List<ServerConfiguration>> GetServersAsync()
    {
        return Task.FromResult(_servers);
    }

    public async Task AddServerAsync(ServerConfiguration server)
    {
        _servers.Add(server);
        await SaveServersAsync();
    }

    public async Task UpdateServerAsync(ServerConfiguration server)
    {
        var existing = _servers.FirstOrDefault(s => s.Id == server.Id);
        if (existing != null)
        {
            existing.Nickname = server.Nickname;
            existing.ServerUrl = server.ServerUrl;
            existing.IsDefault = server.IsDefault;
            await SaveServersAsync();
        }
    }

    public async Task DeleteServerAsync(string serverId)
    {
        _servers.RemoveAll(s => s.Id == serverId);
        await SaveServersAsync();
    }

    public Task<ServerConfiguration?> GetDefaultServerAsync()
    {
        return Task.FromResult(_servers.FirstOrDefault(s => s.IsDefault));
    }
}
