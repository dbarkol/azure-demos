using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Rest;
using ProcessOrdersApp.Models;

namespace ProcessOrdersApp
{
    public static class ProcessOrder
    {
        #region Private Data Members

        private static EventHubClient HubClient = null;
        private static readonly string EventGridKey = Environment.GetEnvironmentVariable("EventGridKey");
        private static readonly string EventGridTopicHostname =
            Environment.GetEnvironmentVariable("EventGridTopicHostname");

        #endregion

        [FunctionName("ProcessOrder")]
        public static async Task Run(
            [ServiceBusTrigger("orders", Connection = "ServiceBusConnectionString")]string order,
            [CosmosDB(
                databaseName: "ordersDatabase",
                collectionName: "ordersCollection",
                ConnectionStringSetting = "CosmosDBConnectionString")] IAsyncCollector<OrderEvent> documents,
            ILogger log)
        {
            // Retrieve the order details from the queue
            var orderDetails = JsonConvert.DeserializeObject<Order>(order);
            log.LogInformation($"Order received: {orderDetails.Sku} - {orderDetails.Quantity}");

            // Create an order event. We will use this to publish to 
            // event grid as well as record in Cosmos DB.            
            var orderEvent = new OrderEvent
            {
                OrderId = Guid.NewGuid(),  
                Sku = orderDetails.Sku,
                Quantity = orderDetails.Quantity
            };

            // Publish an event that the order has been received
            await PublishOrderEvent(orderEvent);

            // Create a new document in Cosmos with the order details
            await documents.AddAsync(orderEvent);

            // Send to Event Hubs
            await SendToEventHubs(orderEvent);
        }

        #region Private Methods

        private static async Task SendToEventHubs(OrderEvent orderEvent)
        {
            // Check to see if we have an event hubs client already available
            // and create one if necessary.
            if (HubClient == null)
            {
                var connectionString = await GetEhConnectionString();
                HubClient = EventHubClient.CreateFromConnectionString(connectionString);
            }

            // Sent to event hubs
            await HubClient.SendAsync(new EventData(Encoding.UTF8.GetBytes(orderEvent.ToString())));            
        }

        private static async Task<string> GetEhConnectionString()
        {
            // A better configuration would be to enable a 
            // managed service identity (MSI) for this function app.
            // This approach is an alternative for local development and testing.
            // Reference: https://docs.microsoft.com/en-us/azure/app-service/app-service-managed-service-identity#creating-an-app-with-an-identity

            // Retrieve the necessary credentials for the appplication to access
            // the secret from Key Vault.
            var applicationId = Environment.GetEnvironmentVariable("ApplicationId");
            var applicationSecret = Environment.GetEnvironmentVariable("ApplicationSecret");
            var vaultUrl = Environment.GetEnvironmentVariable("VaultUrl");
            var secretName = Environment.GetEnvironmentVariable("SecretName");

            // Authenticate with Key Vault
            var keyClient = new KeyVaultClient(async (authority, resource, scope) =>
            {
                var adCredential = new ClientCredential(applicationId, applicationSecret);
                var authenticationContext = new AuthenticationContext(authority, null);
                return (await authenticationContext.AcquireTokenAsync(resource, adCredential)).AccessToken;
            });

            // Retrieve the secret
            var secretValue = await keyClient.GetSecretAsync(vaultUrl, secretName);
            return secretValue.Value;
        }

        private static async Task PublishOrderEvent(OrderEvent orderEvent)
        {
            // Initialize the event grid client with the access key
            ServiceClientCredentials credentials = new TopicCredentials(EventGridKey);
            var client = new EventGridClient(credentials);

            // Events are always sent to grid in an array, when using the
            // event grid schema (cloud events are sent one at a time). 
            var events = new List<EventGridEvent>
            {
                new EventGridEvent()
                {
                    Id = Guid.NewGuid().ToString(),
                    Data = orderEvent,
                    EventTime = DateTime.UtcNow,
                    EventType = orderEvent.Quantity > 1000 ? "NewOrder.Large" : "NewOrder.Regular",
                    Subject = "demo/orders/new",
                    DataVersion = "1.0"
                }
            };

            // Send the event
            await client.PublishEventsAsync(EventGridTopicHostname, events);
        }

        #endregion
    }


}
