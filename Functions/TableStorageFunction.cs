using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using ABCRetailFunctions.Models;

namespace ABCRetailFunctions.Functions
{
    public class TableStorageFunction
    {
        private readonly ILogger<TableStorageFunction> _logger;
        private readonly TableServiceClient _tableServiceClient;

        public TableStorageFunction(ILogger<TableStorageFunction> logger)
        {
            _logger = logger;
            // Try multiple possible connection string names
            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnectionString")
                                ?? Environment.GetEnvironmentVariable("TableConnectionString")
                                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _tableServiceClient = new TableServiceClient(connectionString);
        }

        [Function("AddCustomer")]
        public async Task<IActionResult> AddCustomer(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("AddCustomer function triggered.");

            try
            {
                var formData = await req.ReadFormAsync();

                var customer = new CustomerProfile
                {
                    PartitionKey = "customers",
                    RowKey = Guid.NewGuid().ToString(),
                    Name = formData["name"]!,
                    Email = formData["email"]!,
                    Phone = formData["phone"]!
                };

                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync(customer);

                _logger.LogInformation($"Customer {customer.Name} added successfully.");

                return new OkObjectResult(new UploadResponse
                {
                    Success = true,
                    Message = $"Customer {customer.Name} added successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding customer to table storage.");
                return new StatusCodeResult(500);
            }
        }

        [Function("GetCustomers")]
        public async Task<IActionResult> GetCustomers(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("GetCustomers function triggered.");

            try
            {
                var tableClient = _tableServiceClient.GetTableClient("customerprofiles");
                await tableClient.CreateIfNotExistsAsync();

                var customers = tableClient.Query<CustomerProfile>().ToList();

                return new OkObjectResult(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers.");
                return new StatusCodeResult(500);
            }
        }
    }
}