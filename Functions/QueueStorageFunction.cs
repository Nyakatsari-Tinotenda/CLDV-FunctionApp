using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues;
using ABCRetailFunctions.Models;

namespace ABCRetailFunctions.Functions
{
    public class QueueStorageFunction
    {
        private readonly ILogger<QueueStorageFunction> _logger;
        private readonly QueueServiceClient _queueServiceClient;

        public QueueStorageFunction(ILogger<QueueStorageFunction> logger)
        {
            _logger = logger;
            // Try multiple possible connection string names
            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnectionString")
                                ?? Environment.GetEnvironmentVariable("QueueConnectionString")
                                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _queueServiceClient = new QueueServiceClient(connectionString);
        }

        [Function("SendQueueMessage")]
        public async Task<IActionResult> SendQueueMessage(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("SendQueueMessage function triggered.");

            try
            {
                var formData = await req.ReadFormAsync();
                var message = formData["message"]!;

                var queueClient = _queueServiceClient.GetQueueClient("order-queue");
                await queueClient.CreateIfNotExistsAsync();

                // No need to base64 encode manually - the SDK handles this
                await queueClient.SendMessageAsync(message);

                _logger.LogInformation($"Queue message sent: {message}");

                return new OkObjectResult(new UploadResponse
                {
                    Success = true,
                    Message = $"Queue message sent: {message}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending queue message.");
                return new StatusCodeResult(500);
            }
        }
    }
}