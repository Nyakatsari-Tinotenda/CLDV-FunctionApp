using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using ABCRetailFunctions.Models;

namespace ABCRetailFunctions.Functions
{
    public class BlobStorageFunction
    {
        private readonly ILogger<BlobStorageFunction> _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public BlobStorageFunction(ILogger<BlobStorageFunction> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnectionString");
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        [Function("UploadImage")]
        public async Task<IActionResult> UploadImage(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("UploadImage function triggered.");

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
                var file = formData.Files["imageFile"];

                if (file == null || file.Length == 0)
                {
                    return new BadRequestObjectResult(new UploadResponse
                    {
                        Success = false,
                        Message = "No file uploaded or file is empty."
                    });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return new BadRequestObjectResult(new UploadResponse
                    {
                        Success = false,
                        Message = "Invalid file type. Please upload JPG, PNG, GIF, BMP, or WebP images."
                    });
                }

                // Validate file size (10MB limit)
                if (file.Length > 10 * 1024 * 1024)
                {
                    return new BadRequestObjectResult(new UploadResponse
                    {
                        Success = false,
                        Message = "File size must be less than 10MB."
                    });
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                await containerClient.CreateIfNotExistsAsync();

                var blobName = $"{Guid.NewGuid()}_{file.FileName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, true);

                _logger.LogInformation($"Image {file.FileName} uploaded successfully. Blob URL: {blobClient.Uri}");

                return new OkObjectResult(new UploadResponse
                {
                    Success = true,
                    Message = $"Image {file.FileName} uploaded successfully.",
                    Url = blobClient.Uri.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image to blob storage.");
                return new BadRequestObjectResult(new UploadResponse
                {
                    Success = false,
                    Message = $"Error uploading image: {ex.Message}"
                });
            }
        }

        [Function("GetImages")]
        public async Task<IActionResult> GetImages(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("GetImages function triggered.");

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("product-images");
                await containerClient.CreateIfNotExistsAsync();

                var blobs = containerClient.GetBlobsAsync();
                var imageUrls = new List<string>();

                await foreach (var blob in blobs)
                {
                    imageUrls.Add(containerClient.GetBlobClient(blob.Name).Uri.ToString());
                }

                return new OkObjectResult(imageUrls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images.");
                return new BadRequestObjectResult(new UploadResponse
                {
                    Success = false,
                    Message = $"Error retrieving images: {ex.Message}"
                });
            }
        }
    }
}