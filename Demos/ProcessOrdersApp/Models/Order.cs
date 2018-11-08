using Newtonsoft.Json;

namespace ProcessOrdersApp.Models
{
    public class Order
    {
        #region Properties

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        #endregion
    }
}
