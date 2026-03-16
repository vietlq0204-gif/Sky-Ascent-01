using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SessionController
/// - Giữ current session
/// - Resolve session từ cosmicObjectName dựa trên pool sessions mà ChapterManager cấp
/// </summary>
public partial class SessionController : IInject<IProgressQuery>, IInject<Core>
{
    #region Reference
    private Core _core;
    private IProgressQuery _progressQuery;

    #endregion

    [SerializeField] private List<SessionSO> availableSessions = new();
    [SerializeField] private SessionSO currentSessionSO;
    public SessionSO SessionSO => currentSessionSO;

    [SerializeField] private CosmicObjectSO currentCosmicObjectSO;
    public CosmicObjectSO CosmicObjectSO => currentCosmicObjectSO;

    #region Inject

    public void Inject(Core context) => _core = context;
    public void Inject(IProgressQuery context) => _progressQuery = context;

    #endregion

    /// <summary>
    /// Build _availableSessions từ ChapterSnapshot.Sessions.
    /// </summary>
    /// <remarks>Resolve SessionSO bằng IChapterQuery để không phụ thuộc ProgressManager.</remarks>
    /// <returns>void</returns>
    private void CacheSessionsFromChapterSnapshot(in ChapterSnapshot chapter)
    {
        availableSessions.Clear();
        if (_progressQuery == null) return;

        var sessions = chapter.Sessions;
        if (sessions == null || sessions.Length == 0) return;

        for (int i = 0; i < sessions.Length; i++)
        {
            string sid = sessions[i].SessionId;
            if (_progressQuery.TryGetSessionSOById(sid, out var so) && so != null)
                availableSessions.Add(so);
        }
    }

    /// <summary>
    /// Áp dụng target cosmic name để chọn currentSessionSO và lưu currentCosmicObject.
    /// </summary>
    /// <remarks>
    /// - Ưu tiên session có cosmic cuối (last) trùng name (phù hợp “điểm đích”).
    /// - Nếu không có, fallback tìm cosmic ở bất kỳ index nào.
    /// </remarks>
    public void ApplyTargetCosmicObjectName(string name)
    {
        if (TryResolveLinkCosmic_ByABRule(name, out var sessionSO, out var cosmicSO))
        {
            currentSessionSO = sessionSO;
            currentCosmicObjectSO = cosmicSO; // lưu để fallback sau này
            return;
        }

        // Không tìm thấy: clear session, có thể tùy ý clear cosmic hoặc giữ lại “last found”
        currentSessionSO = null;
        currentCosmicObjectSO = null;
    }

    /// <summary>
    /// helpeer Tìm session + cosmic theo cosmicName.
    /// </summary>
    /// <remarks>
    /// Two-pass:
    /// 1) Match cosmic cuối (last) => phù hợp target/đích.
    /// 2) Fallback match cosmic ở bất kỳ vị trí nào.
    /// </remarks>
    /// <returns>true nếu tìm thấy</returns>
    private bool TryResolveSessionAndCosmicByName(string cosmicName, out SessionSO sessionSO, out CosmicObjectSO cosmicSO)
    {
        sessionSO = null;
        cosmicSO = null;

        if (string.IsNullOrEmpty(cosmicName)) return false;

        // ưu tiên cosmic cuối (last)
        for (int i = 0; i < availableSessions.Count; i++)
        {
            var session = availableSessions[i];
            if (session == null || session.mapSO == null) continue;

            var cosmicArray = session.mapSO.cosmicObjectSO;
            if (cosmicArray == null || cosmicArray.Length == 0) continue;

            var last = cosmicArray[cosmicArray.Length - 1];
            if (last != null && last.name == cosmicName)
            {
                sessionSO = session;
                cosmicSO = last;
                return true;
            }
        }

        // fallback tìm ở bất kỳ index
        for (int i = 0; i < availableSessions.Count; i++)
        {
            var session = availableSessions[i];
            if (session == null || session.mapSO == null) continue;

            var cosmicArray = session.mapSO.cosmicObjectSO;
            if (cosmicArray == null || cosmicArray.Length == 0) continue;

            for (int k = 0; k < cosmicArray.Length; k++)
            {
                var c = cosmicArray[k];
                if (c != null && c.name == cosmicName)
                {
                    sessionSO = session;
                    cosmicSO = c;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Resolve Cosmic theo rule A/B (đã sửa theo yêu cầu của Ní):
    /// - Quy ước: phần tử đầu tiên của mảng = A, phần tử cuối cùng = B.
    /// - Nếu A của session hiện tại khớp với B của session liền kề trước đó => trả về A và session = session hiện tại.
    /// - Trường hợp biên:
    ///   + cosmicName khớp A của session đầu tiên (không có session liền trước) => trả về A và session = session đó (KHÔNG null)
    ///   + cosmicName khớp B của session cuối cùng (không có session liền sau) => trả về B và session = null
    /// </summary>
    /// <param name="cosmicName">Tên Cosmic cần resolve.</param>
    /// <param name="sessionOfA">
    /// Session mà phần tử được xác định là A thuộc về.
    /// - Biên A (session đầu): sessionOfA = session đầu.
    /// - Biên B (session cuối): sessionOfA = null.
    /// - Nội bộ: sessionOfA = session hiện tại (nơi cosmic được xác định là A).
    /// </param>
    /// <param name="cosmicSO">Cosmic object resolve được (A hoặc B theo rule).</param>
    /// <returns>True nếu resolve được, ngược lại False.</returns>
    /// <remarks>
    /// - “Liền kề” được hiểu là liền kề giữa các session HỢP LỆ (có mapSO + array cosmic có phần tử).
    /// - “Khớp” hiện tại dựa trên name, so sánh StringComparison.Ordinal.
    ///   Nếu Ní muốn khớp theo reference (cùng asset) thì thay so sánh name bằng (currA == prevB).
    /// </remarks>
    private bool TryResolveLinkCosmic_ByABRule(string cosmicName, out SessionSO sessionOfA, out CosmicObjectSO cosmicSO)
    {
        sessionOfA = null;
        cosmicSO = null;

        if (string.IsNullOrEmpty(cosmicName)) return false;
        if (availableSessions == null || availableSessions.Count == 0) return false;

        // --- 1) Tìm session hợp lệ đầu tiên
        SessionSO firstValid = null;
        for (int i = 0; i < availableSessions.Count; i++)
        {
            var s = availableSessions[i];
            if (s == null || s.mapSO == null) continue;

            var arr = s.mapSO.cosmicObjectSO;
            if (arr == null || arr.Length == 0) continue;

            firstValid = s;
            break;
        }

        if (firstValid == null) return false;

        // --- 2) Tìm session hợp lệ cuối cùng
        SessionSO lastValid = null;
        for (int i = availableSessions.Count - 1; i >= 0; i--)
        {
            var s = availableSessions[i];
            if (s == null || s.mapSO == null) continue;

            var arr = s.mapSO.cosmicObjectSO;
            if (arr == null || arr.Length == 0) continue;

            lastValid = s;
            break;
        }

        if (lastValid == null) return false;

        // --- 3) Check BIÊN A (đã sửa): A của session đầu tiên => session = firstValid (không null)
        {
            var arr = firstValid.mapSO.cosmicObjectSO;
            var a = arr[0]; // A
            if (a != null && string.Equals(a.name, cosmicName, StringComparison.Ordinal))
            {
                cosmicSO = a;
                sessionOfA = firstValid; // CHANGED: không null
                return true;
            }
        }

        // --- 4) Check BIÊN B: B của session cuối cùng => session = null
        {
            var arr = lastValid.mapSO.cosmicObjectSO;
            var b = arr[arr.Length - 1]; // B
            if (b != null && string.Equals(b.name, cosmicName, StringComparison.Ordinal))
            {
                cosmicSO = b;
                sessionOfA = null; // giữ nguyên rule biên B
                return true;
            }
        }

        // --- 5) Check NỘI BỘ: A(curr) khớp B(prev) trên chuỗi session hợp lệ
        SessionSO prevValid = firstValid;

        for (int i = 0; i < availableSessions.Count; i++)
        {
            var curr = availableSessions[i];
            if (curr == null || curr.mapSO == null) continue;

            var currArr = curr.mapSO.cosmicObjectSO;
            if (currArr == null || currArr.Length == 0) continue;

            // Bỏ qua chính firstValid (vì nó không có "prev" hợp lệ trước nó trong chuỗi hợp lệ)
            if (ReferenceEquals(curr, firstValid)) continue;

            var prevArr = prevValid.mapSO.cosmicObjectSO;

            var prevB = prevArr[prevArr.Length - 1]; // B(prev)
            var currA = currArr[0];                  // A(curr)

            if (prevB != null && currA != null)
            {
                bool matchName =
                    string.Equals(currA.name, cosmicName, StringComparison.Ordinal) &&
                    string.Equals(prevB.name, cosmicName, StringComparison.Ordinal) &&
                    string.Equals(currA.name, prevB.name, StringComparison.Ordinal);

                if (matchName)
                {
                    cosmicSO = currA;   // trả về A(curr)
                    sessionOfA = curr;  // session mà cosmic được xác định là A
                    return true;
                }

                // Nếu muốn khớp theo reference asset (an toàn hơn name), dùng:
                // if (currA == prevB && string.Equals(currA.name, cosmicName, StringComparison.Ordinal)) { ... }
            }

            prevValid = curr;
        }

        return false;
    }

}

public partial class SessionController : CoreEventBase
{
    public override void SubscribeEvents()
    {
        CoreEvents.ReturnData.Subscribe(e => OnReturnDataEvent(e));

        // nghe UI target name từ ViewObjcetOnRoad 
        CoreEvents.TargetObject.Subscribe(e =>
        {
            if (e.TypeOfTarget == TargetObjectEvent.TypeTarget.UI_To_Data)
                ApplyTargetCosmicObjectName(e.name);
        }, Binder);
    }

    private void OnReturnDataEvent(ReturnDataEvent e)
    {
        if (e.eventType == CoreEventType.Send)
            if (e.snapshotType == SnapShotType.ChapterSnapshot)
                CacheSessionsFromChapterSnapshot(e.chapterSnapshot);

        if (e.eventType == CoreEventType.Request)
            Response_CurrentSession(currentCosmicObjectSO?.Id);
    }

    /// <summary>
    /// SessionController gọi khi current session đổi.
    /// </summary>
    /// <remarks>ProgressManager sẽ MarkSessionAccessedById(e.returnId)</remarks>
    /// <returns>void</returns>
    public void Response_CurrentSession(string Id)
    {
        if (string.IsNullOrEmpty(Id)) return;

        //Debug.LogWarning($"SessionController: {CosmicObjectSO.Id}");

        CoreEvents.ReturnData.Raise(new ReturnDataEvent(
            CoreEventType.Response,
            Id
        ));
    }
}
