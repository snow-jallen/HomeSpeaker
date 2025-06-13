using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server;

public interface IAirPlayService
{
    AirPlayStatus CurrentStatus { get; }
    event EventHandler<AirPlayStatus> StatusChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public class AirPlayService : IAirPlayService, IDisposable
{
    private readonly ILogger<AirPlayService> _logger;
    private readonly IConfiguration _configuration;
    private Process? _shairportProcess;
    private AirPlayStatus _currentStatus = new();
    private readonly object _statusLock = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public AirPlayService(ILogger<AirPlayService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public AirPlayStatus CurrentStatus
    {
        get
        {
            lock (_statusLock)
            {
                return _currentStatus;
            }
        }
        private set
        {
            lock (_statusLock)
            {
                var previous = _currentStatus;
                _currentStatus = value;
                if (!previous.Equals(value))
                {
                    StatusChanged?.Invoke(this, value);
                }
            }
        }
    }

    public event EventHandler<AirPlayStatus>? StatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting AirPlay service...");
        
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Check if shairport-sync is available
        if (!await IsShairportSyncAvailable())
        {
            _logger.LogWarning("shairport-sync is not available. AirPlay functionality will be disabled.");
            return;
        }

        await StartShairportSync(_cancellationTokenSource.Token);
        _logger.LogInformation("AirPlay service started successfully.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping AirPlay service...");
        
        _cancellationTokenSource?.Cancel();
        
        if (_shairportProcess != null && !_shairportProcess.HasExited)
        {
            _shairportProcess.Kill();
            await _shairportProcess.WaitForExitAsync(cancellationToken);
        }

        CurrentStatus = new AirPlayStatus();
        _logger.LogInformation("AirPlay service stopped.");
    }

    private async Task<bool> IsShairportSyncAvailable()
    {
        try
        {
            var result = await RunCommand("which", "shairport-sync");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task StartShairportSync(CancellationToken cancellationToken)
    {
        var deviceName = _configuration["AirPlay:DeviceName"] ?? "HomeSpeaker";
        var port = _configuration.GetValue<int>("AirPlay:Port", 5000);
        
        _shairportProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "shairport-sync",
                Arguments = $"--name \"{deviceName}\" --port {port} --metadata-pipename /tmp/shairport-sync-metadata --verbose",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _shairportProcess.OutputDataReceived += OnShairportOutput;
        _shairportProcess.ErrorDataReceived += OnShairportError;
        _shairportProcess.Exited += OnShairportExited;
        _shairportProcess.EnableRaisingEvents = true;

        _shairportProcess.Start();
        _shairportProcess.BeginOutputReadLine();
        _shairportProcess.BeginErrorReadLine();

        _logger.LogInformation("Started shairport-sync with device name '{DeviceName}' on port {Port}", deviceName, port);

        // Start monitoring metadata
        _ = Task.Run(() => MonitorMetadata(cancellationToken), cancellationToken);
    }

    private void OnShairportOutput(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            _logger.LogDebug("Shairport output: {Output}", e.Data);
        }
    }

    private void OnShairportError(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            _logger.LogDebug("Shairport error: {Error}", e.Data);
            
            // Parse connection events from stderr
            ParseConnectionEvents(e.Data);
        }
    }

    private void OnShairportExited(object? sender, EventArgs e)
    {
        _logger.LogWarning("Shairport-sync process exited unexpectedly");
        CurrentStatus = new AirPlayStatus();
    }

    private void ParseConnectionEvents(string logLine)
    {
        // Parse log lines for connection/disconnection events
        // Example: "Connection from [192.168.1.100]:49152."
        // Example: "Connection from [fe80::1%en0]:49152 closed."
        
        var connectionMatch = Regex.Match(logLine, @"Connection from \[([^\]]+)\]:(\d+)");
        if (connectionMatch.Success)
        {
            var ipAddress = connectionMatch.Groups[1].Value;
            _logger.LogInformation("AirPlay client connected from {IpAddress}", ipAddress);
            
            CurrentStatus = CurrentStatus with
            {
                IsConnected = true,
                ClientIpAddress = ipAddress,
                ConnectedAt = DateTime.Now,
                DeviceName = "Unknown Device" // Will be updated from metadata if available
            };
            return;
        }

        var disconnectionMatch = Regex.Match(logLine, @"Connection from .+ closed");
        if (disconnectionMatch.Success)
        {
            _logger.LogInformation("AirPlay client disconnected");
            
            CurrentStatus = new AirPlayStatus();
            return;
        }
    }

    private async Task MonitorMetadata(CancellationToken cancellationToken)
    {
        const string metadataPipe = "/tmp/shairport-sync-metadata";
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(metadataPipe))
                {
                    using var reader = new StreamReader(metadataPipe);
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                    {
                        ParseMetadata(line);
                    }
                }
                else
                {
                    // Wait for the pipe to be created
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error monitoring AirPlay metadata");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private void ParseMetadata(string metadataLine)
    {
        try
        {
            // Parse shairport-sync metadata format
            // This is a simplified parser - actual format is more complex
            if (metadataLine.Contains("\"daid\"") && CurrentStatus.IsConnected)
            {
                // Try to extract device name from metadata
                var deviceNameMatch = Regex.Match(metadataLine, @"""([^""]+)""");
                if (deviceNameMatch.Success)
                {
                    var deviceName = deviceNameMatch.Groups[1].Value;
                    if (!string.IsNullOrEmpty(deviceName) && deviceName != CurrentStatus.DeviceName)
                    {
                        CurrentStatus = CurrentStatus with { DeviceName = deviceName };
                        _logger.LogInformation("Updated AirPlay device name to: {DeviceName}", deviceName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error parsing metadata line: {Line}", metadataLine);
        }
    }

    private async Task<(int ExitCode, string Output)> RunCommand(string command, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _shairportProcess?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
