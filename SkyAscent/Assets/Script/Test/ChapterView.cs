//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Threading;
//using System.Threading.Tasks;
//using UnityEngine;

//#if UNITY_EDITOR
//using UnityEditor;
//#endif

///// <summary>
///// ChapterView: nhận ProgressSnapshot qua event, chọn ChapterSnapshot và spawn cosmic objects.
///// </summary>
///// <remarks>
///// - Không còn phụ thuộc ChapterObjects / SessionObject.
///// - Load/Unload có cancellation.
///// - Auto collect direct children (slots) để dùng làm spawnRoot/zoom.
///// - Cosmic objects được lấy từ ChapterSnapshot bằng reflection:
/////   + Ưu tiên property/field tên "CosmicObjects" hoặc "cosmicObjects" kiểu IEnumerable<SolarObjectSO>.
/////   + Nếu ChapterSnapshot không chứa SolarObjectSO refs => ChapterView sẽ log error và không spawn.
///// </remarks>
//public partial class ChapterView : CoreEventBase, IInject<Core>, IInject<ProgressManager>, IInject<ISolarObjectFactory>
//{
//    #region References // variable

//    private Core _core;
//    private ProgressManager _progress;
//    private ISolarObjectFactory _factory;

//    [SerializeField] private List<Transform> _slots = new List<Transform>(8);

//    [Header("Auto Load")]
//    [Tooltip("Nếu true, khi nhận snapshot lần đầu sẽ auto load chapter.")]
//    [SerializeField] private bool _autoLoadOnFirstSnapshot = true;
//    [Tooltip("Nếu có giá trị, sẽ ưu tiên load chapterId này (khớp với ChapterSnapshot.ChapterId).")]
//    [SerializeField] private string _forceChapterId;

//    #endregion

//    #region Runtime - Snapshot Cache

//    private bool _hasSnapshot;
//    private ProgressSnapshot _lastSnapshot;

//    // current chapter id loaded
//    private string _currentChapterId;

//    #endregion

//    #region Runtime - Async / Cancellation

//    private CancellationTokenSource _lifetimeCts;
//    private CancellationTokenSource _loadCts;

//    // reuse buffer to reduce GC
//    private readonly List<CosmicObjectSO> _cosmicBuffer = new List<CosmicObjectSO>(128);

//    #endregion

//    #region Inject

//    public void Inject(Core context) { _core = context; }
//    public void Inject(ProgressManager context) { _progress = context; }
//    public void Inject(ISolarObjectFactory context) { _factory = context; }

//    #endregion

//    #region Unity Lifecycle

//    protected override void Awake()
//    {
//        base.Awake();

//        _lifetimeCts = new CancellationTokenSource();

//        InitRoot();
//    }

//    private void Start()
//    {
//        ValidateDependencies();
//    }

//    private void OnDestroy()
//    {
//        CancelAndDispose(ref _loadCts);
//        CancelAndDispose(ref _lifetimeCts);

//        // Quy ước: khi view chết thì release cache (nếu factory quản lý pooling/prefab cache).
//        _factory?.ReleaseAllPrefabs();
//    }

//    #endregion

//    #region profession

//    /// <summary>
//    /// Chuẩn bị session mới: thu nhỏ view.
//    /// </summary>
//    private async void PrepareNewSession()
//    {
//        try
//        {
//            // delay có cancellation để tránh chạy "lụi" khi object bị destroy
//            await Task.Delay(3000, _lifetimeCts.Token);

//            _core.ZoomGameObject(_slots[0], new Vector3(0.01f, 0.01f, 0.01f));
//        }
//        catch (Exception ex)
//        {
//            Debug.LogException(ex);
//        }
//    }

//    /// <summary>
//    /// Mở menu: phóng to view.
//    /// </summary>
//    private void OnMenu(bool isOpenMenu)
//    {
//        if (!isOpenMenu) return;

//        _core.ZoomGameObject(_slots[0], Vector3.one);
//    }

//    #endregion

//    #region Root base

//    private void InitRoot()
//    {
//        if (_slots == null || _slots.Count == 0)
//        {
//            CollectDirect_ChildGameObject_IntoSlots();
//        }
//    }

//    #region helper

//    /// <summary>
//    /// Collect direct children (bậc 1) của ChapterView.
//    /// </summary>
//    private void CollectDirect_ChildGameObject_IntoSlots()
//    {
//        if (_slots == null) _slots = new List<Transform>(8);
//        _slots.Clear();

//        int count = transform.childCount;
//        for (int i = 0; i < count; i++)
//            _slots.Add(transform.GetChild(i));
//    }


//    /// <summary>
//    /// helper destroy chhid gameobject in target
//    /// </summary>
//    /// <param name="target"></param>
//    private static void ClearChildGameObjectInTarget(Transform target)
//    {
//        if (target == null) return;

//        for (int i = target.childCount - 1; i >= 0; i--)
//        {
//            var child = target.GetChild(i);
//            if (child != null)
//                Destroy(child.gameObject);
//        }
//    }

//    #endregion

//    #endregion

//    #region Load/Unload Chapter

//    /// <summary>
//    /// Request load chapter (cancel load cũ nếu đang chạy).
//    /// </summary>
//    private void PrepareLoadChapter(ChapterSnapshot chapter)
//    {
//        if (_slots[0] == null)
//        {
//            Debug.LogError("ChapterView: haven't address to spawn chapter ingredient.");
//            return;
//        }

//        // Nếu đang load đúng chapter rồi thì bỏ qua
//        if (!string.IsNullOrEmpty(_currentChapterId) && _currentChapterId == chapter.ChapterId)
//            return;

//        CancelAndDispose(ref _loadCts);
//        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

//        _ = LoadChapterAsync(chapter, _loadCts.Token);
//    }

//    /// <summary>
//    /// Thực thi load chapter async.
//    /// </summary>
//    private async Task LoadChapterAsync(ChapterSnapshot chapter, CancellationToken ct)
//    {
//        try
//        {
//            _currentChapterId = chapter.ChapterId;

//            await UnloadCurrent(ct);

//            // Build cosmic list từ snapshot (Id -> SO qua catalog)
//            _cosmicBuffer.Clear();
//            //if (!TryGetCosmicObjectsByIdsWithCatalog(chapter.CosmicObjectIds, _cosmicBuffer))
//            //{
//            //    Debug.LogWarning($"ChapterView: chapterId='{chapter.ChapterId}' không resolve được cosmic objects từ CosmicObjectIds.");
//            //    return;
//            //}

//            //if (_preloadBeforeSpawn)
//            await _factory.PreloadAsync(_cosmicBuffer, ct);

//            // Spawn
//            for (int i = 0; i < _cosmicBuffer.Count; i++)
//            {
//                if (ct.IsCancellationRequested) break;

//                var so = _cosmicBuffer[i];
//                if (so == null) continue;

//                var instance = await _factory.CreateAsync(so, _slots[0], ct);
//                if (instance == null) continue;

//                //if (_deactivateSpawnedOnCreate)
//                //instance.gameObject.SetActive(false);

//                var tr = instance.transform;
//                tr.localPosition = Vector3.zero;
//                tr.localScale = new Vector3(10f, 10f, 10f);
//            }

//            // thông báo Chapter view đã chuẩn bị data xong đã là đã load xong
//            LoadChapterComplete();
//        }
//        catch (Exception ex)
//        {
//            Debug.LogException(ex);
//        }
//    }

//    /// <summary>
//    /// Unload chapter hiện tại
//    /// </summary>
//    private async Task UnloadCurrent(CancellationToken ct)
//    {
//        if (_slots[0] == null) return;

//        ClearChildGameObjectInTarget(_slots[0]);

//        _factory?.ReleaseAllPrefabs();

//        await Task.CompletedTask;
//    }

//    #endregion

//    #region  Get data helper

//    /// <summary>
//    ///  Cache progress snapShot để tái sử dụng 
//    /// </summary>
//    /// <param name="snapshot"></param>
//    private void CacheSnapshot(ProgressSnapshot snapshot)
//    {
//        _lastSnapshot = snapshot;
//        _hasSnapshot = true;
//    }

//    /// <summary>
//    /// helper select chapter to load
//    /// </summary>
//    /// <param name="snapshot"></param>
//    /// <returns></returns>
//    private ChapterSnapshot? SelectChapterToLoad(ProgressSnapshot snapshot)
//    {
//        if (snapshot.Chapters == null || snapshot.Chapters.Length == 0)
//            return null;

//        // 1) Force by id
//        if (!string.IsNullOrEmpty(_forceChapterId))
//        {
//            for (int i = 0; i < snapshot.Chapters.Length; i++)
//            {
//                if (snapshot.Chapters[i].ChapterId == _forceChapterId)
//                    return snapshot.Chapters[i];
//            }

//            Debug.LogWarning($"ChapterView: forceChapterId='{_forceChapterId}' không tìm thấy trong snapshot.");
//        }

//        // 2) Try lastChapterId from ProgressManager (nếu có method public GetLastChapterId())
//        string lastId = TryGetLastChapterIdFromProgress();
//        if (!string.IsNullOrEmpty(lastId))
//        {
//            for (int i = 0; i < snapshot.Chapters.Length; i++)
//            {
//                if (snapshot.Chapters[i].ChapterId == lastId)
//                    return snapshot.Chapters[i];
//            }
//        }

//        // 3) Fallback: first
//        return snapshot.Chapters[0];
//    }

//    /// <summary>
//    /// helper get last chapter
//    /// </summary>
//    /// <returns></returns>
//    private string TryGetLastChapterIdFromProgress()
//    {
//        if (_progress == null) return null;

//        // Tránh hard-dependency vào method (để file compile dù GameProgresssManager chưa expose).
//        // Nếu GameProgresssManager có public string GetLastChapterId() => dùng.
//        try
//        {
//            //var mi = _progress.GetType().GetMethod("GetLastChapterId", BindingFlags.Public | BindingFlags.Instance);
//            //if (mi != null && mi.ReturnType == typeof(string) && mi.GetParameters().Length == 0)
//            //    return mi.Invoke(_progress, null) as string;

//            return _progress.GetLastChapterId();

//        }
//        catch (Exception e)
//        {
//            Debug.LogException(e);
//            return null;
//        }
//    }

//    ///// <summary>
//    ///// Get danh sách SolarObjectSO từ mảng cosmic ids trong ChapterSnapshot.
//    ///// id -> SO
//    ///// </summary>
//    ///// <returns>true nếu resolve ra được ít nhất 1 SO</returns>
//    //private bool TryGetCosmicObjectsByIdsWithCatalog(string[] ids, List<CosmicObjectSO> buffer)
//    //{
//    //    buffer.Clear();
//    //    if (ids == null || ids.Length == 0) return false;

//    //    var catalog = _progress != null ? _progress.Catalog : null;
//    //    if (catalog == null)
//    //    {
//    //        Debug.LogError("ChapterView: Progress.Catalog == null. Hãy gán GameProgressSO.catalog.");
//    //        return false;
//    //    }

//    //    for (int i = 0; i < ids.Length; i++)
//    //    {
//    //        string id = ids[i];
//    //        if (string.IsNullOrEmpty(id)) continue;

//    //        if (catalog.TryGet<CosmicObjectSO>(id, out var so) && so != null)
//    //            buffer.Add(so);
//    //        else
//    //            Debug.LogWarning($"ChapterView: catalog không tìm thấy cosmic id='{id}'");
//    //    }

//    //    return buffer.Count > 0;
//    //}

//    #endregion

//    #region Utilities

//    private void ValidateDependencies()
//    {
//        if (_progress == null)
//            Debug.LogError("ChapterView: chưa được Inject(GameProgresssManager).");

//        if (_factory == null)
//            Debug.LogError("ChapterView: chưa được Inject(ISolarObjectFactory).");

//        if (_core == null)
//            Debug.LogError("ChapterView: chưa được Inject(Core).");
//    }

//    private static void CancelAndDispose(ref CancellationTokenSource cts)
//    {
//        if (cts == null) return;
//        try { cts.Cancel(); } catch { /* ignore */ }
//        try { cts.Dispose(); } catch { /* ignore */ }
//        cts = null;
//    }

//    // Nếu ProgressManager đã có snapshot sẵn, pull ngay để UI/spawn không phụ thuộc timing event.
//    private void TryPullSnapshotImmediately()
//    {
//        if (_progress == null) return;

//        // Nếu GameProgresssManager có GetSnapshot() public -> pull
//        try
//        {
//            var snap = _progress.GetSnapshot();
//            if (snap.Chapters != null && snap.Chapters.Length > 0)
//            {
//                CacheSnapshot(snap);

//                if (_autoLoadOnFirstSnapshot)
//                {
//                    var selected = SelectChapterToLoad(snap);
//                    if (selected.HasValue)
//                        PrepareLoadChapter(selected.Value);
//                }
//            }
//        }
//        catch
//        {
//            // ignore
//        }
//    }

//    #endregion
//}

//public partial class ChapterView : CoreEventBase
//{
//    public override void SubscribeEvents()
//    {
//        CoreEvents.OnNewSession.Subscribe(_ => PrepareNewSession(), Binder);
//        CoreEvents.OnMenu.Subscribe(e => OnMenu(e.IsOpenMenu), Binder);

//        //CoreEvents.Snapshot.Subscribe(e => Receive_ProgressSnapshot_UpdateChapterView(e), Binder);

//        // receive event Snapshot from prpgress manager
//        CoreEvents.ReturnData.Subscribe(e => OnReturnDataEvent(e), Binder);
//    }

//    private void OnReturnDataEvent(ReturnDataEvent e)
//    {
//        if (e.eventType == CoreEventType.Send)
//        {
//            if (e.snapshotType == SnapShotType.ProgressSnapshot)
//                Receive_ProgressSnapshot_UpdateChapterView(e);
//        }
//        else if (e.eventType == CoreEventType.Request)
//        {
//            // property OnRequest
//        }
//        else
//        {
//            // property OnResponse
//        }
//    }

//    private void Receive_ProgressSnapshot_UpdateChapterView(ReturnDataEvent e)
//    {
//        var snap = e.progressSnapshot;
//        if (snap.Chapters == null || snap.Chapters.Length == 0)
//            return;

//        CacheSnapshot(snap);

//        if (!_autoLoadOnFirstSnapshot)
//            return;

//        // Auto load lần đầu, hoặc khi forceChapterId khác chapter đang load
//        if (!_hasSnapshot || string.IsNullOrEmpty(_currentChapterId) || !string.IsNullOrEmpty(_forceChapterId))
//        {
//            var selected = SelectChapterToLoad(snap);
//            if (selected.HasValue)
//                PrepareLoadChapter(selected.Value);
//        }
//    }

//    private void LoadChapterComplete()
//    {
//        CoreEvents.LoadChapter.Raise(new LoadDataEvent(LoadDataEvent.TypeData.Chapter, true));
//    }

//    // yêu cầu ui target đến last sesion
//    // tạm chưa dùng
//    private void ApplyLastSessionforUI(SessionSO so)
//    {
//        CoreEvents.TargetObject.Raise(new TargetObjectEvent(
//            TargetObjectEvent.TypeTarget.Data_To_UI
//            , so));
//    }
//}

///// <summary>
///// test
///// </summary>
//public partial class ChapterView
//{
//    bool isInitComplete = false;

//    private void Update()
//    {
//        TryApplylastSession();
//    }

//    private void TryApplylastSession()
//    {
//        if (isInitComplete) return;

//        if (_progress.TryGetLastSessionSO(out var so))
//        {
//            ApplyLastSessionforUI(so);
//            isInitComplete = true;
//        }
//    }
//}

//#if UNITY_EDITOR
//[CustomEditor(typeof(ChapterView))]
//public class ChapterViewEditor : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        DrawDefaultInspector();

//        var t = (ChapterView)target;

//        EditorGUILayout.Space(8);
//        EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

//        using (new EditorGUI.DisabledScope(Application.isPlaying))
//        {
//            if (GUILayout.Button("Collect Direct Children Slots"))
//            {
//                // gọi private method bằng reflection để không lộ API runtime
//                var mi = typeof(ChapterView).GetMethod("CollectDirectChildrenIntoSlots", BindingFlags.NonPublic | BindingFlags.Instance);
//                mi?.Invoke(t, null);

//                var mi2 = typeof(ChapterView).GetMethod("ResolveSpawnRoot", BindingFlags.NonPublic | BindingFlags.Instance);
//                mi2?.Invoke(t, null);

//                EditorUtility.SetDirty(t);
//                Repaint();
//            }
//        }
//    }
//}
//#endif
