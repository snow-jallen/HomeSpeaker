using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HomeSpeaker.WebAssembly.Services;

public class ImagePickerService
{
    private readonly HttpClient httpClient;
    private readonly ILogger<ImagePickerService> logger;

    public ImagePickerService(HttpClient httpClient, ILogger<ImagePickerService> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<List<ImageSearchResult>> SearchImagesAsync(string query)
    {
        try
        {
            var results = await httpClient.GetFromJsonAsync<List<ImageSearchResult>>(
                $"/api/streams/image-search?q={Uri.EscapeDataString(query)}");
            return results ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image search failed for query: {Query}", query);
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

            var response = await httpClient.PostAsync("/api/streams/upload-image", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<UploadErrorResponse>();
                logger.LogWarning("Upload failed: {Error}", error?.Error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<UploadSuccessResponse>();
            return result?.Filename;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Upload failed");
            return null;
        }
    }

    private record UploadErrorResponse(string Error);
    private record UploadSuccessResponse(string Filename);
}
