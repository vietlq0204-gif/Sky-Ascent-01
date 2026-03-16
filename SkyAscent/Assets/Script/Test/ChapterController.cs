using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ChapterController (Layer 2):
/// - Nhận ChapterSnapshot (từ ChapterManager) qua CoreEvents.ReturnData
/// - Build danh sách CosmicObjectSO từ ChapterSnapshot.CosmicObjectIds (thông qua ICosmicObjectCatalog)
/// - Preload + Spawn bằng ISolarObjectFactory
/// - Báo hoàn tất load chapter qua CoreEvents.LoadChapter
/// </summary>
/// <remarks>
/// Mục tiêu bản rewrite:
/// - Không phụ thuộc timing ProgressSnapshot đầu game.
/// - Tự request ChapterSnapshot ở Start (để PlayMode luôn có spawn nếu data OK).
/// - ValidateDependencies để thấy lỗi DI ngay.
/// - Cho phép kéo tay ICosmicObjectCatalog provider trong Inspector nếu DI chưa inject.
/// </remarks>
public partial class ChapterController : IInject<ISolarObjectFactory>, IInject<IProgressQuery>, IInject<Core>
{
    #region References
    private Core _core;
    private ISolarObjectFactory _factory;
    private IProgressQuery _progressQuery;
    //private IChapterQuery chapterQuery;

    #endregion

    #region Inspector

    [Header("Spawn Slots (Bậc 1 con của object này)")]
    [Tooltip("Nếu để trống, controller sẽ tự collect bậc 1 từ transform.")]
    [SerializeField] private List<Transform> _slots = new List<Transform>(8);

    [Header("Spawn Options")]
    [SerializeField] private bool _autoCollectSlotsOnAwake = true;

    [Tooltip("Nếu true: khi Start sẽ tự request ChapterSnapshot (để PlayMode luôn chạy).")]
    [SerializeField] private bool _autoRequestSnapshotOnStart = true;

    [Tooltip("Spawn toàn bộ cosmic vào slot index này (mặc định 0).")]
    [SerializeField] private int _spawnSlotIndex = 0;

    [Tooltip("Nếu true: clear children của slot trước khi spawn chapter mới.")]
    [SerializeField] private bool _clearSlotBeforeSpawn = true;

    [Tooltip("Scale áp cho object spawn (tuỳ project).")]
    [SerializeField] private Vector3 _spawnScale = new Vector3(10f, 10f, 10f);

    #endregion

    #region Runtime State

    private string _currentChapterId;
    private bool _isMenuOpen;

    private CancellationTokenSource _lifetimeCts;
    private CancellationTokenSource _loadCts;

    // Reuse buffers to reduce GC
    private readonly List<CosmicObjectSO> _cosmicBuffer = new List<CosmicObjectSO>(128);
    private readonly HashSet<string> _dedupIds = new HashSet<string>(128);

    #endregion

    #region Inject

    public void Inject(Core context) => _core = context;
    public void Inject(ISolarObjectFactory context) => _factory = context;

    public void Inject(IProgressQuery context) => _progressQuery = context;
    //public void Inject(IChapterQuery context) => chapterQuery = context;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Awake: init lifetime token + collect slots.
    /// </summary>
    /// <remarks>Không spawn ở Awake để tránh phụ thuộc timing DI.</remarks>
    protected override void Awake()
    {
        base.Awake();

        _lifetimeCts = new CancellationTokenSource();

        if (_autoCollectSlotsOnAwake)
            CollectDirectChildrenIntoSlots();
    }

    /// <summary>
    /// Start: validate + auto request snapshot
    /// </summary>
    private void Start()
    {
        ValidateDependencies();

        if (_autoRequestSnapshotOnStart)
        {
            RequestChapterSnapshot(isDirty: false);
        }
    }

    private void OnDestroy()
    {
        CancelAndDispose(ref _loadCts);
        CancelAndDispose(ref _lifetimeCts);

        // Optional: nếu factory quản cache/pool
        _factory?.ReleaseAllPrefabs();
    }

    #endregion

    #region Load Chapter

    /// <summary>
    /// Chuẩn bị load chapter (cancel load cũ nếu có).
    /// </summary>
    /// <remarks>Skip nếu đang load cùng chapterId để tránh spam spawn.</remarks>
    /// <returns>void</returns>
    private void PrepareLoadChapter(in ChapterSnapshot chapter)
    {
        //if (_isMenuOpen) return;

        if (!string.IsNullOrEmpty(_currentChapterId) && _currentChapterId == chapter.ChapterId)
            return;

        CancelAndDispose(ref _loadCts);
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        _ = LoadChapterAsync(chapter, _loadCts.Token);
    }

    /// <summary>
    /// Thực thi load: clear -> build list -> preload -> spawn.
    /// </summary>
    /// <remarks>Không throw ra ngoài để tránh unobserved task.</remarks>
    /// <returns>Task</returns>
    private async Task LoadChapterAsync(ChapterSnapshot chapter, CancellationToken ct)
    {
        try
        {
            if (ct.IsCancellationRequested) return;

            _currentChapterId = chapter.ChapterId;

            Transform spawnSlot = ResolveSpawnSlot();

            if (_clearSlotBeforeSpawn)
                ClearChildren(spawnSlot);

            // Build cosmic list
            _cosmicBuffer.Clear();
            if (!TryBuildCosmicListFromSnapshot(chapter, _cosmicBuffer))
            {
                Debug.LogWarning($"ChapterController: chapterId='{chapter.ChapterId}' build cosmic list failed.");
                return;
            }

            if (_factory == null)
            {
                Debug.LogError("ChapterController: ISolarObjectFactory is null => không thể spawn.");
                return;
            }

            // Preload (nếu factory support)
            await _factory.PreloadAsync(_cosmicBuffer, ct);

            // Spawn
            for (int i = 0; i < _cosmicBuffer.Count; i++)
            {
                if (ct.IsCancellationRequested) break;

                var so = _cosmicBuffer[i];
                if (so == null) continue;

                var instance = await _factory.CreateAsync(so, spawnSlot, ct);
                if (instance == null) continue;

                var tr = instance.transform;
                tr.localPosition = Vector3.zero;
                tr.localRotation = Quaternion.identity;
                tr.localScale = _spawnScale;
            }

            LoadChapterComplete();
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            Debug.LogError($"ChapterController.LoadChapterAsync exception: {ex}");
        }
    }

    #endregion

    #region Build List (Snapshot -> SO)

    /// <summary>
    /// Build danh sách CosmicObjectSO theo ChapterSnapshot.CosmicObjectIds.
    /// </summary>
    /// <remarks>
    /// - Dedup id để tránh spawn trùng.
    /// - ICosmicObjectCatalog là abstraction: không phụ thuộc ProgressManager.
    /// </remarks>
    /// <returns>bool</returns>
    private bool TryBuildCosmicListFromSnapshot(in ChapterSnapshot chapter, List<CosmicObjectSO> output)
    {
        output.Clear();
        _dedupIds.Clear();

        var ids = chapter.CosmicObjectIds;
        if (ids == null || ids.Length == 0)
            return true;

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (string.IsNullOrEmpty(id)) continue;
            if (!_dedupIds.Add(id)) continue;

            if (_progressQuery.TryGetCosmicObjectSOById(id, out var so) && so != null)
                output.Add(so);
        }

        return true;
    }

    #endregion

    #region Slot Helpers

    /// <summary>
    /// Collect direct children (bậc 1) của ChapterController.
    /// </summary>
    /// <remarks>Tránh Find/Transform traversal nhiều lần.</remarks>
    /// <returns>void</returns>
    private void CollectDirectChildrenIntoSlots()
    {
        if (_slots != null) return;
        if (_slots == null) _slots = new List<Transform>(8);
        _slots.Clear();

        int count = transform.childCount;
        for (int i = 0; i < count; i++)
            _slots.Add(transform.GetChild(i));
    }

    /// <summary>
    /// Lấy slot spawn hợp lệ.
    /// </summary>
    /// <remarks>Nếu index invalid, fallback transform.</remarks>
    /// <returns>Transform</returns>
    private Transform ResolveSpawnSlot()
    {
        if (_slots != null && _slots.Count > 0)
        {
            int idx = Mathf.Clamp(_spawnSlotIndex, 0, _slots.Count - 1);
            if (_slots[idx] != null) return _slots[idx];
        }

        return transform;
    }

    /// <summary>
    /// Clear toàn bộ child GameObject trong target.
    /// </summary>
    /// <remarks>Destroy theo thứ tự ngược để an toàn.</remarks>
    /// <returns>void</returns>
    private static void ClearChildren(Transform target)
    {
        if (target == null) return;

        for (int i = target.childCount - 1; i >= 0; i--)
        {
            var child = target.GetChild(i);
            if (child != null)
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Validate dependency để debug nhanh.
    /// </summary>
    /// <remarks>Giúp phát hiện lỗi DI ngay khi PlayMode.</remarks>
    /// <returns>void</returns>
    private void ValidateDependencies()
    {
        if (_core == null)
            Debug.LogError("ChapterController: chưa Inject(Core).");

        if (_factory == null)
            Debug.LogError("ChapterController: chưa Inject(ISolarObjectFactory).");

        //if (chapterQuery == null && _manualCatalogProvider == null)
        //    Debug.LogError("ChapterController: thiếu ICosmicObjectCatalog (inject hoặc kéo tay provider).");
    }

    /// <summary>
    /// Cancel và Dispose CTS an toàn.
    /// </summary>
    /// <remarks>Tránh leak token source.</remarks>
    /// <returns>void</returns>
    private static void CancelAndDispose(ref CancellationTokenSource cts)
    {
        if (cts == null) return;

        try { cts.Cancel(); } catch { /* ignore */ }
        try { cts.Dispose(); } catch { /* ignore */ }

        cts = null;
    }

    #endregion
}

// Profession
public partial class ChapterController
{
    /// <summary>
    /// Menu open/close.
    /// </summary>
    /// <remarks>Khi mở menu: cancel load để tránh spawn lúc UI pause.</remarks>
    /// <returns>void</returns>
    private void OnMenu(bool isOpen)
    {
        if (!isOpen) return;
        _isMenuOpen = isOpen;

        // phóng to GameObject chứa các map
        _core.ZoomGameObject(_slots[0], Vector3.one);

        if (_isMenuOpen)
            CancelAndDispose(ref _loadCts);
    }

    /// <summary>
    /// Khi session đổi: refresh chapter (cache OK).
    /// </summary>
    /// <remarks>Nếu game không cần auto refresh, có thể bỏ call này.</remarks>
    /// <returns>void</returns>
    /// <summary>
    /// Chuẩn bị session mới: thu nhỏ view.
    /// </summary>
    private async void PrepareNewSession()
    {
        try
        {
            // delay có cancellation để tránh chạy "lụi" khi object bị destroy
            await Task.Delay(3000, _lifetimeCts.Token);

            RequestChapterSnapshot(isDirty: false);
            _core.ZoomGameObject(_slots[0], new Vector3(0.01f, 0.01f, 0.01f));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void PrepareEndSession()
    {

    }

    private void EndSession()
    {
        _core.ZoomGameObject(_slots[0], new Vector3(1f, 1f, 1f));
    }
}

// Event
public partial class ChapterController : CoreEventBase
{
    public override void SubscribeEvents()
    {
        CoreEvents.ReturnData.Subscribe(OnReturnDataEvent, Binder);

        CoreEvents.OnMenu.Subscribe(e => OnMenu(e.IsOpenMenu), Binder);
        CoreEvents.OnNewSession.Subscribe(_ => PrepareNewSession(), Binder);
        CoreEvents.OnEndSession.Subscribe(_ => EndSession(), Binder);
    }

    /// <summary>
    /// Nhận ChapterSnapshot từ ChapterManager.
    /// </summary>
    /// <remarks>Chỉ xử lý (Send, ChapterSnapshot).</remarks>
    /// <returns>void</returns>
    private void OnReturnDataEvent(ReturnDataEvent e)
    {
        if (e.eventType != CoreEventType.Send) return;
        if (e.snapshotType != SnapShotType.ChapterSnapshot) return;

        PrepareLoadChapter(e.chapterSnapshot);
    }

    /// <summary>
    /// Request get ChapterSnapshot .
    /// </summary>
    /// <remarks>
    /// - isDirty=false: cho phép ChapterManager trả cache.
    /// - isDirty=true : buộc ChapterManager rebuild snapshot.
    /// </remarks>
    /// <returns>void</returns>
    private void RequestChapterSnapshot(bool isDirty = false)
    {
        // Request get ChapterSnapshot từ ChapterManager
        CoreEvents.ReturnData.Raise(new ReturnDataEvent(
            CoreEventType.Request,
            SnapShotType.ChapterSnapshot,
            default(ChapterSnapshot),
            isDirty
        ));
    }

    /// <summary>
    /// Báo cho các hệ thống khác (vd: UI) biết rằng chapterController  đã sẵn sàng data
    /// </summary>
    /// <remarks></remarks>
    /// <returns>void</returns>
    private void LoadChapterComplete()
    {
        CoreEvents.LoadChapter.Raise(new LoadDataEvent(LoadDataEvent.TypeData.Chapter, true));

        _ = ApplyLastSessionforUI();
    }

    // yêu cầu ui target đến last map (data thật hiện tại là cosmicObjectSO)
    private async Task ApplyLastSessionforUI()
    {
        await Task.Delay(/*50*/0);

        if (_progressQuery.TryGetLastMapSO(out var mapSO) && mapSO != null)
            //Debug.LogWarning($"ChapterController: {mapSO._name}");
            CoreEvents.TargetObject.Raise(new TargetObjectEvent(
                TargetObjectEvent.TypeTarget.Data_To_UI,
                mapSO));

    }
}

