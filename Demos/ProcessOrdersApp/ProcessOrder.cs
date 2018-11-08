using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Rest;
using ProcessOrdersApp.Models;

namespace ProcessOrdersApp
{
    public static class ProcessOrder
    {
        #region Private Data Members

        private static readonly string EventGridKey = Environment.GetEnvironmentVariable("EventGridKey");
        private static readonly string EventGridTopicHostname =
            Environment.GetEnvironmentVariable("EventGridTopicHostname");

        #endregion

        [FunctionName("ProcessOrder")]
        //public static void Run(
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
        }

        #region Private Methods

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
