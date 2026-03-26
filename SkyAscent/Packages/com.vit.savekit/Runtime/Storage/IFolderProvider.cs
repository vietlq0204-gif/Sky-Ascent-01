namespace ViT.SaveKit.Storage
{
    public interface IFolderProvider
    {
        string GetUserRoot(string userId);
        string GetActiveSlotFolder(string userId);
        string GetTempFolder(string userId, string tempId);
        string GetBackupFolder(string userId);
    }
}
