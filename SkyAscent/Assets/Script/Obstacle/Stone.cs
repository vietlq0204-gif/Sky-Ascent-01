using UnityEngine;

public enum ObstacelType
{
    None,
    Stone,
}

public enum subject
{
    None,
    Player,
    Stone,
}

public struct ObstacleContext
{
    public InteractType interactType;
    public DamageContext damageContext;

    public ObstacleContext (InteractType interactType, DamageContext damageContext)
    {
        this.interactType = interactType;
        this.damageContext = damageContext;
    }
}

public struct DamageContext
{
    public subject Cause;
    public subject Victim;
    public int Amout;

    public DamageContext (subject cause, subject victim, int amount)
    {
        this.Cause = cause;
        this.Victim = victim;
        this.Amout = amount;
    }
}

public partial class Stone : MonoBehaviour, IInject<SpawnManager>
{
   [SerializeField] SpawnManager _spawner;

    public void Inject(SpawnManager context)
    {
        _spawner = context;
    }

    //  đóng gói data để gửi đi (đang hard code, cần fix lại)
    public ObstacleContext GetObstacleContext()
    {
        return new ObstacleContext
        {
            damageContext = new DamageContext
            {
                Cause = subject.Stone,
                Victim = subject.Player,
                Amout = 1 // tạm thời
            }
        };
    }
}

// Collider
public partial class Stone
{
    private void CheckColliderWithCharacter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Character"))
        {
            if (other.gameObject.CompareTag("Player"))
            {
                //Debug.Log($"[Coin] Đã va chạm với {other.name}");

                CoreEvents.InteractObstacle.Raise(
                   new InteractObstacleEvent(
                       InteractType.collision, 
                       GetObstacleContext()));

                _spawner.Despawn(gameObject); // NÊN ĐƯA NÓ RA CONTROLLER
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
