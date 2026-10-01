using Microsoft.AspNetCore.Http;

namespace HemFoundation.Services
{
    public interface IFileStorageService
    {
        Task<FileUploadResult?> UploadAsync(
            IFormFile file,
            string folderPath);

        Task<FileDownloadResult?> DownloadAsync(
            string filePath);

        Task<bool> DeleteAsync(
            string filePath);
    }

    public class FileUploadResult
    {
        public bool success { get; set; }

        public string filePath { get; set; } = "";

        public string fileName { get; set; } = "";

        public string message { get; set; } = "";
    }

    public class FileDownloadResult
    {
        public byte[] FileBytes { get; set; } = [];

        public string ContentType { get; set; } = "application/octet-stream";

        public string FileName { get; set; } = "";
    }
}