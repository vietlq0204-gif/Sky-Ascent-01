using Newtonsoft.Json.Linq;
using System;

namespace Save.Abstractions
{
    /// <summary>
    /// Wrapper metadata cho từng file save.
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public string key;
        public int version;

        /// <summary>
        /// Full name type của DTO để deserialize.
        /// </summary>
        public string payloadType;

        /// <summary>
        /// Payload serialized json của DTO.
        /// </summary>
        public JToken payload;

    }
}
