using UnityEngine;

[CreateAssetMenu(menuName = "DL/Variables/Int Variable")]
public class IntVariable : DLVariable
{
    public int Value;
    public int amount = 1;

    public override object ApplyAndGet()
    {
        Value += amount;
        return Value;
    }
}