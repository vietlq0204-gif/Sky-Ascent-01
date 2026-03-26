using ViT.SaveKit.Abstractions;

namespace ViT.SaveKit.Runtime
{
    public interface ISaveSystem
    {
        void Register(ISaveable saveable);
        void Unregister(ISaveable saveable);
        void SaveAll();
        void LoadAll();
    }
}
