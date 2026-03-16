using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using JsonFormatting = Newtonsoft.Json.Formatting;
using Save.Abstractions;
using Save.IO;
using Newtonsoft.Json.Linq;

namespace Save.Core
{
    /// <summary>
    /// SaveSystem: orchestrate Save/Load theo user, transaction temp/backup.
    /// </summary>
    /// <remarks>
    /// - Không singleton.
    /// - Dùng DI inject: accountContext + folderProvider + fileHandler.
    /// - Registry ISaveable bằng Dictionary để lookup O(1).
    /// </remarks>
    public sealed class SaveSystem : ISaveSystem
    {
        private readonly IAccountContext _account;
        private readonly IFolderProvider _folders;
        private readonly IFileHandler _files;

        private readonly Dictionary<string, ISaveable> _saveablesByKey = new Dictionary<string, ISaveable>(64);

        #region Contructor

        public SaveSystem(IAccountContext account, IFolderProvider folders, IFileHandler files)
        {
            _account = account;
            _folders = folders;
            _files = files;
        }

        #endregion


        /// <summary>
        /// Register module saveable.
        /// </summary>
        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;

            if (string.IsNullOrWhiteSpace(saveable.Key))
                throw new ArgumentException("ISaveable.Key must not be empty.");

            if (_saveablesByKey.ContainsKey(saveable.Key))
                throw new InvalidOperationException($"Duplicate save key: {saveable.Key}");

            _saveablesByKey.Add(saveable.Key, saveable);
        }

        /// <summary>
        /// Unregister module saveable.
        /// </summary>
        public void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;
            _saveablesByKey.Remove(saveable.Key);
        }

        /// <summary>
        /// Save tất cả module (transaction temp -> active + backup rollback).
        /// </summary>
        public void SaveAll()
        {
            string userId = _account.UserId;
            string active = _folders.GetActiveSlotFolder(userId);
            string backup = _folders.GetBackupFolder(userId);

            _files.EnsureDirectory(_folders.GetUserRoot(userId));

            // 1) temp folder
            string tempId = Guid.NewGuid().ToString("N");
            string temp = _folders.GetTempFolder(userId, tempId);
            _files.CreateDirectory(temp);

            try
            {
                // 2) write all json to temp
                foreach (var kv in _saveablesByKey)
                {
                    var saveable = kv.Value;
                    if (saveable == null) continue;
                    if (!saveable.ShouldSave) continue;

                    saveable.BeforeSave();

                    object dto = saveable.Capture();
                    if (dto == null) continue;

                    var env = BuildEnvelope(saveable, dto);
                    string json = JsonConvert.SerializeObject(env, JsonFormatting.Indented);

                    string filePath = Path.Combine(temp, $"{saveable.Key}.json");
                    _files.WriteAllText(filePath, json);
                }

                // 3) backup active -> backup
                if (_files.DirectoryExists(active))
                {
                    _files.MoveDirectory(active, backup);
                }

                // 4) move temp -> active
                _files.MoveDirectory(temp, active);

                // 5) cleanup backup (optional)
                // _files.DeleteDirectory(backup, true);
            }
            catch
            {
                // rollback best-effort
                try
                {
                    // restore backup -> active if active missing
                    if (!_files.DirectoryExists(active) && _files.DirectoryExists(backup))
                        _files.MoveDirectory(backup, active);
                }
                catch { /* swallow rollback errors */ }

                // cleanup temp
                try { _files.DeleteDirectory(temp, true); } catch { }

                throw;
            }
        }

        /// <summary>
        /// Load tất cả module từ slot active (nếu có).
        /// </summary>
        public void LoadAll()
        {
            string userId = _account.UserId;
            string active = _folders.GetActiveSlotFolder(userId);

            if (!_files.DirectoryExists(active))
                return;

            string[] files = _files.GetFiles(active, "*.json");
            if (files == null || files.Length == 0)
                return;

            // 1) restore data
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string json = _files.ReadAllText(path);

                SaveEnvelope env;
                try
                {
                    env = JsonConvert.DeserializeObject<SaveEnvelope>(json);
                }
                catch
                {
                    // file corrupt -> skip
                    continue;
                }

                if (env == null || string.IsNullOrWhiteSpace(env.key))
                    continue;

                if (!_saveablesByKey.TryGetValue(env.key, out var saveable) || saveable == null)
                    continue;

                object dto = DeserializePayload(env);
                if (dto == null) continue;

                saveable.Restore(dto, env.version);
            }

            // 2) after load hooks
            foreach (var kv in _saveablesByKey)
            {
                kv.Value?.AfterLoad();
            }
        }

        /// <summary>
        /// Build envelope: store payload as json string + payload type.
        /// </summary>
        private SaveEnvelope BuildEnvelope(ISaveable saveable, object dto)
        {
            return new SaveEnvelope
            {
                key = saveable.Key,
                version = saveable.Version,
                payloadType = dto.GetType().AssemblyQualifiedName,
                payload = JToken.FromObject(dto)
            };
        }

        /// <summary>
        /// Deserialize payload về đúng type.
        /// </summary>
        private object DeserializePayload(SaveEnvelope env)
        {
            if (env.payload == null || string.IsNullOrWhiteSpace(env.payloadType))
                return null;

            var type = Type.GetType(env.payloadType);
            if (type == null) return null;

            try
            {
                return env.payload.ToObject(type);
            }
            catch
            {
                return null;
            }
        }
    }
}
