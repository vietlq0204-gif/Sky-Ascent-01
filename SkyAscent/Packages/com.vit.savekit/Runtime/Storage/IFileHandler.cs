namespace ViT.SaveKit.Storage
{
    public interface IFileHandler
    {
        void WriteAllText(string path, string content);
        string ReadAllText(string path);
        void EnsureDirectory(string folderPath);
        string[] GetFiles(string folderPath, string searchPattern);
        bool DirectoryExists(string folderPath);
        void DeleteDirectory(string folderPath, bool recursive);
        void MoveDirectory(string from, string to);
        void CreateDirectory(string folderPath);
    }
}
