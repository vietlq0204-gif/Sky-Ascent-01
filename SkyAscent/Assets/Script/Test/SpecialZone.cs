using System;
using System.Collections.Generic;
using UnityEngine;

public class SpecialZoneContext
{
    // Cấu hình zone
    public SpecialZoneSO ZoneSO { get; }
    public SpecialZoneType ZoneType => ZoneSO._type;

    // Strategy đã chọn cho lần va chạm này
    public SpecialZoneStrategySO Strategy { get; }

    // Độ khó hiện tại của session
    public DifficultyType Difficulty { get; }

    // Object đã va chạm
    public GameObject CollidedObj { get; }

    // Payload mở rộng (nếu sau này cần)
    public IReadOnlyDictionary<string, object> ExtraData { get; }

    public SpecialZoneContext(
        SpecialZoneSO zoneSO,
        SpecialZoneStrategySO strategy,
        DifficultyType difficulty,
        GameObject collidedObj,
        Dictionary<string, object> extra = null)
    {
        ZoneSO = zoneSO;
        Strategy = strategy;
        Difficulty = difficulty;
        CollidedObj = collidedObj;
        ExtraData = extra ?? new Dictionary<string, object>();
    }

    // Helper đọc payload
    public T GetExtra<T>(string key, T defaultValue = default)
    {
        if (ExtraData.TryGetValue(key, out var value))
            return (T)value;

        return defaultValue;
    }
}

public class SpecialZone : MonoBehaviour, IInject<SessionSO>, IInject<SpecialZoneSO>
{
    [SerializeField] SessionSO _sessionSO;
    [SerializeField] SpecialZoneSO _specialZoneSO;

    public void Inject(SessionSO context) => _sessionSO = context;
    public void Inject(SpecialZoneSO context)
    {
        _specialZoneSO = context;
        // đăng kí Chiến lược cho this zone
        SpecialZoneStrategyResolver.Register(_specialZoneSO);
    }

    // chuẩn bị context để gửi đi
    SpecialZoneContext PrepareSpecialZoneContext(GameObject obj)
    {
        var diff = _sessionSO.difficultyType;
        var strategy = SpecialZoneStrategyResolver.Get(_specialZoneSO._type, diff);

        if (strategy == null)
        {
            Debug.LogWarning(
                $"[SpecialZone] Không tìm thấy strategy cho zone={_specialZoneSO._type} diff={diff}");
            return null;
        }

        // Nếu cần payload, thêm vào đây :D
        var extra = new Dictionary<string, object>
        {
            // { "EnterTime", Time.time },
            // { "ZonePosition", transform.position }
        };

        var ctx = new SpecialZoneContext(
            zoneSO: _specialZoneSO,
            strategy: strategy,
            difficulty: diff,
            collidedObj: obj.gameObject,
            extra: extra
        );

        return ctx;
    }

    private void CheckOnCollider(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var ctx = PrepareSpecialZoneContext(other.gameObject);

            CoreEvents.OnColliderWithObjBySpecialZone.Raise
                (new OnColliderWithObjBySpecialZoneEvent(ctx));
        }
    }

    private void CheckExitCollier(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var ctx = PrepareSpecialZoneContext(other.gameObject);

            CoreEvents.OnColliderWithObjBySpecialZone.Raise
                (new OnColliderWithObjBySpecialZoneEvent(ctx, false));
        }
    }

    #region Unity life cycle

    private void OnTriggerEnter(Collider other)
    {
        CheckOnCollider(other);
    }

    private void OnCollisionEnter(Collision other)
    {
        CheckOnCollider(other.collider);
    }

    private void OnTriggerExit(Collider other)
    {
        CheckExitCollier(other);
    }

    private void OnCollisionExit(Collision other)
    {
        CheckExitCollier(other.collider);
    }

    #endregion
}
