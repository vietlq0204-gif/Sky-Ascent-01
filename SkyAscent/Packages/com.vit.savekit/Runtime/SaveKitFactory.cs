using ViT.SaveKit.Abstractions;
using ViT.SaveKit.Storage;

namespace ViT.SaveKit.Runtime
{
    public static class SaveKitFactory
    {
        public const string DefaultGuestUserId = "guest_001";

        public static ISaveSystem CreateLocalJson(string baseRoot)
        {
            return CreateLocalJson(new DefaultAccountContext(DefaultGuestUserId), baseRoot);
        }

        public static ISaveSystem CreateLocalJson(string baseRoot, params ISaveable[] saveables)
        {
            return CreateLocalJson(new DefaultAccountContext(DefaultGuestUserId), baseRoot, saveables);
        }

        public static ISaveSystem CreateLocalJson(IAccountContext accountContext, string baseRoot)
        {
            IFolderProvider folders = new FolderProvider(baseRoot);
            IFileHandler files = new JsonFileHandlerNewtonsoft();
            ISaveStore store = new LocalJsonSaveStore(folders, files);
            return new SaveSystem(accountContext, store);
        }

        public static ISaveSystem CreateLocalJson(
            IAccountContext accountContext,
            string baseRoot,
            params ISaveable[] saveables)
        {
            ISaveSystem saveSystem = CreateLocalJson(accountContext, baseRoot);
            RegisterAll(saveSystem, saveables);
            return saveSystem;
        }

        private static void RegisterAll(ISaveSystem saveSystem, ISaveable[] saveables)
        {
            if (saveSystem == null || saveables == null || saveables.Length == 0)
            {
                return;
            }

            foreach (ISaveable saveable in saveables)
            {
                saveSystem.Register(saveable);
            }
        }

        private sealed class DefaultAccountContext : IAccountContext
        {
            public DefaultAccountContext(string userId)
            {
                UserId = userId;
            }

            public string UserId { get; }
        }
    }
}
