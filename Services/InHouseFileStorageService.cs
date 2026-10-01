using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HemFoundation.Services
{
    public class InHouseFileStorageService : IFileStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public InHouseFileStorageService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<FileUploadResult?> UploadAsync(
            IFormFile file,
            string folderPath)
        {
            if (file == null || file.Length <= 0)
                throw new ArgumentException("Invalid file.");

            string uploadUrl =
                _configuration["FileSettings:InHouseUploadUrl"]
                ?? throw new Exception("InHouseUploadUrl is not configured.");

            string apiKey =
                _configuration["FileSettings:ApiKey"]
                ?? throw new Exception("File API Key is not configured.");

            using var content = new MultipartFormDataContent();

            await using var fileStream = file.OpenReadStream();

            using var streamContent =
                new StreamContent(fileStream);

            streamContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    file.ContentType ?? "application/octet-stream");

            content.Add(
                streamContent,
                "file",
                file.FileName);

            content.Add(
                new StringContent(folderPath),
                "folderPath");

            var client = _httpClientFactory.CreateClient(
                "InHouseFileStorage");

            client.DefaultRequestHeaders.Remove("X-API-Key");

            client.DefaultRequestHeaders.Add(
                "X-API-Key",
                apiKey);

            var response = await client.PostAsync(
                uploadUrl,
                content);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync();

                throw new Exception(
                    $"File upload failed. {error}");
            }

            var result =
                await response.Content
                    .ReadFromJsonAsync<FileUploadResult>();

            if (result == null || !result.success)
            {
                throw new Exception(
                    result?.message ??
                    "In-house file server returned unsuccessful response.");
            }

            return result;
        }


        public async Task<FileDownloadResult?> DownloadAsync(
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            string downloadUrl =
                _configuration["FileSettings:InHouseDownloadUrl"]
                ?? throw new Exception(
                    "InHouseDownloadUrl is not configured.");

            string apiKey =
                _configuration["FileSettings:ApiKey"]
                ?? throw new Exception(
                    "File API Key is not configured.");

            var client = _httpClientFactory.CreateClient(
                "InHouseFileStorage");

            client.DefaultRequestHeaders.Remove("X-API-Key");

            client.DefaultRequestHeaders.Add(
                "X-API-Key",
                apiKey);

            var url =
                $"{downloadUrl}?filePath={Uri.EscapeDataString(filePath)}";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var bytes =
                await response.Content.ReadAsByteArrayAsync();

            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/octet-stream";

            var fileName =
                Path.GetFileName(filePath);

            return new FileDownloadResult
            {
                FileBytes = bytes,
                ContentType = contentType,
                FileName = fileName
            };
        }


        public async Task<bool> DeleteAsync(
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string deleteUrl =
                _configuration["FileSettings:InHouseDeleteUrl"]
                ?? throw new Exception(
                    "InHouseDeleteUrl is not configured.");

            string apiKey =
                _configuration["FileSettings:ApiKey"]
                ?? throw new Exception(
                    "File API Key is not configured.");

            var client = _httpClientFactory.CreateClient(
                "InHouseFileStorage");

            client.DefaultRequestHeaders.Remove("X-API-Key");

            client.DefaultRequestHeaders.Add(
                "X-API-Key",
                apiKey);

            var url =
                $"{deleteUrl}?filePath={Uri.EscapeDataString(filePath)}";

            var response = await client.DeleteAsync(url);

            return response.IsSuccessStatusCode;
        }
    }
}