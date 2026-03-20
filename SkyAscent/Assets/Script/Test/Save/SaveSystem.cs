using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Save.Abstractions;
using Save.IO;

namespace Save.Core
{
    /// <summary>
    /// SaveSystem: orchestrate Save/Load theo user qua save store.
    /// </summary>
    /// <remarks>
    /// - Không singleton.
    /// - Dùng DI inject: accountContext + saveStore.
    /// - Registry ISaveable bằng Dictionary để lookup O(1).
    /// </remarks>
    public sealed class SaveSystem : ISaveSystem
    {
        private readonly IAccountContext _account;
        private readonly ISaveStore _store;

        private readonly Dictionary<string, ISaveable> _saveablesByKey = new Dictionary<string, ISaveable>(64);

        #region Contructor

        public SaveSystem(IAccountContext account, ISaveStore store)
        {
            _account = account;
            _store = store;
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
        /// Save tất cả module thành một snapshot rồi giao cho store lưu.
        /// </summary>
        public void SaveAll()
        {
            string userId = _account.UserId;
            var snapshot = new SaveSnapshot();

            foreach (var kv in _saveablesByKey)
            {
                var saveable = kv.Value;
                if (saveable == null) continue;
                if (!saveable.ShouldSave) continue;

                saveable.BeforeSave();

                object dto = saveable.Capture();
                if (dto == null) continue;

                snapshot.Entries[saveable.Key] = BuildEnvelope(saveable, dto);
            }

            _store.Save(userId, snapshot);
        }

        /// <summary>
        /// Load tất cả module từ store.
        /// </summary>
        public void LoadAll()
        {
            string userId = _account.UserId;
            var snapshot = _store.Load(userId);
            if (snapshot == null || snapshot.Entries.Count == 0)
                return;

            // 1) restore data
            foreach (var kv in snapshot.Entries)
            {
                SaveEnvelope env = kv.Value;
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
