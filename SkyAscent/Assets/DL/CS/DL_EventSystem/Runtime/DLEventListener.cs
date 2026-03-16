using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

public enum VariableEventType
{
    Int,
    Float,
    Bool,
    String,
    Vector3,
    GameObject,
    Transform,
    Color,
    CustomObject,   // ScriptableObject hoặc POCO
    IntList         // ví dụ collection cơ bản
}

[Serializable]
public struct DLPrimitiveResponses
{
    public UnityEvent<int> intResponse;
    public UnityEvent<float> floatResponse;
    public UnityEvent<bool> boolResponse;
    public UnityEvent<string> stringResponse;
    public UnityEvent<Vector3> vector3Response;
}

[Serializable]
public struct DLOtherResponses
{
    public UnityEvent<GameObject> gameObjectResponse;
    public UnityEvent<Transform> transformResponse;
    public UnityEvent<Color> colorResponse;
    public UnityEvent<UnityEngine.Object> customResponse;           // dùng cho ScriptableObject hoặc bất kỳ UnityEngine.Object nào
    public UnityEvent<List<int>> intListResponse;
}

/// <summary>
/// Router trung gian giữa ScriptableObject Event và UnityEvent trong Scene
/// </summary>
/// <remarks>
/// Dùng Proxy class để gom nhiều kiểu event (Int, Float, Bool...) trong một component.
/// lắng nghe DLEventBase (DLEvent<T>) và gọi UnityEvent tương ứng khi event được raise.
/// chuyển đổi dữ liệu từ DLEvent<T> sang UnityEvent<T>.
/// </remarks>
public class DLEventListener : MonoBehaviour
{
    [SerializeField] private DLEventBase dLEvent;
    [SerializeField] private VariableEventType variableEventType;

    [Header("Primitive Responses")]
    [SerializeField] private DLPrimitiveResponses primitiveResponse;

    [Header("Other Responses")]
    [SerializeField] private DLOtherResponses otherResponse;

    private DLEventListenerBase<int> proxyInt;
    private DLEventListenerBase<float> proxyFloat;
    private DLEventListenerBase<bool> proxyBool;
    private DLEventListenerBase<string> proxyString;
    private DLEventListenerBase<Vector3> proxyVector3;
    private DLEventListenerBase<GameObject> proxyGameObject;
    private DLEventListenerBase<Transform> proxyTransform;
    private DLEventListenerBase<Color> proxyColor;
    private DLEventListenerBase<UnityEngine.Object> proxyCustom;
    private DLEventListenerBase<List<int>> proxyIntList;

    private void OnEnable() => Register();
    private void OnDisable() => Unregister();

    private void Register()
    {
        switch (variableEventType)
        {
            case VariableEventType.Int:
                proxyInt = new ProxyInt(this);
                (dLEvent as DLEvent<int>)?.Register(proxyInt);
                break;
            case VariableEventType.Float:
                proxyFloat = new ProxyFloat(this);
                (dLEvent as DLEvent<float>)?.Register(proxyFloat);
                break;
            case VariableEventType.Bool:
                proxyBool = new ProxyBool(this);
                (dLEvent as DLEvent<bool>)?.Register(proxyBool);
                break;
            case VariableEventType.String:
                proxyString = new ProxyString(this);
                (dLEvent as DLEvent<string>)?.Register(proxyString);
                break;
            case VariableEventType.Vector3:
                proxyVector3 = new ProxyVector3(this);
                (dLEvent as DLEvent<Vector3>)?.Register(proxyVector3);
                break;
            case VariableEventType.GameObject:
                proxyGameObject = new ProxyGameObject(this);
                (dLEvent as DLEvent<GameObject>)?.Register(proxyGameObject);
                break;
            case VariableEventType.Transform:
                proxyTransform = new ProxyTransform(this);
                (dLEvent as DLEvent<Transform>)?.Register(proxyTransform);
                break;
            case VariableEventType.Color:
                proxyColor = new ProxyColor(this);
                (dLEvent as DLEvent<Color>)?.Register(proxyColor);
                break;
            case VariableEventType.CustomObject:
                proxyCustom = new ProxyCustom(this);
                (dLEvent as DLEvent<UnityEngine.Object>)?.Register(proxyCustom);
                break;
            case VariableEventType.IntList:
                proxyIntList = new ProxyIntList(this);
                (dLEvent as DLEvent<List<int>>)?.Register(proxyIntList);
                break;
        }
    }

    private void Unregister()
    {
        switch (variableEventType)
        {
            case VariableEventType.Int: (dLEvent as DLEvent<int>)?.Unregister(proxyInt); break;
            case VariableEventType.Float: (dLEvent as DLEvent<float>)?.Unregister(proxyFloat); break;
            case VariableEventType.Bool: (dLEvent as DLEvent<bool>)?.Unregister(proxyBool); break;
            case VariableEventType.String: (dLEvent as DLEvent<string>)?.Unregister(proxyString); break;
            case VariableEventType.Vector3: (dLEvent as DLEvent<Vector3>)?.Unregister(proxyVector3); break;
            case VariableEventType.GameObject: (dLEvent as DLEvent<GameObject>)?.Unregister(proxyGameObject); break;
            case VariableEventType.Transform: (dLEvent as DLEvent<Transform>)?.Unregister(proxyTransform); break;
            case VariableEventType.Color: (dLEvent as DLEvent<Color>)?.Unregister(proxyColor); break;
            case VariableEventType.CustomObject: (dLEvent as DLEvent<UnityEngine.Object>)?.Unregister(proxyCustom); break;
            case VariableEventType.IntList: (dLEvent as DLEvent<List<int>>)?.Unregister(proxyIntList); break;
        }
    }

    // Proxy classes
    private class ProxyInt : DLEventListenerBase<int> { private readonly DLEventListener o; public ProxyInt(DLEventListener o) => this.o = o; public override void OnEventRaised(int d) => o.primitiveResponse.intResponse.Invoke(d); }
    private class ProxyFloat : DLEventListenerBase<float> { private readonly DLEventListener o; public ProxyFloat(DLEventListener o) => this.o = o; public override void OnEventRaised(float d) => o.primitiveResponse.floatResponse.Invoke(d); }
    private class ProxyBool : DLEventListenerBase<bool> { private readonly DLEventListener o; public ProxyBool(DLEventListener o) => this.o = o; public override void OnEventRaised(bool d) => o.primitiveResponse.boolResponse.Invoke(d); }
    private class ProxyString : DLEventListenerBase<string> { private readonly DLEventListener o; public ProxyString(DLEventListener o) => this.o = o; public override void OnEventRaised(string d) => o.primitiveResponse.stringResponse.Invoke(d); }
    private class ProxyVector3 : DLEventListenerBase<Vector3> { private readonly DLEventListener o; public ProxyVector3(DLEventListener o) => this.o = o; public override void OnEventRaised(Vector3 d) => o.primitiveResponse.vector3Response.Invoke(d); }

    private class ProxyGameObject : DLEventListenerBase<GameObject> { private readonly DLEventListener o; public ProxyGameObject(DLEventListener o) => this.o = o; public override void OnEventRaised(GameObject d) => o.otherResponse.gameObjectResponse.Invoke(d); }
    private class ProxyTransform : DLEventListenerBase<Transform> { private readonly DLEventListener o; public ProxyTransform(DLEventListener o) => this.o = o; public override void OnEventRaised(Transform d) => o.otherResponse.transformResponse.Invoke(d); }
    private class ProxyColor : DLEventListenerBase<Color> { private readonly DLEventListener o; public ProxyColor(DLEventListener o) => this.o = o; public override void OnEventRaised(Color d) => o.otherResponse.colorResponse.Invoke(d); }
    private class ProxyCustom : DLEventListenerBase<UnityEngine.Object> { private readonly DLEventListener o; public ProxyCustom(DLEventListener o) => this.o = o; public override void OnEventRaised(UnityEngine.Object d) => o.otherResponse.customResponse.Invoke(d); }
    private class ProxyIntList : DLEventListenerBase<List<int>> { private readonly DLEventListener o; public ProxyIntList(DLEventListener o) => this.o = o; public override void OnEventRaised(List<int> d) => o.otherResponse.intListResponse.Invoke(d); }
}


// Note: DLEventListener có thể đăng ký nhiều DLEvent cùng lúc nếu cần thiết, nhưng để đơn giản và dễ quản lý, mỗi DLEventListener chỉ nên đăng ký một DLEventBase. Nếu cần đăng ký nhiều sự kiện, hãy thêm nhiều DLEventListener vào cùng một GameObject hoặc các GameObject con.

// chưa tối ưu: có thể dùng reflection để tự động tạo proxy thay vì viết thủ công
// chưa tối ưu: có thể dùng generic để tránh viết nhiều proxy class
// chưa tối ưu: có thể dùng Dictionary<VariableEventType, UnityEvent> để tránh viết nhiều field