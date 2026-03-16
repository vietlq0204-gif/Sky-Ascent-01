using UnityEngine;

// base
public partial class Coin : MonoBehaviour, IItem, IInject<SpawnManager>
{
    [SerializeField] SpawnManager _spawnManager;

    [SerializeField] private ItemType itemtype;
    [SerializeField] private MoneySO moneyData;
    [SerializeField] private bool canPickUp = true;

    public ItemType Type => itemtype;

    public void Inject(SpawnManager context)
    {
        _spawnManager = context;
    }

    public bool CanPickUp()
    {
        return canPickUp;
    }

    //khởi tạo dữ liệu để gửi đi

    public MoneyContext GetMoneyData()
    {
        return new MoneyContext
        {
            MoneyData = moneyData,
            CanPickUp = canPickUp
        };
    }
}

// collision
public partial class Coin
{
    private void CheckColliderWithCharacter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Character"))
        {
            if (other.gameObject.CompareTag("Player"))
            {
                //Debug.Log($"[Coin] Đã va chạm với {other.name}");

                CoreEvents.InteractMoney.Raise(
                   new InteractMoneyEvent(GetMoneyData(), InteractItemType.Pickup));

                _spawnManager.Despawn(gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckColliderWithCharacter(other);
    }

    private void OnCollisionEnter(Collision other)
    {
        CheckColliderWithCharacter(other.collider);
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(Coin))]
public class CoinEditor : UnityEditor.Editor
{
    Coin coin => (Coin)target;

    /// <summary>
    /// Hàm này được gọi khi có sự thay đổi trong Inspector của Unity Editor.
    /// </summary>
    private void Reset()
    {
        //coin.ValidateVariables();
    }

}
#endif
