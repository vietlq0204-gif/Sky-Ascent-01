using UnityEngine;

[CreateAssetMenu(menuName = "GameProgress/ New game progress SO")]
public class ProgressSO : BaseSO
{
    [Header("Catalog")]
    public ProgressCatalogSO catalog;

    public ChapterSO[] Chapter;
}
