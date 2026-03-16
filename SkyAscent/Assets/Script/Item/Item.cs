public enum ItemType
{
    none,
    Money,
    Weapon,
}

public interface IItem
{
    ItemType Type { get; }
    bool CanPickUp();

}

//public struct ItemContext
//{
//    public ItemType Type;
//    public 
//    public bool CanPickUp;
//}

//public enum weaponType
//{
//    Sword,
//    Bow,
//    Staff,
//}

//public abstract class Item
//{
//    protected bool CanPickUp(bool Can)
//    {
//        return Can;
//    }
//}
