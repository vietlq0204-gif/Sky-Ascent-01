using UnityEngine;
using System;

public enum DifficultyType
{
    Easy,
    Nomal,
    Hard,
    NoHope
}

[CreateAssetMenu(menuName = "Session/Data/New session SO")]
public class SessionSO : BaseSO, IStableId
{
    //public bool IsCompleted;

    [Tooltip("Độ khó. là điều kiện để maping với phần thưởng.. (test)")]
    public DifficultyType difficultyType;

    [Tooltip("Can't null")]
    public MapSO mapSO;

    [Tooltip("Can null, (money, weapon, ...)")]
    public ItemsSO[] itemsSO;
}
