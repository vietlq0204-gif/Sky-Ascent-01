namespace ViT.SaveKit.Abstractions
{
    public interface ISaveable
    {
        string Key { get; }
        int Version { get; }
        bool ShouldSave { get; }

        void BeforeSave();
        object Capture();
        void Restore(object data, int version);
        void AfterLoad();
    }
}
