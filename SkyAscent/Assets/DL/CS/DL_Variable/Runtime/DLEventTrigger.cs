using UnityEngine;
/// <summary>
/// Kích hoạt sự kiện khi có va chạm (hoặc hành động).
/// – Lấy giá trị từ DLVariable.
/// – Gọi onRaised.Raise(value).
/// </summary>
/// <remarks>
/// Dùng trong gameplay (VD: player chạm object → tăng điểm, bật UI…).
/// </remarks>
public class DLEventTrigger : MonoBehaviour
{
    [SerializeField] private DLEventBase onRaised;
    [SerializeField] private DLVariable variable;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || variable == null || onRaised == null)
            return;

        object value = variable.ApplyAndGet();

        // Type checking — tránh nhầm (tà thuật)
        switch (value)
        {
            case int i when onRaised is DLEvent<int> ei: ei.Raise(i); break;
            case float f when onRaised is DLEvent<float> ef: ef.Raise(f); break;
            case bool b when onRaised is DLEvent<bool> eb: eb.Raise(b); break;
            case string s when onRaised is DLEvent<string> es: es.Raise(s); break;
            default:
                Debug.LogWarning($"[DLEventTrigger] Type mismatch: variable={variable.GetType().Name}, event={onRaised.GetType().Name}", this);
                break;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player") || variable == null || onRaised == null)
            return;

        object value = variable.ApplyAndGet();

        // Type checking — tránh nhầm (tà thuật)
        switch (value)
        {
            case int i when onRaised is DLEvent<int> ei: ei.Raise(i); break;
            case float f when onRaised is DLEvent<float> ef: ef.Raise(f); break;
            case bool b when onRaised is DLEvent<bool> eb: eb.Raise(b); break;
            case string s when onRaised is DLEvent<string> es: es.Raise(s); break;
            default:
                Debug.LogWarning($"[DLEventTrigger] Type mismatch: variable={variable.GetType().Name}, event={onRaised.GetType().Name}", this);
                break;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {

        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {

        }
    }

}
