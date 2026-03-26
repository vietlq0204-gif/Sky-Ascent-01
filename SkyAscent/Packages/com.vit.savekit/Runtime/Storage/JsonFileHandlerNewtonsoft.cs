using System;
using System.IO;

namespace ViT.SaveKit.Storage
{
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

        public void EnsureDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        public string[] GetFiles(string folderPath, string searchPattern)
        {
            if (!Directory.Exists(folderPath))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(folderPath, searchPattern);
        }

        public bool DirectoryExists(string folderPath)
        {
            return Directory.Exists(folderPath);
        }

        public void DeleteDirectory(string folderPath, bool recursive)
        {
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, recursive);
            }
        }

        public void MoveDirectory(string from, string to)
        {
            if (!Directory.Exists(from))
            {
                return;
            }

            if (Directory.Exists(to))
            {
                Directory.Delete(to, true);
            }

            Directory.Move(from, to);
        }

        public void CreateDirectory(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
        }
    }
}
