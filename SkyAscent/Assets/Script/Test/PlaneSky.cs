using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class PlaneSky : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform planeCenter;

    [Header("Cloud Settings")]
    [SerializeField] private List<GameObject> cloudPrefabs;
    [SerializeField] private float radius = 10f;
    [SerializeField] private int cloudCount = 30;

    [Header("Rotation")]
    [SerializeField] private bool Rotation = false;
    [SerializeField] private Vector3 rotationDirection;
    [SerializeField] private float rotationSpeed;

    private void Update()
    {
       if (Rotation) RotatePlane(rotationDirection, rotationSpeed);
    }

    [ContextMenu("Generate plane Clouds")]
    private void GenerateClouds()
    {
        planeCenter = gameObject.transform;

        if (planeCenter == null)
        {
            Debug.LogError("PlaneCenter chưa được gán!");
            return;
        }

        if (cloudPrefabs == null || cloudPrefabs.Count == 0)
        {
            Debug.LogError("Danh sách Cloud Prefabs rỗng!");
            return;
        }

        // Xóa mây cũ
        ClearOldClouds();

        for (int i = 0; i < cloudCount; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            Vector3 pos = planeCenter.position + dir * radius;
            GameObject prefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Count)];

            GameObject cloud = Instantiate(prefab, pos, Quaternion.identity, transform);

            // Lấy hướng từ cloud đến tâm
            Vector3 dirToCenter = (planeCenter.position - pos).normalized;
            // Xoay sao cho Y+ của cloud hướng vào tâm
            cloud.transform.rotation = Quaternion.FromToRotation(Vector3.down, dirToCenter);
        }
    }

    private void ClearOldClouds()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            foreach (Transform child in transform)
                DestroyImmediate(child.gameObject);
        }
        else
#endif
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
        }
    }

    private void RotatePlane(Vector3 axis, float speed)
    {
        // Xoay quanh tâm plane
        transform.Rotate(axis, speed, Space.Self);
        //Debug.Log($"[PlaneSky] Đã xoay plane quanh {axis} {speed}°");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (planeCenter == null) return;

        // Vẽ tâm
        //Gizmos.color = Color.yellow;
        //Gizmos.DrawSphere(planeCenter.position, 0.2f);

        // Vẽ hình cầu
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(planeCenter.position, radius);

        // Vẽ vector hướng Y ngược (tham chiếu)
        //Gizmos.color = Color.red;
        //Gizmos.DrawLine(planeCenter.position, planeCenter.position - Vector3.up * radius);
    }
#endif

}
