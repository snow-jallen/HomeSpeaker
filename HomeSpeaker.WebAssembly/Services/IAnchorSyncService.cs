using HomeSpeaker.Shared;

namespace HomeSpeaker.WebAssembly.Services;

public interface IAnchorSyncService : IDisposable
{
    Task StartAsync();
    Task StopAsync();
    bool IsConnected { get; }
    
    event Action<AnchorDefinition>? OnAnchorDefinitionCreated;
    event Action<AnchorDefinition>? OnAnchorDefinitionUpdated;
    event Action<int>? OnAnchorDefinitionDeactivated;
    event Action<UserAnchor>? OnUserAnchorAssigned;
    event Action<string, int>? OnUserAnchorRemoved;
    event Action<int, bool, DateTime?>? OnDailyAnchorCompletionUpdated;
}