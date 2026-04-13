namespace Employeemanagementpractice.Services
{
    public interface IFileStorageService
    {
        Task<(byte[] data, string? ftpPath, Models.StorageType storageType)> SaveFileAsync(IFormFile file, string folder);
        Task<byte[]?> GetFileAsync(string? ftpPath, byte[]? dbData, Models.StorageType storageType);
        Task DeleteFileAsync(string? ftpPath, Models.StorageType storageType);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(IWebHostEnvironment env, IConfiguration config, ILogger<FileStorageService> logger)
        {
            _env = env;
            _config = config;
            _logger = logger;
        }

        public async Task<(byte[] data, string? ftpPath, Models.StorageType storageType)> SaveFileAsync(IFormFile file, string folder)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = ms.ToArray();

            // Try FTP first if configured
            var ftpEnabled = _config.GetValue<bool>("FileStorage:FtpEnabled");
            if (ftpEnabled)
            {
                try
                {
                    var ftpBasePath = _config["FileStorage:FtpBasePath"] ?? Path.Combine(_env.ContentRootPath, "FtpStorage");
                    var ftpFolder = Path.Combine(ftpBasePath, folder);
                    Directory.CreateDirectory(ftpFolder);

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var ftpPath = Path.Combine(ftpFolder, fileName);
                    await File.WriteAllBytesAsync(ftpPath, data);

                    _logger.LogInformation("File saved to FTP: {Path}", ftpPath);
                    return (data, ftpPath, Models.StorageType.FTP);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FTP save failed, falling back to database storage");
                }
            }

            // Fallback to database
            return (data, null, Models.StorageType.Database);
        }

        public async Task<byte[]?> GetFileAsync(string? ftpPath, byte[]? dbData, Models.StorageType storageType)
        {
            // Always try FTP first (faster reads)
            if (!string.IsNullOrEmpty(ftpPath) && File.Exists(ftpPath))
            {
                try
                {
                    return await File.ReadAllBytesAsync(ftpPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FTP read failed, falling back to database");
                }
            }

            // Fallback to database
            return dbData;
        }

        public Task DeleteFileAsync(string? ftpPath, Models.StorageType storageType)
        {
            if (storageType == Models.StorageType.FTP && !string.IsNullOrEmpty(ftpPath) && File.Exists(ftpPath))
            {
                try
                {
                    File.Delete(ftpPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FTP delete failed for: {Path}", ftpPath);
                }
            }
            return Task.CompletedTask;
        }
    }
}
