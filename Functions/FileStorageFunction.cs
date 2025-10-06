using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Files.Shares;
using ABCRetailFunctions.Models;

namespace ABCRetailFunctions.Functions
{
    public class FileStorageFunction
    {
        private readonly ILogger<FileStorageFunction> _logger;
        private readonly ShareServiceClient _fileServiceClient;

        public FileStorageFunction(ILogger<FileStorageFunction> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnectionString");
            _fileServiceClient = new ShareServiceClient(connectionString);
        }

        [Function("UploadContract")]
        public async Task<IActionResult> UploadContract(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("UploadContract function triggered.");

            try
            {
                // Check if the request contains form data
                if (!req.HasFormContentType)
                {
                    return new BadRequestObjectResult(new UploadResponse
                    {
                        Success = false,
                        Message = "Invalid content type. Expected form data."
                    });
                }

                var formData = await req.ReadFormAsync();
                var file = formData.Files["contractFile"];

                if (file == null || file.Length == 0)
                {
                    return new BadRequestObjectResult(new UploadResponse
                    {
                        Success = false,
                        Message = "No file uploaded or file is empty."
                    });
                }

                // Validate file type
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".xls", ".ppt", ".pptx" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return new BadRequestObjectResult(new UploadResponse
                    {
                        Success = false,
                        Message = "Invalid file type. Please upload PDF, Word, Excel, PowerPoint, or Text files."
                    });
                }

                // Validate file size (100MB limit)
                if (file.Length > 100 * 1024 * 1024)
                {
                    return new BadRequestObjectResult(new UploadResponse
                    {
                        Success = false,
                        Message = "File size must be less than 100MB."
                    });
                }

                var shareClient = _fileServiceClient.GetShareClient("contracts");
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetRootDirectoryClient();
                var fileClient = directoryClient.GetFileClient(file.FileName);

                using var stream = file.OpenReadStream();
                await fileClient.CreateAsync(stream.Length);
                await fileClient.UploadAsync(stream);

                _logger.LogInformation($"Contract {file.FileName} uploaded successfully.");

                return new OkObjectResult(new UploadResponse
                {
                    Success = true,
                    Message = $"Contract {file.FileName} uploaded successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading contract to file storage.");
                return new BadRequestObjectResult(new UploadResponse
                {
                    Success = false,
                    Message = $"Error uploading contract: {ex.Message}"
                });
            }
        }

        [Function("GetContracts")]
        public async Task<IActionResult> GetContracts(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("GetContracts function triggered.");

            try
            {
                var shareClient = _fileServiceClient.GetShareClient("contracts");
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetRootDirectoryClient();
                var files = new List<string>();

                await foreach (var fileItem in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!fileItem.IsDirectory)
                    {
                        files.Add(fileItem.Name);
                    }
                }

                return new OkObjectResult(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contracts.");
                return new BadRequestObjectResult(new UploadResponse
                {
                    Success = false,
                    Message = $"Error retrieving contracts: {ex.Message}"
                });
            }
        }
    }
}