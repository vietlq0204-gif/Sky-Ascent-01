using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CosmicObjectWheelRoadMover_Adjacent : MonoBehaviour
{
    public enum Dir { Right, Left }

    [Header("Refs")]
    [SerializeField] private Transform queueRoot;
    [SerializeField] private RoadBaker roadBaker;

    [Header("Input")]
    [SerializeField] private KeyCode keyReset = KeyCode.S;
    [SerializeField] private KeyCode keyRight = KeyCode.D;
    [SerializeField] private KeyCode keyLeft = KeyCode.A;

    [Header("Move")]
    [Min(0.01f)][SerializeField] private float moveSpeed = 3f; // units/second
    [SerializeField] private List<Transform> _points = new();

    private int _middleIndex = -1;

    // MODEL: mỗi point chỉ 1 object
    private Transform[] _slots;                        // slots[i] = object tại point i
    private readonly Queue<Transform> _queue = new();  // cache queue để dự đoán/giảm scan

    private bool _busy;

    private void Awake()
    {
        CacheRoad();
        RebuildStateFromScene();
    }

    private void OnValidate()
    {
        CacheRoad();
    }

    private void Update()
    {
        if (_busy) return;
        if (queueRoot == null || roadBaker == null) return;
        if (_points.Count == 0) CacheRoad();
        if (_points.Count == 0) return;

        if (Input.GetKeyDown(keyReset))
            StartCoroutine(ResetAndFillLeftFromMiddle());
        else if (Input.GetKeyDown(keyRight))
            StartCoroutine(Execute(Dir.Right));
        else if (Input.GetKeyDown(keyLeft))
            StartCoroutine(Execute(Dir.Left));
    }

    /// <summary>
    /// Cache road points from RoadBaker
    /// </summary>
    private void CacheRoad()
    {
        _points.Clear();
        if (roadBaker == null || roadBaker.bakedPoints == null) return;

        foreach (var go in roadBaker.bakedPoints)
            if (go != null) _points.Add(go.transform);

        _middleIndex = ResolveMiddleIndex();

        if (_points.Count > 0)
            _slots = new Transform[_points.Count];
    }

    /// <summary>
    /// lấy index của middle point (bakedPointImportant[2])
    /// </summary>
    /// <returns></returns>
    private int ResolveMiddleIndex()
    {
        if (roadBaker == null || roadBaker.bakedPointImportant == null) return -1;
        if (roadBaker.bakedPointImportant.Count <= 2) return -1;

        var middleGo = roadBaker.bakedPointImportant[2];
        if (middleGo == null) return -1;

        return _points.FindIndex(p => p != null && p.gameObject == middleGo);
    }

    /// <summary>
    /// Rebuild state từ scene hiện tại: slots + queue cache
    /// </summary>
    private void RebuildStateFromScene()
    {
        if (_points.Count == 0 || _slots == null) return;

        // 1) rebuild queue cache theo hierarchy order (index từ trên xuống)
        _queue.Clear();
        if (queueRoot != null)
        {
            for (int i = 0; i < queueRoot.childCount; i++)
                _queue.Enqueue(queueRoot.GetChild(i));
        }

        // 2) rebuild slots từ scene, và nếu point có >1 child => đẩy dư về queue
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = null;

            var p = _points[i];
            if (p == null) continue;

            if (p.childCount <= 0) continue;

            // lấy 1 cái đầu làm occupant
            var first = p.GetChild(0);
            _slots[i] = first;

            // đẩy phần dư về queue để đảm bảo 1 object/point
            while (p.childCount > 1)
            {
                var extra = p.GetChild(1);
                EnqueueToQueue(extra); // set parent về queueRoot + cache
            }

            // snap lại occupant để chuẩn local transform
            SnapToPoint(first, p);
        }
    }

    /// <summary>
    /// Move command: chỉ 1 bước liền kề hoặc spawn/eject
    /// </summary>
    private struct MoveCmd
    {
        public Transform obj;
        public int from;  // owner slot index (-1 = from queue)
        public int to;    // target slot index
    }

    /// <summary>
    /// Kế hoạch di chuyển trong 1 lần execute
    /// </summary>
    private class Plan
    {
        public readonly List<MoveCmd> moves = new();            // move commands
        public readonly List<Transform> ejectToQueue = new();   // objects to eject to queue
        public Transform spawnFromQueue = null;                 // object to spawn from queue (if any)
    }

    /// <summary>
    /// Dự đoán target slots cho các object trên road nếu di chuyển theo dir
    /// </summary>
    /// <param name="dir"></param>
    /// <returns>
    /// trả về dictionary mapping: object -> targetSlotIndex
    /// </returns>
    public Dictionary<Transform, int> PredictNextTargets(Dir dir)
    {
        var plan = ComputePlan(dir, simulateOnly: true, out var simNextSlots);

        var map = new Dictionary<Transform, int>();
        for (int i = 0; i < simNextSlots.Length; i++)
        {
            var o = simNextSlots[i];
            if (o != null) map[o] = i;
        }
        return map;
    }

    /// <summary>
    /// Compute plan di chuyển theo dir
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="simulateOnly"></param>
    /// <param name="nextSlotsSim"></param>
    /// <returns></returns>
    private Plan ComputePlan(Dir dir, bool simulateOnly, out Transform[] nextSlotsSim)
    {
        int n = _slots.Length;

        // clone slots
        var slots = (Transform[])_slots.Clone();

        // simulate queue count only (không cần clone content để predict)
        int queueCount = _queue.Count;

        var plan = new Plan();

        bool roadFull = slots.All(o => o != null);
        bool hasQueue = queueCount > 0;

        int last = n - 1;

        // right
        if (dir == Dir.Right)
        {
            // Rule: road full + queue còn => eject last về queue (không làm object road đi quá 1 bước)
            if (roadFull && hasQueue)
            {
                plan.ejectToQueue.Add(slots[last]);
                slots[last] = null;
            }

            // shift right 1 bước: i-1 -> i
            for (int i = last; i >= 1; i--)
            {
                if (slots[i] != null) continue;
                if (slots[i - 1] == null) continue;

                var obj = slots[i - 1];

                // đảm bảo liền kề: from i-1 to i (liền kề)
                plan.moves.Add(new MoveCmd { obj = obj, from = i - 1, to = i });

                slots[i] = obj;
                slots[i - 1] = null;
            }

            // spawn vào slot[0] nếu rỗng
            if (slots[0] == null && hasQueue)
            {
                // lấy object đầu trong queue để spawn (queue[0])
                plan.spawnFromQueue = queueRoot != null && queueRoot.childCount > 0 ? queueRoot.GetChild(0) : null;
                if (plan.spawnFromQueue != null)
                {
                    plan.moves.Add(new MoveCmd { obj = plan.spawnFromQueue, from = -1, to = 0 });
                    slots[0] = plan.spawnFromQueue;
                    queueCount = Mathf.Max(0, queueCount - 1);
                }
            }
        }
        // Left
        else
        {
            if (roadFull && hasQueue)
            {
                plan.ejectToQueue.Add(slots[0]);
                slots[0] = null;
            }

            // shift left 1 bước: i+1 -> i
            for (int i = 0; i <= last - 1; i++)
            {
                if (slots[i] != null) continue;
                if (slots[i + 1] == null) continue;

                var obj = slots[i + 1];
                plan.moves.Add(new MoveCmd { obj = obj, from = i + 1, to = i });

                slots[i] = obj;
                slots[i + 1] = null;
            }

            //spawn vào slot[last] nếu rỗng
            if (slots[last] == null && hasQueue)
            {
                plan.spawnFromQueue = queueRoot != null && queueRoot.childCount > 0 ? queueRoot.GetChild(0) : null;
                if (plan.spawnFromQueue != null)
                {
                    plan.moves.Add(new MoveCmd { obj = plan.spawnFromQueue, from = -1, to = last });
                    slots[last] = plan.spawnFromQueue;
                    queueCount = Mathf.Max(0, queueCount - 1);
                }
            }
        }

        nextSlotsSim = slots;
        return plan;
    }

    /// <summary>
    /// thực thi di chuyển theo dir
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    private IEnumerator Execute(Dir dir)
    {
        _busy = true;

        // đảm bảo state sạch: 1 object/point
        RebuildStateFromScene();

        // 1) compute plan (không vượt adjacency)
        var plan = ComputePlan(dir, simulateOnly: false, out _);

        // 2) apply eject trước (eject không làm object road đi nhiều bước)
        foreach (var obj in plan.ejectToQueue)
        {
            if (obj == null) continue;

            int fromIdx = FindSlotIndex(obj);
            if (fromIdx >= 0) _slots[fromIdx] = null;

            EnqueueToQueue(obj); //teleport + reset
        }

        // 3) chạy moves song song (mỗi move chỉ 1 bước kề hoặc spawn)
        int done = 0;
        for (int i = 0; i < plan.moves.Count; i++)
        {
            var cmd = plan.moves[i];

            if (cmd.from < 0)
            {
                // // spawn từ queue -> teleport ngay (reset transform bên trong)
                TeleportFromQueueToPoint(cmd.obj, cmd.to);
                done++; // // hoàn thành ngay, không coroutine
            }
            else
            {
                // // road -> road: bay 1 bước liền kề
                StartCoroutine(RunMove(cmd, () => done++));
            }
        }


        while (done < plan.moves.Count)
            yield return null;

        // 4) rebuild slots lại từ scene để chắc chắn đúng (cheap vì n nhỏ)
        RebuildStateFromScene();

        _busy = false;
    }

    /// <summary>
    /// helper: chạy 1 move command.
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="onDone"></param>
    /// <returns></returns>
    private IEnumerator RunMove(MoveCmd cmd, System.Action onDone)
    {
        if (cmd.obj == null || cmd.to < 0 || cmd.to >= _points.Count)
        {
            onDone?.Invoke();
            yield break;
        }

        // đảm bảo đích hiện đang trống (1 object/point)
        if (_points[cmd.to].childCount > 0)
        {
            // nếu bị sai state, đẩy dư về queue để không phá rule
            var extra = _points[cmd.to].GetChild(0);
            EnqueueToQueue(extra);
        }

        // detach để di chuyển world-space
        cmd.obj.SetParent(null, true);

        Vector3 a;
        Vector3 b = _points[cmd.to].position;

        if (cmd.from < 0)
        {
            // spawn từ queue root -> point
            a = queueRoot.position;
        }
        else
        {
            // chỉ từ point kề
            a = _points[cmd.from].position;
        }

        yield return MoveSegment(cmd.obj, a, b);

        // snap + reset transform khi tới nơi
        SnapToPoint(cmd.obj, _points[cmd.to]);

        // update model slots nhanh (không scan)
        if (cmd.from >= 0) _slots[cmd.from] = null;
        _slots[cmd.to] = cmd.obj;

        // nếu là spawn từ queue => dequeue cache + hierarchy
        if (cmd.from < 0) DequeueCacheAndHierarchy(cmd.obj);

        onDone?.Invoke();
    }

    /// <summary>
    /// Tìm index của obj trong slots
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    private int FindSlotIndex(Transform obj)
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] == obj) return i;
        return -1;
    }

    /// <summary>
    /// Reset road và fill từ middle sang trái
    /// </summary>
    /// <returns></returns>
    private IEnumerator ResetAndFillLeftFromMiddle()
    {
        _busy = true;

        RebuildStateFromScene();

        if (_middleIndex < 0 || _points.Count == 0)
        {
            _busy = false;
            yield break;
        }

        // clear road -> queue
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;
            var obj = _slots[i];
            _slots[i] = null;

            EnqueueToQueue(obj); //teleport + reset 
        }

        // fill middle point
        if (TryPeekQueue(out var first))
        {
            TeleportFromQueueToPoint(first, _middleIndex); // teleport + reset + update slots + dequeue
        }
        else
        {
            _busy = false;
            yield break;
        }

        // fill left side from middle-1 down to 0
        for (int i = _middleIndex - 1; i >= 0; i--)
        {
            if (!TryPeekQueue(out var obj)) break;

            TeleportFromQueueToPoint(obj, i); // teleport + reset + update slots + dequeue
        }

        _busy = false;
    }

    // =========================
    // Move / Snap
    // =========================

    /// <summary>
    /// Teleport object từ queue đến point[toIndex],
    /// reset local transform,
    /// cập nhật slots và dequeue.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="toIndex"></param>
    private void TeleportFromQueueToPoint(Transform obj, int toIndex)
    {
        if (obj == null) return;
        if (toIndex < 0 || toIndex >= _points.Count) return;

        var point = _points[toIndex];
        if (point == null) return;

        // đảm bảo Point chỉ chứa 1 object
        if (point.childCount > 0)
        {
            // object dư bị đẩy về queue để giữ invariant
            var extra = point.GetChild(0);
            EnqueueToQueue(extra);
        }

        // teleport + reset local transform
        obj.SetParent(point, worldPositionStays: false);
        obj.localPosition = Vector3.zero;
        obj.localRotation = Quaternion.identity;
        obj.localScale = Vector3.one;

        // cập nhật model slot
        _slots[toIndex] = obj;

        // dequeue cache + hierarchy (queue[0])
        DequeueCacheAndHierarchy(obj);
    }

    /// <summary>
    /// helper di chuyển obj từ a đến b
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private IEnumerator MoveSegment(Transform obj, Vector3 a, Vector3 b)
    {
        float dist = Vector3.Distance(a, b);
        if (dist <= 0.0001f)
        {
            obj.position = b;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += (Time.deltaTime * moveSpeed) / dist; // // tốc độ đều
            obj.position = Vector3.LerpUnclamped(a, b, Mathf.Clamp01(t));
            yield return null;
        }
        obj.position = b;
    }

    /// <summary>
    /// Helper Snap object đến point, reset local transform
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="point"></param>
    private void SnapToPoint(Transform obj, Transform point)
    {
        obj.SetParent(point, false);
        ResetTranform(obj);
    }

    /// <summary>
    /// helper reset local transform
    /// </summary>
    /// <param name="obj"></param>
    private void ResetTranform(Transform obj)
    {
        obj.localPosition = Vector3.zero;
        obj.localRotation = Quaternion.identity;
        obj.localScale = Vector3.one;
    }

    // =========================
    // Queue ops (cache + hierarchy)
    // =========================

    /// <summary>
    /// thử peek object đầu trong queue
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>
    /// trả về true nếu có object, false nếu queue rỗng hoặc lỗi
    /// </returns>
    private bool TryPeekQueue(out Transform obj)
    {
        obj = null;
        if (queueRoot == null || queueRoot.childCount == 0) return false;
        obj = queueRoot.GetChild(0);
        return obj != null;
    }

    /// <summary>
    /// dequeue object khỏi cache + hierarchy. 
    /// dùng để đồng bộ khi spawn từ queue.
    /// </summary>
    /// <param name="expected"></param>
    private void DequeueCacheAndHierarchy(Transform expected)
    {
        // đảm bảo object đầu trong queue hierarchy là expected
        if (queueRoot != null && queueRoot.childCount > 0 && queueRoot.GetChild(0) == expected)
        {
            if (_queue.Count > 0 && _queue.Peek() == expected) // nếu cache đúng
                _queue.Dequeue();
        }
        else
        {
            // fallback: rebuild cache nếu lệch
            RebuildQueueCacheOnly();
        }
    }

    /// <summary>
    /// helper đưa object vào queue
    /// </summary>
    /// <param name="obj"></param>
    /// <remarks>
    ///  set parent + reset transform + cache
    ///  </remarks>
    private void EnqueueToQueue(Transform obj)
    {
        if (obj == null || queueRoot == null) return;

        // teleport về queue (local space)
        obj.SetParent(queueRoot, worldPositionStays: false);
        obj.SetAsLastSibling(); // vào cuối queue
        ResetTranform(obj);

        _queue.Enqueue(obj); // cache  
    }

    /// <summary>
    /// Helper rebuild queue cache từ scene hierarchy.
    /// </summary>
    /// <remarks>
    /// dùng để đồng bộ lại khi lệch cache.
    /// </remarks>
    private void RebuildQueueCacheOnly()
    {
        _queue.Clear();
        if (queueRoot == null) return;
        for (int i = 0; i < queueRoot.childCount; i++)
            _queue.Enqueue(queueRoot.GetChild(i));
    }
}
