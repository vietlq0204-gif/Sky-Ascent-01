using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ViT.SaveKit.Models
{
    [Serializable]
    public sealed class SaveEnvelope
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("payloadType")]
        public string PayloadType { get; set; }

        // Keep the old JSON field name to avoid breaking existing saves.
        [JsonProperty("Payload")]
        public JToken Payload { get; set; }
    }
}
