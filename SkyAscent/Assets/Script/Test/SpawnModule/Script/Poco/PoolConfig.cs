using System;
using UnityEngine;


/// <summary>
/// Cấu hình pool cho một spawnable.
/// </summary>
[Serializable]
public class PoolConfig
{
    [Tooltip ("Số lượng instance được tạo sẵn khi pool khởi tạo. Giá trị này không được lớn hơn maxSize.")]
    [Min(0)] public int prewarmCount = 0;
    [Tooltip ("Số lượng instance tối đa mà pool có thể chứa. Nếu số lượng instance vượt quá giá trị này, các instance thừa sẽ bị hủy.")]
    [Min(1)] public int maxSize = 64;
    [Tooltip ("Số lượng instance được tạo mới mỗi khi pool cần mở rộng. Giá trị này không được lớn hơn maxSize.")]
    [Min(1)] public int growStep = 8;
    [Tooltip ("Cho phép pool tự động mở rộng khi số lượng instance vượt quá maxSize. Nếu giá trị này là false, các instance thừa sẽ bị hủy ngay lập tức.")]
    public bool allowGrow = true;

    /// <summary>
    /// Validate config để tránh giá trị lỗi.
    /// </summary>
    /// <remarks>Gọi khi load catalog.</remarks>
    public void Sanitize()
    {
        if (maxSize < 1) maxSize = 1;
        if (growStep < 1) growStep = 1;
        if (prewarmCount < 0) prewarmCount = 0;
    }

}