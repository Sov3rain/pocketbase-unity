using System;
using Newtonsoft.Json;

namespace PocketBaseSdk
{
    [Serializable]

    public class RecordSubscriptionEvent<T>
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("record")]
        public T Record { get; set; }
    }
}