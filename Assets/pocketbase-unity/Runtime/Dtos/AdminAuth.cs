using System;
using Newtonsoft.Json;

namespace PocketBaseSdk
{
    [Serializable]

    public sealed class AdminAuth
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("admin")]
        public AdminModel Admin { get; set; }
    }
}