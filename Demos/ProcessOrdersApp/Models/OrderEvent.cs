using System;
using Newtonsoft.Json;

namespace ProcessOrdersApp.Models
{
    public class OrderEvent
    {
        #region Properties

        [JsonProperty("orderid")]
        public Guid OrderId { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        #endregion
    }
}
