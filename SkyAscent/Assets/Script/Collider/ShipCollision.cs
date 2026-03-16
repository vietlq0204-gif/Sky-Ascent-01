using UnityEngine;


public class ShipCollision : CoreEventBase
{

    public override void SubscribeEvents()
    {
    }

    private void CheckColliderWithObstacle(Collider other)
    {
        if (other == null) return;
        // nếu và chạm với GameObject có layer là Obstacle
        if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            // nếu other có tag là Stone
            if (other.gameObject.CompareTag("Stone"))
            {
                Debug.Log($"[ShipCollision] Đã va chạm với {other.name}");
            }
        }
    }

    private void CheckColliderWithItem(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Item"))
        {
            if (other.gameObject.CompareTag("Coin"))
            {
                Debug.Log($"[ShipCollision] Đã va chạm với {other.name}");

            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckColliderWithObstacle(other);
        //CheckColliderWithItem(other);
    }

    private void OnCollisionEnter(Collision other)
    {
        CheckColliderWithObstacle(other.collider);
        //CheckColliderWithItem(other.collider);
    }
}
