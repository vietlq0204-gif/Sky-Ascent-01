using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Serializable]
public struct EffectItem
{
    public GameObject Parent;
    public GameObject Effect;
}

public class EffectController : CoreEventBase, IInject<Core>
{
    private Core _core;
    [SerializeField] private EffectItem[] effectItem;

#if UNITY_EDITOR
    private void OnValidate()
    {
        GetEffectChidGameObject();
    }
#endif

    private void Start()
    {
        GetEffectChidGameObject();
    }

    public void Inject(Core context) { _core = context; } 

    public override void SubscribeEvents()
    {
        CoreEvents.OnEffect.Subscribe(e => IvokeEffect(e), Binder);
    }

    private void GetEffectChidGameObject()
    {
        List<EffectItem> tempList = new List<EffectItem>();

        // Lấy toàn bộ Transform con (kể cả inactive)
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        int targetLayer = LayerMask.NameToLayer("TransparentFX");

        foreach (Transform child in allChildren)
        {
            if (child.gameObject.layer == targetLayer)
            {
                EffectItem item = new EffectItem
                {
                    Parent = child.parent != null ? child.parent.gameObject : null,
                    Effect = child.gameObject
                };
                tempList.Add(item);
            }
        }

        effectItem = tempList.ToArray();
        //Debug.Log($"[EffectController] Đã tìm thấy {effectItem.Length} EffectItem (TransparentFX).");
    }
    
    private void IvokeEffect(OnEffectEvent onEffectEvent)
    {
        if (onEffectEvent.ParentEffectObject == null) return;

        //Debug.Log($"[EffectController] {onEffectEvent.ParentEffectObject.ToString()}");


        GameObject parentGO = onEffectEvent.ParentEffectObject as GameObject;
        if (parentGO == null)
        {
            Debug.LogWarning("[EffectController] parentObjectEffect không phải GameObject.");
            return;
        }

        foreach (var item in effectItem)
        {
            if (item.Parent == parentGO && item.Effect != null)
            {
                // ParticleSystem
                var ps = item.Effect.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    if (onEffectEvent.Play) ps.Play();
                    else ps.Stop();

                    //Debug.Log($"[EffectController] Phát hiệu ứng: {item.Effect.name} (cha: {item.Parent.name})");
                    continue;
                }

                // Animator
                var anim = item.Effect.GetComponent<Animator>();
                if (anim != null)
                {
                    if (onEffectEvent.Play) anim.SetTrigger("Play");
                    else anim.SetTrigger("Stop");

                    //Debug.Log($"[PlayEffect] Kích hoạt Animator: {item.Effect.name}");
                    continue;
                }

                //// Nếu chỉ là GameObject (vd effect prefab)
                //item.Effect.SetActive(true);
                //Debug.Log($"[PlayEffect] Bật GameObject Effect: {item.Effect.name}");
            }
        }
    }

}

public static class EffectUtility
{
    public static async Task RaiseEffect(
        GameObject parentEffect,
        float timePrepareStart = 0f,
        float timePrepareEnd = 0f,
        bool waitEndDuration = false,
        bool play = true)
    {
        await Delay(timePrepareStart);

        // Truyền đối tượng sang event hệ thống
        var obj = parentEffect as object;
        CoreEvents.OnEffect.Raise(new OnEffectEvent(obj, waitEndDuration, play));

        await Delay(timePrepareEnd);
    }

    private static async Task Delay(float seconds)
    {
        if (seconds > 0)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
                await Task.Yield();
        }
    }
}
