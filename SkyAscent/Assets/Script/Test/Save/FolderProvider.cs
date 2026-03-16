using System.IO;

namespace Save.IO
{

    /// <summary>
    /// Xây đường dẫn save theo user/slot.
    /// </summary>
    public interface IFolderProvider
    {
        string GetUserRoot(string userId);
        string GetActiveSlotFolder(string userId);
        string GetTempFolder(string userId, string tempId);
        string GetBackupFolder(string userId);
    }


    /// <summary>
    /// Folder provider: mỗi user có 1 root, dùng 1 slot "active".
    /// </summary>
    /// <remarks>
    /// - Không parse tên folder phức tạp.
    /// - Có temp và backup để transaction.
    /// </remarks>
    public sealed class FolderProvider : IFolderProvider
    {
        private readonly string _baseRoot;

        /// <summary>
        /// </summary>
        /// <param name="baseRoot">vd: Application.persistentDataPath</param>
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
            return Path.Combine(GetUserRoot(userId), $"temp_{tempId}");
        }

        public string GetBackupFolder(string userId)
        {
            return Path.Combine(GetUserRoot(userId), "backup_last");
        }

        /// <summary>
        /// Sanitize để tránh ký tự phá path.
        /// </summary>
        private string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "unknown";
            foreach (var c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input;
        }
    }
}
