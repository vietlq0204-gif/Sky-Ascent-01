using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ViT.SaveKit.Abstractions;
using ViT.SaveKit.Models;
using ViT.SaveKit.Storage;

namespace ViT.SaveKit.Runtime
{
    public sealed class SaveSystem : ISaveSystem
    {
        private readonly IAccountContext _account;
        private readonly ISaveStore _store;
        private readonly Dictionary<string, ISaveable> _saveablesByKey =
            new Dictionary<string, ISaveable>(64, StringComparer.Ordinal);

        public SaveSystem(IAccountContext account, ISaveStore store)
        {
            _account = account ?? throw new ArgumentNullException(nameof(account));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public void Register(ISaveable saveable)
        {
            if (saveable == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(saveable.Key))
            {
                throw new ArgumentException("ISaveable.Key must not be empty.");
            }

            if (_saveablesByKey.ContainsKey(saveable.Key))
            {
                throw new InvalidOperationException("Duplicate save key: " + saveable.Key);
            }

            _saveablesByKey.Add(saveable.Key, saveable);
        }

        public void Unregister(ISaveable saveable)
        {
            if (saveable == null || string.IsNullOrWhiteSpace(saveable.Key))
            {
                return;
            }

            _saveablesByKey.Remove(saveable.Key);
        }

        public void SaveAll()
        {
            string userId = GetUserId();
            var snapshot = new SaveSnapshot();

            foreach (var entry in _saveablesByKey)
            {
                ISaveable saveable = entry.Value;
                if (saveable == null || !saveable.ShouldSave)
                {
                    continue;
                }

                saveable.BeforeSave();

                object dto = saveable.Capture();
                if (dto == null)
                {
                    continue;
                }

                snapshot.Entries[saveable.Key] = BuildEnvelope(saveable, dto);
            }

            _store.Save(userId, snapshot);
        }

        public void LoadAll()
        {
            string userId = GetUserId();
            SaveSnapshot snapshot = _store.Load(userId);
            if (snapshot == null || snapshot.Entries.Count == 0)
            {
                return;
            }

            foreach (var entry in snapshot.Entries)
            {
                SaveEnvelope envelope = entry.Value;
                if (envelope == null || string.IsNullOrWhiteSpace(envelope.Key))
                {
                    continue;
                }

                if (!_saveablesByKey.TryGetValue(envelope.Key, out ISaveable saveable) || saveable == null)
                {
                    continue;
                }

                object dto = DeserializePayload(envelope);
                if (dto == null)
                {
                    continue;
                }

                saveable.Restore(dto, envelope.Version);
            }

            foreach (ISaveable saveable in _saveablesByKey.Values)
            {
                saveable?.AfterLoad();
            }
        }

        private string GetUserId()
        {
            string userId = _account.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new InvalidOperationException("IAccountContext.UserId must not be empty.");
            }

            return userId;
        }

        private static SaveEnvelope BuildEnvelope(ISaveable saveable, object dto)
        {
            return new SaveEnvelope
            {
                Key = saveable.Key,
                Version = saveable.Version,
                PayloadType = dto.GetType().AssemblyQualifiedName,
                Payload = JToken.FromObject(dto)
            };
        }

        private static object DeserializePayload(SaveEnvelope envelope)
        {
            if (envelope.Payload == null || string.IsNullOrWhiteSpace(envelope.PayloadType))
            {
                return null;
            }

            Type type = Type.GetType(envelope.PayloadType);
            if (type == null)
            {
                return null;
            }

            try
            {
                return envelope.Payload.ToObject(type);
            }
            catch
            {
                return null;
            }
        }
    }
}
