using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#region DTO save

/// <summary>
/// DTO để lưu progress (POCO).
/// </summary>
[Serializable]
public sealed class ProgressSaveData
{
    public LastAccess LasMap;

    /// <summary>Session truy cập gần nhất.</summary>
    public LastAccess lastSession;

    /// <summary>Chapter truy cập gần nhất (optional).</summary>
    public LastAccess lastChapter;

    /// <summary>Danh sách chapter + sessions completed.</summary>
    public List<ChapterEntry> chapters = new();

    /// <summary>Danh sách session đã truy cập (để unlock, highlight UI...)</summary>
    public List<string> accessedSessions = new();

    [Serializable]
    public sealed class LastAccess
    {
        /// <summary>Stable id của ChapterSO/SessionSO.</summary>
        public string id;

        /// <summary>Unix milliseconds (UTC) để sort/compare dễ.</summary>
        public long atUtcMs;
    }

    [Serializable]
    public sealed class ChapterEntry
    {
        public string id;
        public bool completed;

        /// <summary>
        /// Có thể rỗng trong save cũ. Nếu rỗng mà completed=true/false thì sẽ apply theo chapter.
        /// </summary>
        public List<SessionEntry> sessions = new();
    }

    [Serializable]
    public sealed class SessionEntry
    {
        public string id;
        public bool completed;
    }
}

/// <summary>
/// DTO để UI/logic đọc trạng thái kèm SO.
/// </summary>
public readonly struct ProgressEntry<T> where T : ScriptableObject
{
    public readonly T So;
    public readonly bool IsCompleted;

    public ProgressEntry(T so, bool isCompleted)
    {
        So = so;
        IsCompleted = isCompleted;
    }
}

#endregion

#region Snapshot struct

/// <summary>
/// Snapshot immutable để hệ khác đọc (UI/logic) mà không cần biết internal state.
/// </summary>
public readonly struct ProgressSnapshot
{
    public readonly int Version;
    public readonly ChapterSnapshot[] Chapters;

    public ProgressSnapshot(int version, ChapterSnapshot[] chapters)
    {
        Version = version;
        Chapters = chapters ?? Array.Empty<ChapterSnapshot>();
    }
}

/// <summary>
/// Snapshot cho 1 chapter.
/// </summary>
public readonly struct ChapterSnapshot
{
    public readonly string ChapterId;
    public readonly string DisplayName;
    public readonly bool IsCompleted;

    public readonly SessionSnapshot[] Sessions;

    /// <summary>
    /// Danh sách cosmic ids thuộc chapter (unique).
    /// Nếu CosmicObjectSO không có Id.
    /// </summary>
    public readonly string[] CosmicObjectIds;

    public ChapterSnapshot(
        string chapterId,
        string displayName,
        bool isCompleted,
        SessionSnapshot[] sessions,
        string[] cosmicObjectIds)
    {
        ChapterId = chapterId;
        DisplayName = displayName;
        IsCompleted = isCompleted;
        Sessions = sessions ?? Array.Empty<SessionSnapshot>();
        CosmicObjectIds = cosmicObjectIds ?? Array.Empty<string>();
    }
}

/// <summary>
/// Snapshot cho 1 session.
/// </summary>
public readonly struct SessionSnapshot
{
    public readonly string SessionId;
    public readonly string DisplayName;
    public readonly bool IsCompleted;
    public readonly bool IsAccessed;

    public SessionSnapshot(string sessionId, string displayName, bool isCompleted, bool isAccessed)
    {
        SessionId = sessionId;
        DisplayName = displayName;
        IsCompleted = isCompleted;
        IsAccessed = isAccessed;
    }
}

#endregion

public partial class ProgressManager : IProgressQuery, IInject<Core>
{
    #region Reference
    private Core _core;
    private Type _secondaryStateBeforeSave;
    [SerializeField] private ProgressSO _gameProgressSO;

    /// <summary>Catalog map Id -> SO (nguồn tra cứu runtime).</summary>
    private ProgressCatalogSO Catalog => _gameProgressSO != null ? _gameProgressSO.catalog : null;
    #endregion

    #region state Runtime

    [Header("Last access (runtime)")]
    [SerializeField] private string _lastMapId;
    [SerializeField] private string _lastSessionId;
    [SerializeField] private string _lastChapterId;
    private long _lastMapnAtUtcMs;
    private long _lastSessionAtUtcMs;
    private long _lastChapterAtUtcMs;

    #endregion

    #region Runtime State (Source of Truth)

    /// <summary> Completed state by stable id  </summary>
    private readonly Dictionary<string, bool> _sessionCompletedById = new Dictionary<string, bool>(256);

    /// <summary> Accessed sessions </summary>
    private readonly HashSet<string> _accessedSessionIds = new HashSet<string>();

    private bool _isInitialized;
    #endregion

    #region Snapshot State (Cache)

    /// <summary>Version tăng mỗi khi state đổi.</summary>
    [SerializeField] private int _snapshotVersion;

    private bool _snapshotDirty = true;
    private ProgressSnapshot _cachedSnapshot;

    #endregion

    #region Unity lifecycle

    private void Start()
    {
        Initialize();
        Broadcast_Snapshot_UpdateGlobalData(); // đảm bảo hệ thống khác có snapshot ngay khi start
    }

    #endregion

    #region Inject

    public void Inject(Core context) { _core = context; }

    #endregion

    #region Initialize

    /// <summary>
    /// Initialize: chỉ ensure keys + prune stale. Không giữ index dictionary cố định nữa.
    /// </summary>
    /// <remarks>
    /// - Source-of-truth graph: ProgressSO -> ChapterSO[] -> SessionSO[]
    /// - Id->SO resolve chuyển sang ProgressCatalogSO (generic).
    /// </remarks>
    /// <returns>void</returns>
    public void Initialize()
    {
        if (_isInitialized) return;

        RebuildIndicesAndEnsureKeys(); // giữ tên method để không phá nơi khác
        _isInitialized = true;

        MarkSnapshotDirty();
    }

    /// <summary>
    /// (Giữ tên cũ) RebuildIndicesAndEnsureKeys:
    /// - Ensure _sessionCompletedById có key cho mọi session đang tồn tại trong graph
    /// - Prune key thừa (session đã bị xoá khỏi graph)
    /// - Validate lastSession/lastChapter
    /// </summary>
    /// <returns>void</returns>
    public void RebuildIndicesAndEnsureKeys()
    {
        // Build set ids hiện có trong graph
        var existingSessionIds = new HashSet<string>();

        if (_gameProgressSO != null && _gameProgressSO.Chapter != null)
        {
            var chapters = _gameProgressSO.Chapter;
            for (int c = 0; c < chapters.Length; c++)
            {
                var ch = chapters[c];
                if (ch == null || ch.Sessions == null) continue;

                var sessions = ch.Sessions;
                for (int s = 0; s < sessions.Length; s++)
                {
                    var ss = sessions[s];
                    if (ss == null) continue;

                    string sid = GetStableId(ss);
                    if (string.IsNullOrEmpty(sid)) continue;

                    existingSessionIds.Add(sid);

                    if (!_sessionCompletedById.ContainsKey(sid))
                        _sessionCompletedById.Add(sid, false);
                }
            }
        }

        // prune stale completed keys
        if (_sessionCompletedById.Count > 0)
        {
            var toRemove = new List<string>();
            foreach (var kv in _sessionCompletedById)
                if (!existingSessionIds.Contains(kv.Key))
                    toRemove.Add(kv.Key);

            for (int i = 0; i < toRemove.Count; i++)
                _sessionCompletedById.Remove(toRemove[i]);
        }

        // prune stale accessed ids
        if (_accessedSessionIds.Count > 0)
        {
            var toRemove = new List<string>();
            foreach (var id in _accessedSessionIds)
                if (!existingSessionIds.Contains(id))
                    toRemove.Add(id);

            for (int i = 0; i < toRemove.Count; i++)
                _accessedSessionIds.Remove(toRemove[i]);
        }

        // validate last ids
        if (!string.IsNullOrEmpty(_lastSessionId) && !existingSessionIds.Contains(_lastSessionId))
            _lastSessionId = null;

        if (!string.IsNullOrEmpty(_lastChapterId))
        {
            // chapter validate by traversal (không giữ index)
            if (!TryResolveChapterSOById_NoIndex(_lastChapterId, out _))
                _lastChapterId = null;
        }

        MarkSnapshotDirty();
    }

    #endregion

    #region Save

    /// <summary>
    /// Capture progress state ra DTO để SaveSystem serialize.
    /// </summary>
    /// <returns>ProgressSaveData</returns>
    public ProgressSaveData CaptureSaveData()
    {
        Initialize();

        var data = new ProgressSaveData();

        if (!string.IsNullOrEmpty(_lastSessionId))
        {
            data.LasMap = new ProgressSaveData.LastAccess
            {
                id = _lastMapId,
                atUtcMs = _lastMapnAtUtcMs
            };

            data.lastSession = new ProgressSaveData.LastAccess
            {
                id = _lastSessionId,
                atUtcMs = _lastSessionAtUtcMs
            };
        }

        if (!string.IsNullOrEmpty(_lastChapterId))
        {
            data.lastChapter = new ProgressSaveData.LastAccess
            {
                id = _lastChapterId,
                atUtcMs = _lastChapterAtUtcMs
            };
        }

        // accessed sessions
        data.accessedSessions.Clear();
        foreach (var id in _accessedSessionIds)
            data.accessedSessions.Add(id);

        // chapters + sessions: iterate graph trực tiếp
        data.chapters.Clear();

        if (_gameProgressSO != null && _gameProgressSO.Chapter != null)
        {
            var chapters = _gameProgressSO.Chapter;
            for (int c = 0; c < chapters.Length; c++)
            {
                var ch = chapters[c];
                if (ch == null) continue;

                string chapterId = GetStableId(ch);
                if (string.IsNullOrEmpty(chapterId)) continue;

                var chEntry = new ProgressSaveData.ChapterEntry
                {
                    id = chapterId,
                    completed = IsCompleted(ch)
                };

                if (ch.Sessions != null)
                {
                    var sessions = ch.Sessions;
                    for (int s = 0; s < sessions.Length; s++)
                    {
                        var ss = sessions[s];
                        if (ss == null) continue;

                        string sid = GetStableId(ss);
                        if (string.IsNullOrEmpty(sid)) continue;

                        chEntry.sessions.Add(new ProgressSaveData.SessionEntry
                        {
                            id = sid,
                            completed = _sessionCompletedById.TryGetValue(sid, out var done) && done
                        });
                    }
                }

                data.chapters.Add(chEntry);
            }
        }

        EventAfterSave();
        return data;
    }

    /// <summary>
    /// Apply DTO vào runtime dictionaries và bắn snapshot.
    /// gọi khi load game hoặc sau khi save để restore state.
    /// </summary>
    /// <param name="data">ProgressSaveData</param>
    /// <returns>void</returns>
    public void ApplySaveData(ProgressSaveData data)
    {
        // reset runtime state theo graph hiện tại
        _sessionCompletedById.Clear();
        _accessedSessionIds.Clear();

        _isInitialized = false;
        Initialize(); // ensure keys theo graph

        if (data == null)
        {
            MarkSnapshotDirty();
            Broadcast_Snapshot_UpdateGlobalData();
            return;
        }

        // restore completed (session-level)
        if (data.chapters != null)
        {
            for (int i = 0; i < data.chapters.Count; i++)
            {
                var ch = data.chapters[i];
                if (ch == null || string.IsNullOrEmpty(ch.id)) continue;

                // save cũ có thể không có sessions => apply theo chapter
                if (ch.sessions == null || ch.sessions.Count == 0)
                {
                    SetChapterCompletedById_NoIndex(ch.id, ch.completed);
                    continue;
                }

                for (int j = 0; j < ch.sessions.Count; j++)
                {
                    var s = ch.sessions[j];
                    if (s == null || string.IsNullOrEmpty(s.id)) continue;

                    // chỉ apply nếu session id đang tồn tại trong graph hiện tại
                    if (_sessionCompletedById.ContainsKey(s.id))
                        _sessionCompletedById[s.id] = s.completed;
                }
            }
        }

        // restore accessed
        if (data.accessedSessions != null)
        {
            for (int i = 0; i < data.accessedSessions.Count; i++)
            {
                var id = data.accessedSessions[i];
                if (string.IsNullOrEmpty(id)) continue;

                // chỉ add nếu session id đang tồn tại trong graph hiện tại
                if (_sessionCompletedById.ContainsKey(id))
                    _accessedSessionIds.Add(id);
            }
        }

        // restore last access
        if (data.lastSession != null)
        {
            _lastSessionId = data.lastSession.id;
            _lastSessionAtUtcMs = data.lastSession.atUtcMs;

            // validate
            if (!string.IsNullOrEmpty(_lastSessionId) && !_sessionCompletedById.ContainsKey(_lastSessionId))
                _lastSessionId = null;
        }

        if (data.lastChapter != null)
        {
            _lastChapterId = data.lastChapter.id;
            _lastChapterAtUtcMs = data.lastChapter.atUtcMs;

            // validate by traversal
            if (!string.IsNullOrEmpty(_lastChapterId) && !TryResolveChapterSOById_NoIndex(_lastChapterId, out _))
                _lastChapterId = null;
        }

        MarkSnapshotDirty();
        Broadcast_Snapshot_UpdateGlobalData();
    }

    /// <summary>
    /// Gọi sau khi ApplySaveData xong để notify các hệ khác.
    /// </summary>
    /// <returns>void</returns>
    public void AfterApplySaveData()
    {
        Broadcast_Snapshot_UpdateGlobalData();
    }

    #endregion

    #region Snapshot logic

    /// <summary>
    /// Mark snapshot dirty và tăng version.
    /// </summary>
    /// <returns>void</returns>
    private void MarkSnapshotDirty()
    {
        _snapshotDirty = true;
        _snapshotVersion++;
    }

    /// <summary>
    /// Rebuild snapshot từ SO graph + runtime state.
    /// </summary>
    /// <remarks>
    /// - Snapshot là immutable (array) để tránh listener thấy data bị mutate sau đó.
    /// - CosmicObjectIds unique theo chapter.
    /// </remarks>
    /// <returns>void</returns>
    private void RebuildSnapshot()
    {
        if (_gameProgressSO == null || _gameProgressSO.Chapter == null)
        {
            _cachedSnapshot = new ProgressSnapshot(_snapshotVersion, Array.Empty<ChapterSnapshot>());
            _snapshotDirty = false;
            return;
        }

        var chapters = _gameProgressSO.Chapter;
        var chapterSnaps = new List<ChapterSnapshot>(chapters.Length);

        for (int i = 0; i < chapters.Length; i++)
        {
            var ch = chapters[i];
            if (ch == null) continue;

            string chapterId = GetStableId(ch);
            if (string.IsNullOrEmpty(chapterId)) continue;

            // sessions
            SessionSnapshot[] sessionSnaps = BuildSessionSnapshots(ch);

            // chapter completed derived
            bool chDone = true;
            for (int s = 0; s < sessionSnaps.Length; s++)
            {
                if (!sessionSnaps[s].IsCompleted)
                {
                    chDone = false;
                    break;
                }
            }

            // cosmic ids
            string[] cosmicIds = BuildCosmicIdsOfChapter(ch);

            chapterSnaps.Add(new ChapterSnapshot(
                chapterId,
                ch.name,
                chDone,
                sessionSnaps,
                cosmicIds
            ));
        }

        _cachedSnapshot = new ProgressSnapshot(_snapshotVersion, chapterSnaps.ToArray());
        _snapshotDirty = false;
    }

    private SessionSnapshot[] BuildSessionSnapshots(ChapterSO chapter)
    {
        if (chapter == null || chapter.Sessions == null || chapter.Sessions.Length == 0)
            return Array.Empty<SessionSnapshot>();

        var ss = chapter.Sessions;
        var list = new List<SessionSnapshot>(ss.Length);

        for (int i = 0; i < ss.Length; i++)
        {
            var sso = ss[i];
            if (sso == null) continue;

            string sid = GetStableId(sso);
            if (string.IsNullOrEmpty(sid)) continue;

            bool completed = _sessionCompletedById.TryGetValue(sid, out var c) && c;
            bool accessed = _accessedSessionIds.Contains(sid);

            list.Add(new SessionSnapshot(
                sid,
                sso.name,
                completed,
                accessed
            ));
        }

        return list.ToArray();
    }

    private string[] BuildCosmicIdsOfChapter(ChapterSO chapter)
    {
        if (chapter == null || chapter.Sessions == null || chapter.Sessions.Length == 0)
            return Array.Empty<string>();

        var set = new HashSet<string>();
        var list = new List<string>(64);

        foreach (var session in chapter.Sessions)
        {
            if (session == null || session.mapSO == null) continue;

            var cosmicArray = session.mapSO.cosmicObjectSO;
            if (cosmicArray == null) continue;

            foreach (var cosmic in cosmicArray)
            {
                if (cosmic == null) continue;

                string id = GetStableId(cosmic);
                if (string.IsNullOrEmpty(id)) continue;

                if (set.Add(id))
                    list.Add(id);
            }
        }

        return list.ToArray();
    }

    #endregion

    #region Internal Helpers (Set by Id / Derived) - No Index

    private void SetSessionCompletedById(string sessionId, bool isCompleted)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        if (!_sessionCompletedById.ContainsKey(sessionId)) return; // session không tồn tại trong graph

        if (_sessionCompletedById.TryGetValue(sessionId, out var cur) && cur == isCompleted)
            return;

        _sessionCompletedById[sessionId] = isCompleted;
        MarkSnapshotDirty();
        Broadcast_Snapshot_UpdateGlobalData();
    }

    /// <summary>
    /// Resolve SessionId từ CosmicObjectId bằng cách duyệt toàn bộ graph (không dùng index/cache).
    /// </summary>
    /// <remarks>
    /// - Tìm CosmicObjectSO trong session.mapSO.cosmicObjectSO.
    /// - Trả về session đầu tiên match.
    /// - Nếu 1 cosmic xuất hiện ở nhiều session -> hàm sẽ trả về session gặp trước (có thể mơ hồ).
    /// </remarks>
    /// <returns>true nếu tìm thấy</returns>
    private bool TryResolveSessionIdOfCosmicObject_NoIndex(string cosmicObjectId, out string sessionId)
    {
        sessionId = null;

        if (string.IsNullOrEmpty(cosmicObjectId)) return false;
        if (_gameProgressSO == null || _gameProgressSO.Chapter == null) return false;

        var chapters = _gameProgressSO.Chapter;
        for (int c = 0; c < chapters.Length; c++)
        {
            var ch = chapters[c];
            if (ch == null || ch.Sessions == null) continue;

            var sessions = ch.Sessions;
            for (int s = 0; s < sessions.Length; s++)
            {
                var ss = sessions[s];
                if (ss == null || ss.mapSO == null) continue;

                var cosmicArray = ss.mapSO.cosmicObjectSO;
                if (cosmicArray == null || cosmicArray.Length == 0) continue;

                for (int i = 0; i < cosmicArray.Length; i++)
                {
                    var co = cosmicArray[i];
                    if (co == null) continue;

                    if (GetStableId(co) == cosmicObjectId)
                    {
                        sessionId = GetStableId(ss);
                        return !string.IsNullOrEmpty(sessionId);
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Resolve SessionId từ MapId bằng cách duyệt toàn bộ graph (không dùng index/cache).
    /// </summary>
    /// <remarks>
    /// - Match theo StableId (IStableId.Id), không dùng name.
    /// - Trả về session đầu tiên match.
    /// </remarks>
    /// <returns>true nếu tìm thấy</returns>
    private bool TryResolveSessionIdOfMap_NoIndex(string mapId, out string sessionId)
    {
        sessionId = null;

        if (string.IsNullOrEmpty(mapId)) return false;
        if (_gameProgressSO == null || _gameProgressSO.Chapter == null) return false;

        var chapters = _gameProgressSO.Chapter;
        for (int c = 0; c < chapters.Length; c++)
        {
            var ch = chapters[c];
            if (ch == null || ch.Sessions == null) continue;

            var sessions = ch.Sessions;
            for (int s = 0; s < sessions.Length; s++)
            {
                var ss = sessions[s];
                if (ss == null || ss.mapSO == null) continue;

                // MapSO : BaseSO => có stable id
                if (GetStableId(ss.mapSO) == mapId)
                {
                    sessionId = GetStableId(ss);
                    return !string.IsNullOrEmpty(sessionId);
                }
            }
        }

        return false;
    }

    private void SetChapterCompletedById_NoIndex(string chapterId, bool isCompleted)
    {
        if (string.IsNullOrEmpty(chapterId)) return;
        if (!TryResolveChapterSOById_NoIndex(chapterId, out var chapter) || chapter == null) return;
        if (chapter.Sessions == null) return;

        var sessions = chapter.Sessions;
        for (int i = 0; i < sessions.Length; i++)
        {
            var ss = sessions[i];
            if (ss == null) continue;

            string sid = GetStableId(ss);
            if (string.IsNullOrEmpty(sid)) continue;

            if (_sessionCompletedById.ContainsKey(sid))
                _sessionCompletedById[sid] = isCompleted;
        }

        MarkSnapshotDirty();
        Broadcast_Snapshot_UpdateGlobalData();
    }

    private bool TryResolveChapterSOById_NoIndex(string chapterId, out ChapterSO chapter)
    {
        chapter = null;
        if (string.IsNullOrEmpty(chapterId)) return false;
        if (_gameProgressSO == null || _gameProgressSO.Chapter == null) return false;

        var chapters = _gameProgressSO.Chapter;
        for (int i = 0; i < chapters.Length; i++)
        {
            var ch = chapters[i];
            if (ch == null) continue;

            if (GetStableId(ch) == chapterId)
            {
                chapter = ch;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveChapterIdOfSession_NoIndex(string sessionId, out string chapterId)
    {
        chapterId = null;
        if (string.IsNullOrEmpty(sessionId)) return false;
        if (_gameProgressSO == null || _gameProgressSO.Chapter == null) return false;

        var chapters = _gameProgressSO.Chapter;
        for (int c = 0; c < chapters.Length; c++)
        {
            var ch = chapters[c];
            if (ch == null || ch.Sessions == null) continue;

            var sessions = ch.Sessions;
            for (int s = 0; s < sessions.Length; s++)
            {
                var ss = sessions[s];
                if (ss == null) continue;

                if (GetStableId(ss) == sessionId)
                {
                    chapterId = GetStableId(ch);
                    return !string.IsNullOrEmpty(chapterId);
                }
            }
        }

        return false;
    }

    #endregion

    #region Stable Id Helper

    /// <summary>
    /// Lấy stable id theo chuẩn:
    /// - Ưu tiên IStableId.Id (BaseSO đã có)
    /// - Fallback: so.name (chỉ để tránh crash với SO lạ)
    /// </summary>
    /// <param name="so">ScriptableObject</param>
    /// <returns>string</returns>
    private static string GetStableId(ScriptableObject so)
    {
        if (so == null) return null;

        if (so is IStableId stable && !string.IsNullOrEmpty(stable.Id))
            return stable.Id;

        return so.name;
    }

    #endregion
}

// Profession
public partial class ProgressManager
{
    private void EndSession(SessionSO so, bool isComplete)
    {
        SetCompleted(so, isComplete);
    }
}

// Event
public partial class ProgressManager : CoreEventBase
{
    public override void SubscribeEvents()
    {
        CoreEvents.OnEndSession.Subscribe(e => EndSession(e.SessionSO, e.IsComplete), Binder);

        // phát hiện ReturnDataEvent từ hệ thống khác
        CoreEvents.ReturnData.Subscribe(e => OnReturnDataEvent(e), Binder);
    }

    private void OnReturnDataEvent(ReturnDataEvent e)
    {
        if (e.eventType == CoreEventType.Request) { }
        else if (e.eventType == CoreEventType.Response)
        {
            Response_ReturnDataToBase_UpdateBase(e);
        }
    }

    /// <summary>
    /// Bắn request để thu thập Data (chuẩn bị save).
    /// </summary>
    /// <returns>void</returns>
    public void Request_ReturnDataToBase_PrepareSave()
    {
        _secondaryStateBeforeSave = _core?.SecondaryStateMachine?.CurrentStateType;
        CoreEvents.OnSaveGame.Raise();

        // Yêu cầu lấy data đến toàn bộ hệ thống
        CoreEvents.ReturnData.Raise(new ReturnDataEvent(
            CoreEventType.Request,
            null
        ));
    }

    /// <summary>
    /// Nhận response từ hệ thống khác trả về
    /// </summary>
    /// <param name="e">response</param>
    /// <returns>void</returns>
    private void Response_ReturnDataToBase_UpdateBase(ReturnDataEvent e)
    {
        MarkSessionAccessedById(e.returnId);
    }

    /// <summary>
    /// Phát ProgressSnapshot ra ngoài cho các hệ thống khác lấy
    /// </summary>
    /// <remarks>gọi khi data thay đổi</remarks>
    /// <returns>void</returns>
    private void Broadcast_Snapshot_UpdateGlobalData()
    {
        // đang save thì cút nhé
        if (_core?.SecondaryStateMachine?.CurrentStateType == typeof(OnSaveGameState)) return;

        var snap = GetSnapshot();

        // gửi ProgressSnapshot cho các hệ thống khác lấy
        CoreEvents.ReturnData.Raise(new ReturnDataEvent(
            CoreEventType.Send,
            SnapShotType.ProgressSnapshot,
            snap
        ));
    }

    private void EventAfterSave()
    {
        var machine = _core?.SecondaryStateMachine;
        if (machine == null) return;

        Type stateToRestore = _secondaryStateBeforeSave ?? typeof(NoneStateSecondary);
        _secondaryStateBeforeSave = null;
        machine.ChangeState(stateToRestore);
    }
}

public interface IProgressQuery
{
    /// <summary> Lấy ProgressSnapshot hiện tại (cache). </summary>
    /// <returns>ProgressSnapshot</returns>
    ProgressSnapshot GetSnapshot();

    /// <summary> Query last session id (stable). </summary>
    /// <returns>string</returns>
    string GetLastSessionId();

    /// <summary> Query last chapter id (stable). </summary>
    /// <returns>string</returns>
    string GetLastChapterId();

    /// <summary> Resolve SessionSO theo sessionId. </summary>
    /// <returns>true nếu tìm thấy</returns>
    bool TryGetSessionSOById(string sessionId, out SessionSO sessionSO);


    /// //////////////////////////////////////////////////////////// TÀ THUẬT
    bool TryGetLastMapSO(out CosmicObjectSO mapSO);

    /// <summary> Resolve last SessionSO nếu có. </summary>
    /// <returns>true nếu resolve được</returns>
    bool TryGetLastSessionSO(out SessionSO so);

    /// <summary>
    /// Resolve CosmicObjectSO theo id (ưu tiên Catalog, fallback traversal).
    /// </summary>
    /// <remarks>Read-only.</remarks>
    /// <returns>true nếu resolve được</returns>
    bool TryGetCosmicObjectSOById(string id, out CosmicObjectSO so);

    /// <summary>
    /// Resolve CosmicObjectSO theo (sessionId, index) trong list cosmicObjectSO của session.mapSO.
    /// </summary>
    /// <remarks>index phụ thuộc thứ tự asset; đổi thứ tự => đổi kết quả.</remarks>
    /// <returns>true nếu resolve được</returns>
    bool TryGetCosmicObjectSOByIndexInSessionId(string sessionId, int indexInList, out CosmicObjectSO so);

    /// <summary>
    /// Resolve CosmicObjectSO theo (SessionSO, index) trong list cosmicObjectSO của session.mapSO.
    /// </summary>
    /// <remarks>index phụ thuộc thứ tự asset; đổi thứ tự => đổi kết quả.</remarks>
    /// <returns>true nếu resolve được</returns>
    bool TryGetCosmicObjectSOByIndexInSessionSO(SessionSO sessionSO, int indexInList, out CosmicObjectSO so);
}

// Public API
public partial class ProgressManager
{
    /// <summary>
    /// Lấy ProgressSnapshot hiện tại (cache).
    /// </summary>
    /// <returns>ProgressSnapshot</returns>
    public ProgressSnapshot GetSnapshot()
    {
        Initialize();

        if (_snapshotDirty)
            RebuildSnapshot();

        return _cachedSnapshot;
    }

    /// <summary>
    /// Query last session id (stable).
    /// </summary>
    /// <returns>string</returns>
    public string GetLastSessionId() => _lastSessionId;

    /// <summary>
    /// Query last chapter id (stable).
    /// </summary>
    /// <returns>string</returns>
    public string GetLastChapterId() => _lastChapterId;

    /// <summary>
    /// Resolve SessionSO theo id:
    /// - Ưu tiên Catalog (O(1))
    /// - Fallback traversal graph (O(N))
    /// </summary>
    /// <remarks>Không còn dùng _sessionById dictionary.</remarks>
    /// <returns>true nếu tìm thấy</returns>
    public bool TryGetSessionSOById(string sessionId, out SessionSO sessionSO)
    {
        Initialize();
        sessionSO = null;

        if (string.IsNullOrEmpty(sessionId)) return false;

        // 1) Catalog (generic)
        var catalog = Catalog;
        if (catalog != null && catalog.TryGet<SessionSO>(sessionId, out var so) && so != null)
        {
            sessionSO = so;
            return true;
        }

        // 2) Fallback traversal graph
        if (_gameProgressSO == null || _gameProgressSO.Chapter == null) return false;

        var chapters = _gameProgressSO.Chapter;
        for (int c = 0; c < chapters.Length; c++)
        {
            var ch = chapters[c];
            if (ch == null || ch.Sessions == null) continue;

            var sessions = ch.Sessions;
            for (int s = 0; s < sessions.Length; s++)
            {
                var ss = sessions[s];
                if (ss == null) continue;

                if (GetStableId(ss) == sessionId)
                {
                    sessionSO = ss;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Resolve lastSessionSo.
    /// </summary>
    /// <returns>true nếu có lastSessionId và resolve được SessionSO</returns>
    public bool TryGetLastSessionSO(out SessionSO so)
    {
        so = null;
        var id = GetLastSessionId();
        if (string.IsNullOrEmpty(id)) return false;

        return TryGetSessionSOById(id, out so);
    }

    // TÀ THUẬT tên là GetMap mà trả ra CosmicObjectSO :( ///////////////////////////////////////////// CẦN FIX SAU NHÉ 
    public string GetLastMapId() => _lastMapId;
    public bool TryGetLastMapSO(out CosmicObjectSO so)
    {
        so = null;
        var id = GetLastMapId();

        if (string.IsNullOrEmpty(id)) return false;

        return TryGetCosmicObjectSOById(id, out so);
    }

    /// <summary>
    /// Resolve CosmicObjectSO theo id:
    /// - Ưu tiên Catalog (O(1))
    /// - Fallback traversal graph (O(N))
    /// </summary>
    /// <remarks>Read-only.</remarks>
    /// <returns>true nếu tìm thấy</returns>
    public bool TryGetCosmicObjectSOById(string id, out CosmicObjectSO so)
    {
        Initialize();
        so = null;

        if (string.IsNullOrEmpty(id)) return false;

        // 1) Catalog (generic)
        var catalog = Catalog; // Catalog là property của ProgressManager :contentReference[oaicite:4]{index=4}
        if (catalog != null && catalog.TryGet<CosmicObjectSO>(id, out var found) && found != null)
        {
            so = found;
            return true;
        }

        // 2) Fallback traversal graph
        if (_gameProgressSO == null || _gameProgressSO.Chapter == null) return false;

        var chapters = _gameProgressSO.Chapter;
        for (int c = 0; c < chapters.Length; c++)
        {
            var ch = chapters[c];
            if (ch == null || ch.Sessions == null) continue;

            var sessions = ch.Sessions;
            for (int s = 0; s < sessions.Length; s++)
            {
                var ss = sessions[s];
                if (ss == null || ss.mapSO == null) continue;

                var cosmicArray = ss.mapSO.cosmicObjectSO;
                if (cosmicArray == null) continue;

                for (int i = 0; i < cosmicArray.Length; i++)
                {
                    var co = cosmicArray[i];
                    if (co == null) continue;

                    if (GetStableId(co) == id)
                    {
                        so = co;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Resolve CosmicObjectSO theo (sessionId, index) trong list cosmicObjectSO của session.mapSO.
    /// </summary>
    /// <remarks>
    /// - Ưu tiên resolve SessionSO từ Catalog (O(1)), fallback traversal (O(N)).
    /// - index phụ thuộc thứ tự trong mảng cosmicObjectSO (asset order).
    /// </remarks>
    /// <returns>true nếu resolve được</returns>
    public bool TryGetCosmicObjectSOByIndexInSessionId(string sessionId, int indexInList, out CosmicObjectSO so)
    {
        so = null;

        if (string.IsNullOrEmpty(sessionId)) return false;
        if (indexInList < 0) return false;

        if (!TryGetSessionSOById(sessionId, out var sessionSO) || sessionSO == null)
            return false;

        return TryGetCosmicObjectSOByIndexInSessionSO(sessionSO, indexInList, out so);
    }

    /// <summary>
    /// Resolve CosmicObjectSO theo (SessionSO, index) trong list cosmicObjectSO của session.mapSO.
    /// </summary>
    /// <remarks>
    /// - Không đụng Catalog vì cosmic nằm trong graph của SessionSO -> mapSO.
    /// - index phụ thuộc thứ tự trong mảng cosmicObjectSO (asset order).
    /// </remarks>
    /// <returns>true nếu resolve được</returns>
    public bool TryGetCosmicObjectSOByIndexInSessionSO(SessionSO sessionSO, int indexInList, out CosmicObjectSO so)
    {
        Initialize();
        so = null;

        if (sessionSO == null) return false;
        if (indexInList < 0) return false;

        // sessionSO.mapSO.cosmicObjectSO :contentReference[oaicite:4]{index=4}
        var map = sessionSO.mapSO;
        if (map == null) return false;

        var cosmicArray = map.cosmicObjectSO;
        if (cosmicArray == null) return false;
        if ((uint)indexInList >= (uint)cosmicArray.Length) return false;

        var picked = cosmicArray[indexInList];
        if (picked == null) return false;

        so = picked;
        return true;
    }

    #region Queries (Public)

    /// <summary>
    /// Query trạng thái completed của 1 SO (Chapter hoặc Session).
    /// </summary>
    /// <param name="so">ScriptableObject</param>
    /// <returns>bool</returns>
    public bool IsCompleted(ScriptableObject so)
    {
        Initialize();
        if (so == null) return false;

        if (so is SessionSO ss)
        {
            string id = GetStableId(ss);
            return !string.IsNullOrEmpty(id) && _sessionCompletedById.TryGetValue(id, out var c) && c;
        }

        if (so is ChapterSO ch)
        {
            if (ch.Sessions == null || ch.Sessions.Length == 0) return false;

            for (int i = 0; i < ch.Sessions.Length; i++)
            {
                var sso = ch.Sessions[i];
                if (sso == null) return false;

                string sid = GetStableId(sso);
                if (string.IsNullOrEmpty(sid)) return false;

                if (!_sessionCompletedById.TryGetValue(sid, out var done) || !done)
                    return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Lấy tất cả ChapterSO kèm trạng thái hoàn thành (derived).
    /// </summary>
    /// <returns>IReadOnlyList</returns>
    public IReadOnlyList<ProgressEntry<ChapterSO>> GetAllChaptersWithStatus()
    {
        Initialize();

        if (_gameProgressSO == null || _gameProgressSO.Chapter == null)
            return Array.Empty<ProgressEntry<ChapterSO>>();

        var chapters = _gameProgressSO.Chapter;
        var result = new List<ProgressEntry<ChapterSO>>(chapters.Length);

        for (int i = 0; i < chapters.Length; i++)
        {
            var ch = chapters[i];
            if (ch == null) continue;

            result.Add(new ProgressEntry<ChapterSO>(ch, IsCompleted(ch)));
        }

        return result;
    }

    /// <summary>
    /// Lấy tất cả SessionSO kèm trạng thái hoàn thành.
    /// </summary>
    /// <returns>IReadOnlyList</returns>
    public IReadOnlyList<ProgressEntry<SessionSO>> GetAllSessionsWithStatus()
    {
        Initialize();

        if (_gameProgressSO == null || _gameProgressSO.Chapter == null)
            return Array.Empty<ProgressEntry<SessionSO>>();

        // capacity estimate
        int estimate = 0;
        var chapters = _gameProgressSO.Chapter;
        for (int c = 0; c < chapters.Length; c++)
            estimate += chapters[c]?.Sessions?.Length ?? 0;

        var result = new List<ProgressEntry<SessionSO>>(Mathf.Max(estimate, 8));

        for (int c = 0; c < chapters.Length; c++)
        {
            var ch = chapters[c];
            if (ch == null || ch.Sessions == null) continue;

            var sessions = ch.Sessions;
            for (int s = 0; s < sessions.Length; s++)
            {
                var ss = sessions[s];
                if (ss == null) continue;

                string sid = GetStableId(ss);
                bool completed = !string.IsNullOrEmpty(sid) && _sessionCompletedById.TryGetValue(sid, out var done) && done;

                result.Add(new ProgressEntry<SessionSO>(ss, completed));
            }
        }

        return result;
    }

    #endregion

    #region Mutations Public

    /// <summary>
    /// API tổng quát: set completed cho ScriptableObject
    /// </summary>
    /// <param name="so">ChapterSO hoặc SessionSO</param>
    /// <param name="isCompleted">true/false</param>
    /// <remarks>
    /// - Nếu set Session -> chỉ đổi session
    /// - Nếu set Chapter -> set toàn bộ session trong chapter
    /// </remarks>
    /// <returns>void</returns>
    public void SetCompleted(ScriptableObject so, bool isCompleted)
    {
        Initialize();
        if (so == null) return;

        if (so is SessionSO ss)
        {
            SetSessionCompletedById(GetStableId(ss), isCompleted);
            return;
        }

        if (so is ChapterSO ch)
        {
            SetChapterCompletedById_NoIndex(GetStableId(ch), isCompleted);
            return;
        }
    }

    /// <summary>
    /// Mark accessed bằng session id (không cần SessionSO object).
    /// </summary>
    /// <param name="sessionId">global-unique session id</param>
    /// <returns>void</returns>
    public void MarkSessionAccessedById(string mapId)
    {
        Initialize();
        if (string.IsNullOrEmpty(mapId)) return;
        //if (!_sessionCompletedById.ContainsKey(mapId)) return; // session không tồn tại trong graph

        _lastMapId = mapId;
        _lastMapnAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // SAU NÀY THAY HÀM NÀY BẰNG MÁP THỰC SỰ NHÉ :_ [
        if (TryResolveSessionIdOfCosmicObject_NoIndex(mapId, out var sessionId) && !string.IsNullOrEmpty(sessionId))
        {
            _lastSessionId = sessionId;
            _lastSessionAtUtcMs = _lastMapnAtUtcMs;
        }

        // resolve chapterId bằng traversal (không index)
        if (TryResolveChapterIdOfSession_NoIndex(sessionId, out var chapterId) && !string.IsNullOrEmpty(chapterId))
        {
            _lastChapterId = chapterId;
            _lastChapterAtUtcMs = _lastMapnAtUtcMs;
        }

        _accessedSessionIds.Add(sessionId);

        MarkSnapshotDirty();
        Broadcast_Snapshot_UpdateGlobalData();
    }

    /// <summary>
    /// helper markMapID
    /// </summary>
    /// <param name="mapId"></param>
    public void MarkMapAccessedById(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return;


    }

    #endregion
}


#if UNITY_EDITOR

[CustomEditor(typeof(ProgressManager))]
public class ProgressManagerEditor : Editor
{
    private ProgressManager _target;
    private SerializedProperty _gameProgressSOProp;

    private int _chapterIndex;
    private int _sessionIndex;
    private bool _testIsCompleted;

    private enum PickMode { Chapter, Session }
    private PickMode _mode = PickMode.Session;

    private void OnEnable()
    {
        _target = (ProgressManager)target;
        _gameProgressSOProp = serializedObject.FindProperty("_gameProgressSO");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Progress Tools", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Ensure Keys & Rebuild Snapshot"))
            {
                _target.RebuildIndicesAndEnsureKeys();
                _target.GetSnapshot();
                EditorUtility.SetDirty(_target);
                Repaint();
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Test Set Completed (From GameProgressSO)", EditorStyles.boldLabel);

        var gp = _gameProgressSOProp != null ? _gameProgressSOProp.objectReferenceValue as ProgressSO : null;
        if (gp == null || gp.Chapter == null || gp.Chapter.Length == 0)
        {
            EditorGUILayout.HelpBox("Chưa gán GameProgressSO hoặc Chapter rỗng.", MessageType.Warning);
            return;
        }

        _mode = (PickMode)EditorGUILayout.EnumPopup("Pick Mode", _mode);

        var chapters = gp.Chapter;
        var chapterNames = new string[chapters.Length];
        for (int i = 0; i < chapters.Length; i++)
            chapterNames[i] = chapters[i] != null ? chapters[i].name : "(null)";

        _chapterIndex = Mathf.Clamp(_chapterIndex, 0, chapters.Length - 1);
        _chapterIndex = EditorGUILayout.Popup("Chapter (from GP)", _chapterIndex, chapterNames);

        var selectedChapter = chapters[_chapterIndex];
        if (selectedChapter == null)
        {
            EditorGUILayout.HelpBox("Chapter đang null.", MessageType.Warning);
            return;
        }

        ScriptableObject picked = null;

        if (_mode == PickMode.Chapter)
        {
            picked = selectedChapter;
            EditorGUILayout.LabelField("Picked SO", $"CHAPTER | {selectedChapter.name}");
        }
        else
        {
            var sessions = selectedChapter.Sessions;
            if (sessions == null || sessions.Length == 0)
            {
                EditorGUILayout.HelpBox("Chapter này không có Sessions.", MessageType.Info);
                return;
            }

            var sessionNames = new string[sessions.Length];
            for (int i = 0; i < sessions.Length; i++)
                sessionNames[i] = sessions[i] != null ? sessions[i].name : "(null)";

            _sessionIndex = Mathf.Clamp(_sessionIndex, 0, sessions.Length - 1);
            _sessionIndex = EditorGUILayout.Popup("Session (from Chapter)", _sessionIndex, sessionNames);

            var selectedSession = sessions[_sessionIndex];
            if (selectedSession == null)
            {
                EditorGUILayout.HelpBox("Session đang null.", MessageType.Warning);
                return;
            }

            picked = selectedSession;
            EditorGUILayout.LabelField("Picked SO", $"SESSION | {selectedSession.name}");
        }

        bool current = _target.IsCompleted(picked);
        EditorGUILayout.LabelField("Current (Runtime)", current ? "TRUE" : "FALSE");

        _testIsCompleted = EditorGUILayout.Toggle("Set To", _testIsCompleted);

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Apply SetCompleted"))
            {
                _target.Initialize();
                _target.SetCompleted(picked, _testIsCompleted);
                EditorUtility.SetDirty(_target);
                Repaint();
            }

            if (GUILayout.Button("Toggle"))
            {
                _target.Initialize();

                bool next = !_target.IsCompleted(picked);
                _target.SetCompleted(picked, next);
                _testIsCompleted = next;

                EditorUtility.SetDirty(_target);
                Repaint();
            }
        }

        EditorGUILayout.EndHorizontal();
    }
}

#endif
