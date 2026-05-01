using System.Diagnostics.CodeAnalysis;

namespace HomeSpeaker.Server2.Services;

public class AiMusicOptions
{
    public OpenAiOptions OpenAI { get; set; } = new();
    public AzureOpenAiOptions AzureOpenAI { get; set; } = new();
    public ProcessingOptions Processing { get; set; } = new();
    public string AnalysisVersion { get; set; } = "2026-05-01-v1";

    public bool HasOpenAIConfiguration => !string.IsNullOrWhiteSpace(OpenAI.ApiKey);
    public bool UseAzureOpenAI => AzureOpenAI.IsConfigured;
    public bool HasConfiguredProvider => UseAzureOpenAI || HasOpenAIConfiguration;
    public bool HasApiKey => HasConfiguredProvider;
    public string ConfiguredModelId => UseAzureOpenAI ? AzureOpenAI.DeploymentName! : OpenAI.ChatModel;
    public string? ConfigurationIssue => getConfigurationIssue();

    public class OpenAiOptions
    {
        public string? ApiKey { get; set; }
        public string ChatModel { get; set; } = "gpt-4o-mini";
    }

    public class AzureOpenAiOptions
    {
        public string? Endpoint { get; set; }
        public string? ApiKey { get; set; }
        public string? DeploymentName { get; set; }

        [MemberNotNullWhen(true, nameof(ApiKey), nameof(DeploymentName))]
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(DeploymentName) &&
            TryGetEndpointUri(out _);

        public bool HasAnyValue =>
            !string.IsNullOrWhiteSpace(Endpoint) ||
            !string.IsNullOrWhiteSpace(ApiKey) ||
            !string.IsNullOrWhiteSpace(DeploymentName);

        public bool TryGetEndpointUri([NotNullWhen(true)] out Uri? endpointUri) =>
            Uri.TryCreate(Endpoint, UriKind.Absolute, out endpointUri);
    }

    public class ProcessingOptions
    {
        public bool Enabled { get; set; } = true;
        public int BatchSize { get; set; } = 12;
        public int MaxParallelBatches { get; set; } = 1;
        public int ScanIntervalMinutes { get; set; } = 30;
        public int StaleLeaseMinutes { get; set; } = 10;
    }

    private string? getConfigurationIssue()
    {
        if (HasConfiguredProvider)
        {
            return null;
        }

        if (AzureOpenAI.HasAnyValue)
        {
            var missingSettings = new List<string>();
            if (string.IsNullOrWhiteSpace(AzureOpenAI.Endpoint))
            {
                missingSettings.Add("AI:AzureOpenAI:Endpoint");
            }
            else if (!AzureOpenAI.TryGetEndpointUri(out _))
            {
                missingSettings.Add("AI:AzureOpenAI:Endpoint (absolute URI)");
            }

            if (string.IsNullOrWhiteSpace(AzureOpenAI.ApiKey))
            {
                missingSettings.Add("AI:AzureOpenAI:ApiKey");
            }

            if (string.IsNullOrWhiteSpace(AzureOpenAI.DeploymentName))
            {
                missingSettings.Add("AI:AzureOpenAI:DeploymentName");
            }

            return $"Azure OpenAI is not fully configured. Set {string.Join(", ", missingSettings)}.";
        }

        return "AI provider is not configured. Set AI:OpenAI:ApiKey or AI:AzureOpenAI:Endpoint, ApiKey, and DeploymentName.";
    }
}
