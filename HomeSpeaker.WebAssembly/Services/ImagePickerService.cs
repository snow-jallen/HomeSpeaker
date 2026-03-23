using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HomeSpeaker.WebAssembly.Services;

public class ImagePickerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImagePickerService> _logger;

    public ImagePickerService(HttpClient httpClient, ILogger<ImagePickerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ImageSearchResult>> SearchImagesAsync(string query)
    {
        try
        {
            var results = await _httpClient.GetFromJsonAsync<List<ImageSearchResult>>(
                $"/api/streams/image-search?q={Uri.EscapeDataString(query)}");
            return results ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image search failed for query: {Query}", query);
            return [];
        }
    }

    public async Task<string?> UploadImageAsync(byte[] fileBytes, string fileName, string contentType)
    {
        try
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/api/streams/upload-image", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<UploadErrorResponse>();
                _logger.LogWarning("Upload failed: {Error}", error?.Error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<UploadSuccessResponse>();
            return result?.Filename;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upload failed");
            return null;
        }
    }

    private record UploadErrorResponse(string Error);
    private record UploadSuccessResponse(string Filename);
}
