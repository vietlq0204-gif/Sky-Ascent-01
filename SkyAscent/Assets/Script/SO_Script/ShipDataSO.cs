using UnityEngine;

[CreateAssetMenu(menuName = "Ship/ Ship Data SO")]
public class ShipDataSO : BaseSO
{
    [Tooltip("")]
    public MainCompartmentDataSO MainCompartmentDataSO;

    [Tooltip("")]
    public EngineDataSO EngineDataSO;

    [Tooltip("")]
    public FuelTankDataSO FuelTankDataSO;

    [Tooltip("")]
    public float TotalWeight; // tấn 
}