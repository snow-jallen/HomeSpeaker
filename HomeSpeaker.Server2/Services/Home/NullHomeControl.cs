namespace HomeSpeaker.Server2.Services.Home;

public sealed class NullHomeControl : IHomeControl
{
    public bool IsAvailable => false;

    public event Action? TilesChanged
    {
        add { }
        remove { }
    }

    public Task<IReadOnlyList<HomeTile>> GetTilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HomeTile>>([]);

    public Task InvokeAsync(string tileId, HomeAction action, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
