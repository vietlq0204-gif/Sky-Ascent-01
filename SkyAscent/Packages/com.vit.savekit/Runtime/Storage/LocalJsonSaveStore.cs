using System;
using System.IO;
using Newtonsoft.Json;
using JsonFormatting = Newtonsoft.Json.Formatting;
using ViT.SaveKit.Models;

namespace ViT.SaveKit.Storage
{
    public sealed class LocalJsonSaveStore : ISaveStore
    {
        private readonly IFolderProvider _folders;
        private readonly IFileHandler _files;

        public LocalJsonSaveStore(IFolderProvider folders, IFileHandler files)
        {
            _folders = folders ?? throw new ArgumentNullException(nameof(folders));
            _files = files ?? throw new ArgumentNullException(nameof(files));
        }

        public void Save(string userId, SaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            string active = _folders.GetActiveSlotFolder(userId);
            string backup = _folders.GetBackupFolder(userId);

            _files.EnsureDirectory(_folders.GetUserRoot(userId));

            string tempId = Guid.NewGuid().ToString("N");
            string temp = _folders.GetTempFolder(userId, tempId);
            _files.CreateDirectory(temp);

            try
            {
                foreach (var entry in snapshot.Entries)
                {
                    SaveEnvelope envelope = entry.Value;
                    if (envelope == null || string.IsNullOrWhiteSpace(envelope.Key))
                    {
                        continue;
                    }

                    string json = JsonConvert.SerializeObject(envelope, JsonFormatting.Indented);
                    string filePath = Path.Combine(temp, envelope.Key + ".json");
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
                    {
                        _files.MoveDirectory(backup, active);
                    }
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
            {
                return snapshot;
            }

            string[] files = _files.GetFiles(active, "*.json");
            if (files.Length == 0)
            {
                return snapshot;
            }

            foreach (string path in files)
            {
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

                if (envelope == null || string.IsNullOrWhiteSpace(envelope.Key))
                {
                    continue;
                }

                snapshot.Entries[envelope.Key] = envelope;
            }

            return snapshot;
        }
    }
}
