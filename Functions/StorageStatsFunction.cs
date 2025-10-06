using ABCRetailFunctions.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class StorageStatsFunction
    {
        private readonly ILogger<StorageStatsFunction> _logger;

        public StorageStatsFunction(ILogger<StorageStatsFunction> logger)
        {
            _logger = logger;
        }

        [Function("GetStorageStats")]
        public async Task<IActionResult> GetStorageStats(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("GetStorageStats function triggered.");

            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnectionString");

                // Get customer count from Table Storage
                var tableServiceClient = new TableServiceClient(connectionString);
                var tableClient = tableServiceClient.GetTableClient("customerprofiles");
                await tableClient.CreateIfNotExistsAsync();
                var customers = tableClient.Query<CustomerProfile>().ToList();

                // Get image count from Blob Storage
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("product-images");
                await containerClient.CreateIfNotExistsAsync();
                var imageCount = 0;
                await foreach (var blob in containerClient.GetBlobsAsync()) imageCount++;

                // Get queue message count
                var queueServiceClient = new QueueServiceClient(connectionString);
                var queueClient = queueServiceClient.GetQueueClient("order-queue");
                await queueClient.CreateIfNotExistsAsync();
                var queueProperties = await queueClient.GetPropertiesAsync();
                var queueCount = queueProperties.Value.ApproximateMessagesCount;

                // Get contract count from File Storage
                var fileServiceClient = new ShareServiceClient(connectionString);
                var shareClient = fileServiceClient.GetShareClient("contracts");
                await shareClient.CreateIfNotExistsAsync();
                var contractCount = 0;
                var directoryClient = shareClient.GetRootDirectoryClient();
                await foreach (var fileItem in directoryClient.GetFilesAndDirectoriesAsync())
                {
                    if (!fileItem.IsDirectory) contractCount++;
                }

                var stats = new
                {
                    CustomerCount = customers.Count,
                    ImageCount = imageCount,
                    QueueMessageCount = queueCount,
                    ContractCount = contractCount,
                    LastUpdated = DateTime.UtcNow
                };

                return new OkObjectResult(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics.");
                return new BadRequestObjectResult(new { Error = ex.Message });
            }
        }
    }
}