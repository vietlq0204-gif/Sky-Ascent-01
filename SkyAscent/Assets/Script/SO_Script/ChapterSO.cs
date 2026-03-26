using UnityEngine;
using System;

[CreateAssetMenu(menuName = "GameProgress/Chapter/New chapter SO")]
public class ChapterSO : BaseSO
{
    //public bool IsCompleted;

    public SessionSO[] Sessions; 

    [Tooltip("Tất cả SolarObjectSO (không trùng lặp) xuất hiện trong toàn bộ Sessions của Chapter này")]
    public CosmicObjectSO[] CosmicObjects;   // Kết quả thống kê
}
