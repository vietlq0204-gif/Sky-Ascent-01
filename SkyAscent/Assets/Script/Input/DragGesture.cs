//using UnityEngine;

///// <summary>
///// Chuyển raw drag thành gesture: delta, velocity, swipe length
///// </summary>
//public class DragGesture
//{
//    public float Velocity { get; private set; }
//    public float TotalDrag { get; private set; }

//    private float _lastDelta;
//    private float _startPos;

//    public void Begin(float pos)
//    {
//        _startPos = pos;
//        TotalDrag = 0f;
//        Velocity = 0f;
//    }

//    public float Update(float delta, float dt)
//    {
//        TotalDrag += delta;
//        Velocity = delta / Mathf.Max(0.0001f, dt);
//        return delta;
//    }

//    public int End(float threshold)
//    {
//        if (Mathf.Abs(TotalDrag) < threshold) return 0;
//        return TotalDrag > 0 ? 1 : -1;
//    }
//}
