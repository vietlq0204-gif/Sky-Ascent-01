using System.Collections.Generic;

namespace ViT.SaveKit.Models
{
    public sealed class SaveSnapshot
    {
        public Dictionary<string, SaveEnvelope> Entries { get; } = new Dictionary<string, SaveEnvelope>(64);
    }
}
