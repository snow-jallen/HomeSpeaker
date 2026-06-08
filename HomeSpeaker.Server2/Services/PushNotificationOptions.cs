using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace HomeSpeaker.Server2.Services;

public class PushNotificationOptions
{
    public bool Enabled { get; set; } = true;
    public int RepeatAlertMinutes { get; set; } = 30;

    /// <summary>
    /// How often the background worker polls the health monitors to evaluate alerts.
    /// Clamped to a 30 second minimum at runtime.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 120;
    public ApnsOptions Apns { get; set; } = new();

    public bool CanSend => Enabled && Apns.IsConfigured;

    public class ApnsOptions
    {
        public string? TeamId { get; set; }
        public string? KeyId { get; set; }
        public string? Topic { get; set; }
        public bool UseSandbox { get; set; } = true;
        public string? PrivateKeyPath { get; set; }
        public string? PrivateKeyBase64 { get; set; }

        [MemberNotNullWhen(true, nameof(TeamId), nameof(KeyId), nameof(Topic))]
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(TeamId) &&
            !string.IsNullOrWhiteSpace(KeyId) &&
            !string.IsNullOrWhiteSpace(Topic) &&
            HasPrivateKeyMaterial;

        public bool HasPrivateKeyMaterial =>
            !string.IsNullOrWhiteSpace(PrivateKeyPath) ||
            !string.IsNullOrWhiteSpace(PrivateKeyBase64);

        public bool TryGetPrivateKeyPem([NotNullWhen(true)] out string? pem)
        {
            pem = null;

            if (!string.IsNullOrWhiteSpace(PrivateKeyPath))
            {
                if (!File.Exists(PrivateKeyPath))
                {
                    return false;
                }

                pem = File.ReadAllText(PrivateKeyPath, Encoding.UTF8);
                return !string.IsNullOrWhiteSpace(pem);
            }

            if (string.IsNullOrWhiteSpace(PrivateKeyBase64))
            {
                return false;
            }

            try
            {
                pem = Encoding.UTF8.GetString(Convert.FromBase64String(PrivateKeyBase64));
                return !string.IsNullOrWhiteSpace(pem);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
