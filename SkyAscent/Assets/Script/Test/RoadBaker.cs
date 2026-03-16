using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class RoadBaker : MonoBehaviour
{
    [Header("Baked Road Data")]
    public List<GameObject> bakedPoints = new();
    public List<GameObject> bakedPointImportant = new();

#if UNITY_EDITOR

    [Header("Gizmos Show----------------------------------------------")]
    [SerializeField] bool showGizmos = true;          // Bật/tắt toàn bộ Gizmos
    [SerializeField] bool showLines = true;           // Bật/tắt đường nối
    [SerializeField] bool showSpheres = true;         // Bật/tắt điểm
    [SerializeField] Color gizmoLineColor = Color.cyan;
    [SerializeField] Color gizmoPointColor = Color.yellow;
    [Range(0.05f, 100f), SerializeField]
    float pointSize = 0.1f; // kích thước điểm

    void OnDrawGizmos()
    {
        if (!showGizmos || bakedPoints == null || bakedPoints.Count < 2)
            return;

        if (bakedPoints.Any(p => p == null)) // LINQ check
            return;

        if (showLines)
        {
            Gizmos.color = gizmoLineColor;
            for (int i = 0; i < bakedPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(bakedPoints[i].transform.position,
                                bakedPoints[i + 1].transform.position);
            }
        }

        if (showSpheres)
        {
            Gizmos.color = gizmoPointColor;
            foreach (var point in bakedPoints)
            {
                Gizmos.DrawSphere(point.transform.position, pointSize);
            }
        }
    }

#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(RoadBaker))]
public class RoadBakerEditor : Editor
{
    RoadBaker baker;

    private void Reset()
    {
        baker = (RoadBaker)target;
        BakePoints(baker);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RoadBaker baker = (RoadBaker)target;

        if (GUILayout.Button("Bake Road Points"))
        {
            BakePoints(baker);
        }
    }

    private void BakePoints(RoadBaker baker)
    {
        // Lấy tất cả child có tag "RoadPoint"
        List<GameObject> roadPoints = baker
            .GetComponentsInChildren<Transform>(true)     // lấy tất cả Transform con
            .Where(t => t.CompareTag("RoadPoint"))
            .Select(t => t.gameObject)
            .ToList();

        int count = roadPoints.Count;
        if (count < 2)
        {
            Debug.LogWarning("Không đủ RoadPoint để bake.");
            return;
        }

        baker.bakedPoints = roadPoints.ToList();

        // Xác định các điểm quan trọng
        var indexPairs = new List<KeyValuePair<int, string>>();
        int firstIndex = 0;
        int lastIndex = count - 1;

        indexPairs.Add(new(firstIndex, "Point Start"));
        indexPairs.Add(new(lastIndex, "Point End"));

        if (count == 3)
        {
            indexPairs.Add(new(1, "Point Middle"));
        }
        else if (count == 4)
        {
            indexPairs.Add(new(1, "Point OnSession"));
            indexPairs.Add(new(2, "Point Prepare End"));
        }
        else if (count >= 5)
        {
            int secondIndex = 1;
            int secondLastIndex = count - 2;
            int middleIndex = count / 2;

            indexPairs.Add(new(secondIndex, "Point OnSession"));
            indexPairs.Add(new(middleIndex, "Point Middle"));
            indexPairs.Add(new(secondLastIndex, "Point Prepare End"));
        }

        // Loại trùng & sắp xếp
        var importantPoints = indexPairs
            .GroupBy(p => p.Key)
            .Select(g => g.First())
            .OrderBy(p => p.Key)
            .ToList();

        List<GameObject> important = new();
        HashSet<int> importantIndexes = new(importantPoints.Select(p => p.Key));

        // Đặt tên cho điểm quan trọng
        foreach (var kv in importantPoints)
        {
            int index = kv.Key;
            string name = kv.Value;

            if (index < 0 || index >= roadPoints.Count)
                continue;

            GameObject point = roadPoints[index];
            AddColliderAndRename(point, name);
            important.Add(point);
        }

        // Đặt tên cho điểm còn lại
        for (int i = 0; i < roadPoints.Count; i++)
        {
            if (importantIndexes.Contains(i))
                continue;

            GameObject point = roadPoints[i];
            AddColliderAndRename(point, $"Point {i + 1}");
        }

        baker.bakedPointImportant = important;
        EditorUtility.SetDirty(baker);
        Debug.Log($"Đã bake {baker.bakedPoints.Count} điểm trong {baker.name}");
    }

    private void AddColliderAndRename(GameObject point, string newName)
    {
        var collider = point.GetComponent<BoxCollider>() ?? point.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = Vector3.one * 1f;
        point.name = newName;
    }

}
#endif
