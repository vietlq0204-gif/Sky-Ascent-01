
using UnityEngine;

[CreateAssetMenu(menuName = "DL/Variables/Float Variable")]
public class FloatVariable : DLVariable
{
    public float Value;
    public float amount = 1f;

    public override object ApplyAndGet()
    {
        Value += amount;
        return Value;
    }
}