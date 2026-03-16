using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Quản lý CurrentDefaultCosmicObject trong Scene.
/// CurrentDefaultCosmicObject được dùng để lấp đầy khoản trống nghiệp vụ
/// </summary>
public class DefaultCosmicObject : CoreEventBase, IInject<Core>, IInject<ISolarObjectFactory>
{
    #region  references // variables

    private Core core;
    private ISolarObjectFactory factory;

    #endregion

    #region Inject

    public void Inject(Core context) { core = context; }
    public void Inject(ISolarObjectFactory context) { factory = context; }

    #endregion

    #region Events

    public override void SubscribeEvents()
    {
        CoreEvents.OnMoveAlongToPath.Subscribe(e => { if (e.IsEnd) GoMenu(e.IsEnd); }, Binder);

        CoreEvents.OnNewSession.Subscribe(_ => NewSession(), Binder);

        CoreEvents.MapDataEvent.Subscribe(e => { _ = EndSession(e.CosmicObjectSO); });
    }

    #endregion

    #region profession

    /// <summary>
    /// khi vào menu thì ẩn CurrentDefaultCosmicObject
    /// </summary>
    /// <param name="isOpen"></param>
    private void GoMenu(bool isOpen)
    {
        if (core.StateMachine.CurrentStateType == typeof(OnMenuState))
        {
            gameObject.SetActive(!isOpen);
        }
    }

    /// <summary>
    /// khi bắt đầu session thì ẩn CurrentDefaultCosmicObject
    /// </summary>
    private void NewSession()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// khi kết thúc session: bật this gameObject, dùng factory để spawn CosmicObject tương ứng vào this gameObject.transform 
    /// </summary>
    /// <param name="mapSO"></param>
    private async Task EndSession(CosmicObjectSO cosmicObjectSO)
    {
        gameObject.SetActive(true);

        await loadCosmicObject(cosmicObjectSO);

        //// tạm thời 
        //// từ targetObject này là có thể lấy được CosmicObjectSO và => session tiếp theo
        //CoreEvents.TargetObject.Raise(new TargetObjectEvent(
        //    TargetObjectEvent.TypeTarget.UI, cosmicObjectSO.name, null));
    }

    #endregion

    #region Logic

    /// <summary>
    /// dùng factory để load async CosmicObjectSO vào this.transform
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>
    /// Task
    /// </returns>
    private async Task loadCosmicObject(CosmicObjectSO obj)
    {
        // xóa hết con cũ
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            factory.DestroyInstance(child.gameObject);
        }

        // tiền tải obj
        await factory.PreloadAsync(obj, default);

        // tạo instance obj vào bên trong this.transform
        var instance = await factory.CreateAsync(
            obj,
            this.transform,
            default);

    }

    /// <summary>
    /// helper đổi material cho CurrentDefaultCosmicObject
    /// </summary>
    /// <param name="newMaterial"></param>
    private void SetMaterialToDefaultCosmicObject(Material newMaterial)
    {
        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("[CosmicObjectAPI] Không có Renderer trên object.");
            return;
        }

        renderer.sharedMaterial = newMaterial; // dùng sharedMaterial để không cần bật object
        Debug.Log($"[CosmicObjectAPI] Đã đổi material cho {gameObject.name}");
    }

    #endregion
}


