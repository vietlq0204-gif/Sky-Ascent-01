using System.IO;

namespace Save.IO
{
    /// <summary>
    /// File handler dựa trên System.IO (POCO).
    /// </summary>
    public sealed class JsonFileHandlerNewtonsoft : IFileHandler
    {
        public void WriteAllText(string path, string content)
        {
            File.WriteAllText(path, content);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        public void EnsureDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        public void Move(string from, string to, bool overwrite)
        {
            if (!File.Exists(from)) return;

            if (overwrite && File.Exists(to))
                File.Delete(to);

            File.Move(from, to);
        }

        public string[] GetFiles(string folderPath, string searchPattern)
        {
            if (!Directory.Exists(folderPath)) return new string[0];
            return Directory.GetFiles(folderPath, searchPattern);
        }

        public bool DirectoryExists(string folderPath)
        {
            return Directory.Exists(folderPath);
        }

        public void DeleteDirectory(string folderPath, bool recursive)
        {
            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, recursive);
        }

        public void MoveDirectory(string from, string to)
        {
            if (!Directory.Exists(from)) return;
            if (Directory.Exists(to)) Directory.Delete(to, true);
            Directory.Move(from, to);
        }

        public void CreateDirectory(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
        }
    }
}


