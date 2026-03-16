using UnityEngine;

[CreateAssetMenu(menuName = "Item/Data/New item SO")]
public class ItemsSO : BaseSO
{
    public ItemType itemType;

    [ItemKeyword(ItemType.Money)]
    public MoneySO[] moneySO;

    [ItemKeyword(ItemType.Weapon)]
    public WeaponSO[] weaponSO;
}


public enum WeaponType
{
    sword,
    gun,
}
[CreateAssetMenu(menuName = "Item/Data/New WeaponSO")]
public class WeaponSO : BaseSO
{
    public WeaponType weaponType;
    public float damage;
    public float weight;
    public float fuelConsumption;
}

