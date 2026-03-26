using UnityEngine;

public enum MoneyType
{
    Coin,
    Crystal,
}

public struct MoneyContext
{
    public MoneySO MoneyData;
    public bool CanPickUp;
}

[CreateAssetMenu(menuName = "Item/Data/Money/ New money SO")]
public class MoneySO : BaseSO
{
    [Tooltip("")]
    public MoneyType MonenyType;

    [Tooltip("")]
    public int quantity;

    //public Material material;

    //[TextArea]
    //public string PrefabPath;
}
