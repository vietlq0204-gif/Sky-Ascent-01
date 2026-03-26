using ViT.SaveKit.Models;

namespace ViT.SaveKit.Storage
{
    public interface ISaveStore
    {
        void Save(string userId, SaveSnapshot snapshot);
        SaveSnapshot Load(string userId);
    }
}
