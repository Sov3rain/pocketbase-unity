using System;
using Newtonsoft.Json;

namespace PocketBaseSdk
{
    /// <summary>
    /// Response DTO of a cron job item.
    /// </summary>
    [Serializable]
    public class CronJob
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("expression")]
        public string Expression { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}