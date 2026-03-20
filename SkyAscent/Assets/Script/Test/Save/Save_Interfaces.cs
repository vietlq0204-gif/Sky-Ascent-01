namespace Save.Abstractions
{
    /// <summary>
    /// Cung cấp user context hiện tại cho SaveSystem.
    /// </summary>
    /// <remarks>
    /// - Tách hẳn Account module.
    /// - UserId phải ổn định (không phụ thuộc display name).
    /// </remarks>
    public interface IAccountContext
    {
        string UserId { get; }
    }

    /// <summary>
    /// Adapter đóng gói data để save.
    /// module nào muốn tham gia Save/Load thì implement nó.
    /// </summary>
    /// <remarks>
    /// - Key phải unique (vd: "progress", "inventory", "settings").
    /// - Module tự tạo DTO thuần (Serializable) để SaveSystem serialize.
    /// - Restore nhận đúng kiểu DTO mà Capture trả về.
    /// </remarks>
    public interface ISaveable
    {
        /// <summary>Định danh file (không có đuôi), unique trong 1 slot save.</summary>
        string Key { get; }

        /// <summary>Version schema của DTO.</summary>
        int Version { get; }

        /// <summary>Có cần lưu không (dirty/enable).</summary>
        bool ShouldSave { get; }

        /// <summary>Hook trước khi capture.</summary>
        void BeforeSave();

        /// <summary>
        /// Lấy dữ liệu hiện tại của module và trả về một DTO (Data Transfer Object) để lưu trữ.
        /// </summary>
        /// <returns></returns>
        object Capture();

        /// <summary>
        /// Áp dụng dữ liệu đã lưu (DTO) vào module để khôi phục trạng thái.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="version"></param>
        void Restore(object data, int version);

        /// <summary>Hook sau khi restore.</summary>
        void AfterLoad();
    }
}

namespace Save.IO
{
    using System.Collections.Generic;
    using Save.Abstractions;

    /// <summary>
    /// Snapshot toàn bộ dữ liệu save của một user.
    /// </summary>
    public sealed class SaveSnapshot
    {
        public Dictionary<string, SaveEnvelope> Entries { get; } = new Dictionary<string, SaveEnvelope>(64);
    }

    /// <summary>
    /// Abstraction cho tầng lưu trữ save snapshot.
    /// </summary>
    public interface ISaveStore
    {
        void Save(string userId, SaveSnapshot snapshot);
        SaveSnapshot Load(string userId);
    }

    /// <summary>
    /// Abstraction cho file IO.
    /// </summary>
    public interface IFileHandler
    {
        void WriteAllText(string path, string content);
        string ReadAllText(string path);
        bool Exists(string path);
        void Delete(string path);
        void EnsureDirectory(string folderPath);
        void Move(string from, string to, bool overwrite);
        string[] GetFiles(string folderPath, string searchPattern);
        bool DirectoryExists(string folderPath);
        void DeleteDirectory(string folderPath, bool recursive);
        void MoveDirectory(string from, string to);
        void CreateDirectory(string folderPath);
    }
}

namespace Save.Core
{
    /// <summary>
    /// API Save/Load .
    /// </summary>
    public interface ISaveSystem
    {
        /// <summary>
        /// đăng kí Module muốn save vào đây ;)
        /// </summary>
        /// <param name="saveable"></param>
        void Register(Save.Abstractions.ISaveable saveable);

        /// <summary>
        /// hiện tại đang được saveSystem sử lí để từ động hủy đăng kí
        /// </summary>
        /// <param name="saveable"></param>
        void Unregister(Save.Abstractions.ISaveable saveable);

        /// <summary>
        ///  gọi nó để save tất cả các module
        /// </summary>
        void SaveAll();

        /// <summary>
        /// gọi nó để load tất cả các module
        /// </summary>
        void LoadAll();
    }
}
