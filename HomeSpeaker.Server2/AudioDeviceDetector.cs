using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HomeSpeaker.Server2;

/// <summary>
/// Detects available audio devices and selects the best one for playback.
/// Priority order: USB audio devices → Headphones jack → Built-in/HDMI speakers
/// </summary>
public class AudioDeviceDetector
{
    private readonly ILogger<AudioDeviceDetector> logger;
    private string? selectedCard;
    private string? selectedMixerControl;

    public AudioDeviceDetector(ILogger<AudioDeviceDetector> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// The ALSA card name/number to use for playback (e.g., "Headphones", "0", "UACDemoV10")
    /// </summary>
    public string? SelectedCard => selectedCard;

    /// <summary>
    /// The mixer control name to use for volume (e.g., "PCM", "Master", "Speaker")
    /// </summary>
    public string? SelectedMixerControl => selectedMixerControl;

    /// <summary>
    /// Detects available audio devices and selects the best one.
    /// If ALSA_CARD environment variable is set, uses that instead of auto-detection.
    /// </summary>
    public async Task DetectAndSelectDeviceAsync()
    {
        logger.LogInformation("Starting audio device detection...");

        // Check for environment variable override
        var envCard = Environment.GetEnvironmentVariable("ALSA_CARD");
        if (!string.IsNullOrWhiteSpace(envCard))
        {
            logger.LogInformation("Using ALSA_CARD environment variable: {Card}", envCard);
            selectedCard = envCard;
            selectedMixerControl = await detectMixerControlAsync(envCard);
            logger.LogInformation("Selected mixer control: {MixerControl}", selectedMixerControl ?? "(none found)");
            return;
        }

        var devices = await getAvailableDevicesAsync();

        if (devices.Count == 0)
        {
            logger.LogWarning("No audio devices found!");
            return;
        }

        foreach (var device in devices)
        {
            logger.LogInformation("Found audio device: Card {CardNumber} [{CardName}]: {Description}",
                device.CardNumber, device.CardName, device.Description);
        }

        // Priority selection:
        // 1. "Headphones" (Raspberry Pi headphone jack - most reliable)
        // 2. USB audio devices (external DACs or speakers)
        // 3. Any device with "Speaker" in the name (built-in screen speakers)
        // 4. HDMI audio
        // 5. First available device

        var selected = devices
            .OrderByDescending(d => d.CardName.Equals("Headphones", StringComparison.OrdinalIgnoreCase) ? 100 : 0)
            .ThenByDescending(d => d.IsUsb ? 50 : 0)
            .ThenByDescending(d => d.Description.Contains("Speaker", StringComparison.OrdinalIgnoreCase) ? 40 : 0)
            .ThenByDescending(d => d.Description.Contains("HDMI", StringComparison.OrdinalIgnoreCase) ? 10 : 0)
            .ThenBy(d => d.CardNumber)
            .First();

        selectedCard = selected.CardName;
        logger.LogInformation("Selected audio device: Card {CardNumber} [{CardName}]: {Description}",
            selected.CardNumber, selected.CardName, selected.Description);

        // Detect the appropriate mixer control for this card
        selectedMixerControl = await detectMixerControlAsync(selected.CardName);
        logger.LogInformation("Selected mixer control: {MixerControl}", selectedMixerControl ?? "(none found)");
    }

    private async Task<List<AudioDevice>> getAvailableDevicesAsync()
    {
        var devices = new List<AudioDevice>();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "aplay",
                    Arguments = "-l",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse output like:
            // card 0: Headphones [bcm2835 Headphones], device 0: bcm2835 Headphones [bcm2835 Headphones]
            // card 1: UACDemoV10 [UACDemoV1.0], device 0: USB Audio [USB Audio]
            var cardRegex = new Regex(@"card (\d+): (\w+) \[([^\]]+)\]", RegexOptions.Multiline);
            var matches = cardRegex.Matches(output);

            foreach (Match match in matches)
            {
                var cardNumber = int.Parse(match.Groups[1].Value);
                var cardName = match.Groups[2].Value;
                var description = match.Groups[3].Value;

                // Skip duplicates (aplay -l shows each card multiple times for each device)
                if (devices.Any(d => d.CardNumber == cardNumber))
                {
                    continue;
                }

                devices.Add(new AudioDevice
                {
                    CardNumber = cardNumber,
                    CardName = cardName,
                    Description = description,
                    IsUsb = description.Contains("USB", StringComparison.OrdinalIgnoreCase) ||
                            cardName.Contains("USB", StringComparison.OrdinalIgnoreCase) ||
                            cardName.StartsWith("UAC", StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enumerate audio devices");
        }

        return devices;
    }

    private async Task<string?> detectMixerControlAsync(string cardName)
    {
        // Try common mixer control names in order of preference
        var controlsToTry = new[] { "PCM", "Master", "Speaker", "Headphone", "Digital" };

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "amixer",
                    Arguments = $"-c {cardName} scontrols",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            logger.LogDebug("Available mixer controls for card {Card}: {Output}", cardName, output);

            // Parse output like:
            // Simple mixer control 'PCM',0
            // Simple mixer control 'Master',0
            foreach (var control in controlsToTry)
            {
                if (output.Contains($"'{control}'", StringComparison.OrdinalIgnoreCase))
                {
                    return control;
                }
            }

            // If none of the preferred controls found, try to extract the first one
            var controlRegex = new Regex(@"Simple mixer control '([^']+)'");
            var match = controlRegex.Match(output);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to detect mixer controls for card {Card}", cardName);
        }

        return null;
    }

    private sealed class AudioDevice
    {
        public int CardNumber { get; set; }
        public string CardName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsUsb { get; set; }
    }
}