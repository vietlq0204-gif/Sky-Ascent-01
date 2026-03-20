using System;
using System.IO;
using Newtonsoft.Json;
using JsonFormatting = Newtonsoft.Json.Formatting;
using Save.Abstractions;

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

    /// <summary>
    /// SaveStore local dựa trên file JSON, có temp/backup để giảm rủi ro corrupt.
    /// </summary>
    public sealed class LocalJsonSaveStore : ISaveStore
    {
        private readonly IFolderProvider _folders;
        private readonly IFileHandler _files;

        public LocalJsonSaveStore(IFolderProvider folders, IFileHandler files)
        {
            _folders = folders;
            _files = files;
        }

        public void Save(string userId, SaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            string active = _folders.GetActiveSlotFolder(userId);
            string backup = _folders.GetBackupFolder(userId);

            _files.EnsureDirectory(_folders.GetUserRoot(userId));

            string tempId = Guid.NewGuid().ToString("N");
            string temp = _folders.GetTempFolder(userId, tempId);
            _files.CreateDirectory(temp);

            try
            {
                foreach (var kv in snapshot.Entries)
                {
                    var envelope = kv.Value;
                    if (envelope == null || string.IsNullOrWhiteSpace(envelope.key))
                        continue;

                    string json = JsonConvert.SerializeObject(envelope, JsonFormatting.Indented);
                    string filePath = Path.Combine(temp, $"{envelope.key}.json");
                    _files.WriteAllText(filePath, json);
                }

                if (_files.DirectoryExists(active))
                {
                    _files.MoveDirectory(active, backup);
                }

                _files.MoveDirectory(temp, active);
            }
            catch
            {
                try
                {
                    if (!_files.DirectoryExists(active) && _files.DirectoryExists(backup))
                        _files.MoveDirectory(backup, active);
                }
                catch
                {
                }

                try
                {
                    _files.DeleteDirectory(temp, true);
                }
                catch
                {
                }

                throw;
            }
        }

        public SaveSnapshot Load(string userId)
        {
            var snapshot = new SaveSnapshot();
            string active = _folders.GetActiveSlotFolder(userId);

            if (!_files.DirectoryExists(active))
                return snapshot;

            string[] files = _files.GetFiles(active, "*.json");
            if (files == null || files.Length == 0)
                return snapshot;

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string json = _files.ReadAllText(path);

                SaveEnvelope envelope;
                try
                {
                    envelope = JsonConvert.DeserializeObject<SaveEnvelope>(json);
                }
                catch
                {
                    continue;
                }

                if (envelope == null || string.IsNullOrWhiteSpace(envelope.key))
                    continue;

                snapshot.Entries[envelope.key] = envelope;
            }

            return snapshot;
        }
    }
}
