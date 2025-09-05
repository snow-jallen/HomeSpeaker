using HomeSpeaker.Shared;
using System.Text.Json;

namespace HomeSpeaker.WebAssembly.Services;

public interface IAnchorService
{
    // Anchor Definition Management
    Task<IEnumerable<AnchorDefinition>> GetAnchorDefinitionsAsync();
    Task<AnchorDefinition> CreateAnchorDefinitionAsync(CreateAnchorDefinitionRequest request);
    Task<AnchorDefinition?> UpdateAnchorDefinitionAsync(int id, CreateAnchorDefinitionRequest request);
    Task<bool> DeactivateAnchorDefinitionAsync(int id);

    // User Anchor Management
    Task<IEnumerable<UserAnchor>> GetUserAnchorsAsync(string userId);
    Task<UserAnchor> AssignAnchorToUserAsync(AssignAnchorToUserRequest request);
    Task<bool> RemoveAnchorFromUserAsync(string userId, int anchorDefinitionId);

    // Daily Anchor Management
    Task<IEnumerable<DailyAnchor>> GetDailyAnchorsAsync(string userId, DateOnly date);    Task<IEnumerable<DailyAnchor>> GetDailyAnchorsRangeAsync(string userId, DateOnly? startDate = null, DateOnly? endDate = null);
    Task CreateDailyAnchorsAsync(string userId, DateOnly date);
    Task<bool> UpdateAnchorCompletionAsync(UpdateAnchorCompletionRequest request);
    Task EnsureTodayAnchorsAsync();

    // Multi-user methods
    Task<IEnumerable<string>> GetUsersWithAnchorsAsync();
    Task<Dictionary<string, List<DailyAnchor>>> GetAllUsersDailyAnchorsAsync(DateOnly? startDate = null, DateOnly? endDate = null);
}

public class AnchorService : IAnchorService
{
    private readonly HttpClient httpClient;
    private readonly ILogger<AnchorService> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public AnchorService(HttpClient httpClient, ILogger<AnchorService> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new DateOnlyJsonConverter() }
        };
    }

    // Anchor Definition Management
    public async Task<IEnumerable<AnchorDefinition>> GetAnchorDefinitionsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("/api/anchors/definitions");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<AnchorDefinition>>(json, jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get anchor definitions");
            return [];
        }
    }

    public async Task<AnchorDefinition> CreateAnchorDefinitionAsync(CreateAnchorDefinitionRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/anchors/definitions", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AnchorDefinition>(responseJson, jsonOptions)!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create anchor definition");
            throw;
        }
    }

    public async Task<AnchorDefinition?> UpdateAnchorDefinitionAsync(int id, CreateAnchorDefinitionRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync($"/api/anchors/definitions/{id}", content);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
                
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AnchorDefinition>(responseJson, jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update anchor definition {Id}", id);
            throw;
        }
    }

    public async Task<bool> DeactivateAnchorDefinitionAsync(int id)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"/api/anchors/definitions/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deactivate anchor definition {Id}", id);
            return false;
        }
    }

    // User Anchor Management
    public async Task<IEnumerable<UserAnchor>> GetUserAnchorsAsync(string userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/anchors/users/{Uri.EscapeDataString(userId)}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<UserAnchor>>(json, jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get user anchors for {UserId}", userId);
            return [];
        }
    }

    public async Task<UserAnchor> AssignAnchorToUserAsync(AssignAnchorToUserRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/anchors/users", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserAnchor>(responseJson, jsonOptions)!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to assign anchor to user");
            throw;
        }
    }

    public async Task<bool> RemoveAnchorFromUserAsync(string userId, int anchorDefinitionId)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"/api/anchors/users/{Uri.EscapeDataString(userId)}/{anchorDefinitionId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove anchor from user {UserId}", userId);
            return false;
        }
    }

    // Daily Anchor Management
    public async Task<IEnumerable<DailyAnchor>> GetDailyAnchorsAsync(string userId, DateOnly date)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/anchors/daily/{Uri.EscapeDataString(userId)}/{date:yyyy-MM-dd}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<DailyAnchor>>(json, jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get daily anchors for {UserId} on {Date}", userId, date);
            return [];
        }
    }

    public async Task<IEnumerable<DailyAnchor>> GetDailyAnchorsRangeAsync(string userId, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        try
        {
            var queryString = string.Empty;
            if (startDate.HasValue || endDate.HasValue)
            {
                var queryParams = new List<string>();
                if (startDate.HasValue)
                    queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                if (endDate.HasValue)
                    queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
                queryString = "?" + string.Join("&", queryParams);
            }

            var response = await httpClient.GetAsync($"/api/anchors/daily/{Uri.EscapeDataString(userId)}{queryString}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<DailyAnchor>>(json, jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get daily anchors range for {UserId}", userId);
            return [];
        }
    }

    public async Task CreateDailyAnchorsAsync(string userId, DateOnly date)
    {
        try
        {
            var response = await httpClient.PostAsync($"/api/anchors/daily/create/{Uri.EscapeDataString(userId)}/{date:yyyy-MM-dd}", null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create daily anchors for {UserId} on {Date}", userId, date);
            throw;
        }
    }

    public async Task<bool> UpdateAnchorCompletionAsync(UpdateAnchorCompletionRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync("/api/anchors/daily/completion", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update anchor completion");
            return false;
        }
    }

    public async Task EnsureTodayAnchorsAsync()
    {
        try
        {
            var response = await httpClient.PostAsync("/api/anchors/daily/ensure-today", null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure today's anchors");
            throw;        }
    }

    // Multi-user methods
    public async Task<IEnumerable<string>> GetUsersWithAnchorsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("/api/anchors/users");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<IEnumerable<string>>(json, jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get users with anchors");
            return [];
        }
    }

    public async Task<Dictionary<string, List<DailyAnchor>>> GetAllUsersDailyAnchorsAsync(DateOnly? startDate = null, DateOnly? endDate = null)
    {
        try
        {
            var queryString = string.Empty;
            if (startDate.HasValue || endDate.HasValue)
            {
                var queryParams = new List<string>();
                if (startDate.HasValue)
                    queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                if (endDate.HasValue)
                    queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
                queryString = "?" + string.Join("&", queryParams);
            }

            var response = await httpClient.GetAsync($"/api/anchors/daily{queryString}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Dictionary<string, List<DailyAnchor>>>(json, jsonOptions) ?? new Dictionary<string, List<DailyAnchor>>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all users' daily anchors");
            return new Dictionary<string, List<DailyAnchor>>();
        }
    }
}

// Custom DateOnly JSON converter for serialization
public class DateOnlyJsonConverter : System.Text.Json.Serialization.JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
    }
}
