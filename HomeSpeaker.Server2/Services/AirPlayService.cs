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

    public event EventHandler<AirPlayStatus>? StatusChanged;    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting AirPlay service...");
        
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Check if shairport-sync is available
        if (!await IsShairportSyncAvailable())
        {
            _logger.LogWarning("shairport-sync is not available. AirPlay functionality will be disabled.");
            return;
        }

        // Log audio devices for debugging
        await LogAudioDevices();
        
        // Start AirPlay in background - don't block application startup
        _ = Task.Run(async () =>
        {
            try
            {
                await StartShairportSync(_cancellationTokenSource.Token);
                _logger.LogInformation("AirPlay service started successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start AirPlay service. AirPlay functionality will be disabled, but the application will continue running.");
            }
        }, _cancellationTokenSource.Token);
        
        _logger.LogInformation("AirPlay service initialization completed (starting in background).");
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
    }    private async Task StartShairportSync(CancellationToken cancellationToken)
    {
        var deviceName = _configuration["AirPlay:DeviceName"] ?? "HomeSpeaker";
        var port = _configuration.GetValue<int>("AirPlay:Port", 5025);
        
        // Ensure Avahi daemon is running (critical for mDNS)
        await EnsureAvahiDaemonRunning();
        
        // Stop any existing shairport-sync instances to prevent conflicts
        await StopExistingShairportInstances();
        
        // First attempt: Try with ALSA backend
        if (await TryStartShairportWithConfig(deviceName, port, "alsa", cancellationToken))
        {
            return;
        }
        
        _logger.LogWarning("Failed to start shairport-sync with ALSA backend, trying alternative configurations...");
        
        // Second attempt: Try without explicit backend (let shairport-sync auto-detect)
        if (await TryStartShairportWithConfig(deviceName, port, null, cancellationToken))
        {
            return;
        }
        
        _logger.LogError("Failed to start shairport-sync with any configuration. AirPlay functionality will be disabled.");
        // Don't throw exception - let the application continue without AirPlay
    }
    
    private async Task<bool> TryStartShairportWithConfig(string deviceName, int port, string? audioBackend, CancellationToken cancellationToken)
    {
        try
        {
            // Build arguments based on available backend
            var arguments = $"--name \"{deviceName}\" --port {port} --verbose --metadata-pipename /tmp/shairport-sync-metadata";
            
            if (!string.IsNullOrEmpty(audioBackend))
            {
                arguments += $" --output {audioBackend}";
                if (audioBackend == "alsa")
                {
                    arguments += " -- -d default";
                }
            }
            
            _logger.LogInformation("Attempting to start shairport-sync with arguments: {Arguments}", arguments);
            
            _shairportProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "shairport-sync",
                    Arguments = arguments,
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

            // Wait a moment to see if the process exits immediately
            await Task.Delay(2000, cancellationToken);
            
            if (_shairportProcess.HasExited)
            {
                _logger.LogWarning("Shairport-sync process exited immediately with exit code: {ExitCode}", _shairportProcess.ExitCode);
                _shairportProcess.Dispose();
                _shairportProcess = null;
                return false;
            }

            _logger.LogInformation("Started shairport-sync with device name '{DeviceName}' on port {Port} using {Backend} backend", 
                deviceName, port, audioBackend ?? "auto-detected");

            // Start monitoring metadata
            _ = Task.Run(() => MonitorMetadata(cancellationToken), cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start shairport-sync with {Backend} backend", audioBackend ?? "auto-detected");
            _shairportProcess?.Dispose();
            _shairportProcess = null;
            return false;
        }
    }

    private void OnShairportOutput(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            _logger.LogDebug("Shairport output: {Output}", e.Data);
        }
    }    private void OnShairportError(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            // Log all error output for debugging
            _logger.LogWarning("Shairport stderr: {Error}", e.Data);
            
            // Check for critical error patterns that indicate real failures
            if (e.Data.Contains("*fatal error:"))
            {
                _logger.LogError("Fatal error detected: {Error}", e.Data);
            }
            else if (e.Data.Contains("couldn't create avahi client") || e.Data.Contains("Daemon not running"))
            {
                _logger.LogError("Avahi daemon error detected: {Error}", e.Data);
            }
            else if (e.Data.Contains("Could not establish mDNS advertisement"))
            {
                _logger.LogError("mDNS advertisement error detected: {Error}", e.Data);
            }
            else if (e.Data.Contains("address already in use") || e.Data.Contains("bind: Address already in use"))
            {
                _logger.LogError("Port already in use error detected: {Error}", e.Data);
            }
            else if (e.Data.Contains("permission denied") || e.Data.Contains("Permission denied"))
            {
                _logger.LogError("Permission error detected: {Error}", e.Data);
            }
            else if (e.Data.Contains("*error:"))
            {
                _logger.LogError("Shairport error detected: {Error}", e.Data);
            }
            else if (e.Data.Contains("*warning:"))
            {
                _logger.LogWarning("Shairport warning: {Error}", e.Data);
            }
            else
            {
                // Most stderr output from shairport-sync is just informational
                _logger.LogDebug("Shairport info: {Error}", e.Data);
            }
            
            // Parse connection events from stderr (only for actual connection messages)
            if (e.Data.Contains("Connection from") || e.Data.Contains("closed"))
            {
                ParseConnectionEvents(e.Data);
            }
        }
    }

    private void OnShairportExited(object? sender, EventArgs e)
    {
        if (_shairportProcess != null)
        {
            _logger.LogWarning("Shairport-sync process exited with exit code: {ExitCode}", _shairportProcess.ExitCode);

            // Log common exit codes and their meanings
            var exitCodeMeaning = _shairportProcess.ExitCode switch
            {
                1 => "General error (often audio configuration issues)",
                2 => "Permission denied or audio device access issues",
                3 => "Port already in use",
                127 => "Command not found",
                _ => "Unknown error"
            };

            _logger.LogError("Shairport-sync exit reason: {ExitCodeMeaning}", exitCodeMeaning);
        }
        else
        {
            _logger.LogWarning("Shairport-sync process exited unexpectedly (process was null)");
        }

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
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
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
    }    private async Task StopExistingShairportInstances()
    {
        try
        {
            // Kill any existing shairport-sync processes to prevent conflicts
            await RunCommand("pkill", "-f shairport-sync");
            _logger.LogInformation("Stopped existing shairport-sync instances");

            // Wait a moment for processes to clean up
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to stop existing shairport-sync instances (this is normal if none were running)");
        }
    }

    private async Task EnsureAvahiDaemonRunning()
    {
        try
        {
            // Check if avahi-daemon is running
            var (exitCode, output) = await RunCommand("pgrep", "avahi-daemon");
            if (exitCode == 0)
            {
                _logger.LogInformation("Avahi daemon is already running");
                return;
            }

            _logger.LogWarning("Avahi daemon is not running. Attempting to start it...");

            // Try to start avahi-daemon
            var (startExitCode, startOutput) = await RunCommand("avahi-daemon", "--daemonize");
            if (startExitCode == 0)
            {
                _logger.LogInformation("Successfully started Avahi daemon");
                // Wait for daemon to initialize
                await Task.Delay(2000);
            }
            else
            {
                _logger.LogError("Failed to start Avahi daemon. Exit code: {ExitCode}, Output: {Output}", startExitCode, startOutput);
                
                // Try alternative approach with systemctl if available
                var (systemctlExitCode, systemctlOutput) = await RunCommand("systemctl", "start avahi-daemon");
                if (systemctlExitCode == 0)
                {
                    _logger.LogInformation("Successfully started Avahi daemon via systemctl");
                    await Task.Delay(2000);
                }
                else
                {
                    _logger.LogWarning("Failed to start Avahi daemon via systemctl as well. mDNS functionality may not work.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring Avahi daemon is running. mDNS advertisement may fail.");
        }
    }

    private async Task LogAudioDevices()
    {
        try
        {
            // Check ALSA devices
            var (exitCode, output) = await RunCommand("aplay", "-l");
            if (exitCode == 0)
            {
                _logger.LogInformation("Available audio playback devices:\n{Output}", output);
            }
            else
            {
                _logger.LogWarning("Failed to list audio playback devices (exit code: {ExitCode})", exitCode);
            }

            // Check mixer controls
            var (exitCode2, output2) = await RunCommand("amixer", "scontrols");
            if (exitCode2 == 0)
            {
                _logger.LogInformation("Available mixer controls:\n{Output}", output2);
            }
            else
            {
                _logger.LogWarning("Failed to list mixer controls (exit code: {ExitCode})", exitCode2);
            }

            // Check if audio group membership exists
            var (exitCode3, output3) = await RunCommand("groups", "");
            if (exitCode3 == 0)
            {
                _logger.LogInformation("Current user groups: {Groups}", output3.Trim());
                if (!output3.Contains("audio"))
                {
                    _logger.LogWarning("Current user is not in 'audio' group. This may cause audio access issues.");
                }
            }

            // Test basic audio access
            var (exitCode4, output4) = await RunCommand("test", "-w /dev/snd");
            if (exitCode4 != 0)
            {
                _logger.LogWarning("Audio devices may not be writable. This could cause shairport-sync to fail.");
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log audio device information");
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _shairportProcess?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
