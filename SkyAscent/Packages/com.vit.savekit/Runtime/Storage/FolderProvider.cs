using System.IO;

namespace ViT.SaveKit.Storage
{
    public sealed class FolderProvider : IFolderProvider
    {
        private readonly string _baseRoot;

        public FolderProvider(string baseRoot)
        {
            _baseRoot = baseRoot;
        }

        public string GetUserRoot(string userId)
        {
            return Path.Combine(_baseRoot, "Save", Sanitize(userId));
        }

        public string GetActiveSlotFolder(string userId)
        {
            return Path.Combine(GetUserRoot(userId), "slot_active");
        }

        public string GetTempFolder(string userId, string tempId)
        {
            return Path.Combine(GetUserRoot(userId), "temp_" + tempId);
        }

        public string GetBackupFolder(string userId)
        {
            return Path.Combine(GetUserRoot(userId), "backup_last");
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "unknown";
            }

            foreach (char character in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(character, '_');
            }

            return input;
        }
    }
}
