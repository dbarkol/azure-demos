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
        public static void Run(
            [ServiceBusTrigger("orders", Connection = "ServiceBusConnectionString")]string order, 
            ILogger log)
        {
            // Retrieve the order details from the message 
            var orderDetails = JsonConvert.DeserializeObject<Order>(order);
            log.LogInformation($"Order received: {orderDetails.Sku} - {orderDetails.Quantity}");

            // Publish an event that the order has been received
            PublishOrderEvent(orderDetails).GetAwaiter().GetResult();
        }

        #region Private Methods

        private static async Task PublishOrderEvent(Order orderDetails)
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
                    Data = new OrderEvent
                    {
                        OrderId = Guid.NewGuid(),
                        Sku = orderDetails.Sku,
                        Quantity = orderDetails.Quantity
                    },
                    EventTime = DateTime.UtcNow,
                    EventType = orderDetails.Quantity > 1000 ? "NewOrder.Large" : "NewOrder.Regular",
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
