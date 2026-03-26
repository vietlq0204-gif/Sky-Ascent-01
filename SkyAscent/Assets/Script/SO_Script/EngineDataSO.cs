using UnityEngine;

[CreateAssetMenu(menuName = "Ship/ Engine Data SO")]
public class EngineDataSO : ScriptableObject
{
    public float Weight;
    public float Isp; // hiệu xuất (s)
    public float ForceMax; // Lực đẩy tối đa (N)
}