using UnityEngine;

[CreateAssetMenu(menuName = "Ship/ Main Compartment Data SO")]
public class MainCompartmentDataSO : ScriptableObject
{
    public FuelTankDataSO FuelTankDataSO;
    public float Weigh; // tấn 
}

//[CreateAssetMenu(menuName = "Ship/ Sub Compartment Data SO")]
//public class SubCompartmentDataSO : ScriptableObject
//{
//    FuelTankDataSO FuelTankDataSO;
//    public float Weigh; // tấn 

//}


