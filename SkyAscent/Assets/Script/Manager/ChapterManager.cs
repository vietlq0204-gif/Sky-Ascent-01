using UnityEngine;

/// <summary>
/// ChapterManager
/// - Nhận ProgressSnapshot từ ProgressManager
/// - Chọn ChapterSnapshot hiện tại
/// - Phân phối cho ChapterController + SessionController
/// - Nhận thay đổi current session và trả sessionId về ProgressManager (qua ReturnDataEvent.Response)
/// </summary>
public partial class ChapterManager : IChapterQuery, IInject<IProgressQuery>
{
    #region reference
    IProgressQuery _progressQuery;
    #endregion

    #region State Runtime
    private int lastProgressVersion = -1;
    private ChapterSnapshot cachedChapter;
    private bool hasChapter;

    #endregion

    #region Impector
    [Header("Optional override")]
    [SerializeField] private string _forceChapterId;
    #endregion

    #region Inject

    //public void Inject(ProgressManager context) => _progressQuery = context;

    public void Inject (IProgressQuery context) => _progressQuery = context;

    #endregion

    #region  Logic

    /// <summary>
    /// chọn chapter hiện tại và phân phối xuống tầng 2.
    /// </summary>
    /// <remarks>Giảm spam: chỉ xử lý khi Version đổi.</remarks>
    /// <returns>void</returns>
    private void HandleProgressSnapshot(ProgressSnapshot snap)
    {
        if (snap.Chapters == null || snap.Chapters.Length == 0) return;
        if (snap.Version == lastProgressVersion) return;

        lastProgressVersion = snap.Version;

        var selected = SelectChapterToUse(snap);
        if (!selected.HasValue) return;

        cachedChapter = selected.Value;
        hasChapter = true;

        // Gửi ChapterSnapshot cho ChapterController
        SendChapterSnapshot(cachedChapter);

        //// Auto target last session
        //AutoTargetLastSessionToUI();
    }

    /// <summary>
    /// Helper Chọn chapter theo force / lastChapter / fallback.
    /// </summary>
    /// <remarks>Logic này trước đây nằm ở ChapterView.SelectChapterToLoad :contentReference[oaicite:15]{index=15}</remarks>
    /// <returns>ChapterSnapshot?</returns>
    private ChapterSnapshot? SelectChapterToUse(ProgressSnapshot snap)
    {
        // Force
        if (!string.IsNullOrEmpty(_forceChapterId))
        {
            for (int i = 0; i < snap.Chapters.Length; i++)
                if (snap.Chapters[i].ChapterId == _forceChapterId)
                    return snap.Chapters[i];
        }

        // Last chapter from ProgressManager
        string lastId = _progressQuery != null ? _progressQuery.GetLastChapterId() : null;
        if (!string.IsNullOrEmpty(lastId))
        {
            for (int i = 0; i < snap.Chapters.Length; i++)
                if (snap.Chapters[i].ChapterId == lastId)
                    return snap.Chapters[i];
        }

        // Fallback
        return snap.Chapters[0];
    }

    /// <summary>
    /// Helper Rebuild Chapter From Progress
    /// </summary>
    private void RebuildCurrentChapterFromProgress()
    {
        if (_progressQuery == null) return;
        HandleProgressSnapshot(_progressQuery.GetSnapshot());
    }

    #endregion
}

// Event
public partial class ChapterManager : CoreEventBase
{
    public override void SubscribeEvents()
    {
        // Phát hiện ReturnDataEvent từ hệ thống khác
        CoreEvents.ReturnData.Subscribe(OnReturnDataEvent, Binder);
    }

    private void OnReturnDataEvent(ReturnDataEvent e)
    {
        // Nhận ProgressSnapshot từ ProgressManager
        if (e.eventType == CoreEventType.Send)
        {
            if (e.snapshotType == SnapShotType.ProgressSnapshot)
            {
                HandleProgressSnapshot(e.progressSnapshot);
                return;
            }
        }

        // ở đâu đó yêu cầu lấy ChapterSnapshot từ cache || build lại
        if (e.eventType == CoreEventType.Request)
        {
            if (e.snapshotType == SnapShotType.ChapterSnapshot)
            {
                // Nếu isDitry=false thì trả cache, ngược lại rebuild
                if (!e.isDitry && hasChapter)
                {
                    SendChapterSnapshot(cachedChapter);
                }
                else
                {
                    RebuildCurrentChapterFromProgress();
                }
            }

        }
    }

    private void Response_ChapterSnapshotToBase_UpdateBase(ChapterSnapshot chapterSnapshot)
    {
        CoreEvents.ReturnData.Raise(new ReturnDataEvent(
            CoreEventType.Response,
            SnapShotType.ChapterSnapshot,
            chapterSnapshot
            ));
    }

    /// <summary>
    /// Gửi ChapterSnapshot xuống ChapterController bằng ReturnDataEvent.
    /// </summary>
    /// <remarks>Giữ chuẩn event của Ní, không tạo event mới.</remarks>
    /// <returns>void</returns>
    private void SendChapterSnapshot(ChapterSnapshot chapter)
    {
        // Gửi ChapterSnapshot cho bất kì đâu cần
        CoreEvents.ReturnData.Raise(new ReturnDataEvent(
            CoreEventType.Send,
            SnapShotType.ChapterSnapshot,
            chapter,
            false // data này là cache
        ));
    }
}

public interface IChapterQuery
{
    /// <summary>
    /// Lấy ChapterSnapshot hiện tại từ cache.
    /// </summary>
    /// <remarks>Read-only cache.</remarks>
    /// <returns>bool</returns>
    bool TryGetCurrentChapterSnapshot(out ChapterSnapshot snapshot);
}

// API
public partial class ChapterManager
{
    /// <summary>
    /// Lấy ChapterSnapshot hiện tại từ cache của ChapterManager.
    /// </summary>
    /// <remarks>Không rebuild ở đây.</remarks>
    /// <returns>bool</returns>
    public bool TryGetCurrentChapterSnapshot(out ChapterSnapshot snapshot)
    {
        snapshot = default;
        if (!hasChapter) return false;

        snapshot = cachedChapter;
        return true;
    }

}
