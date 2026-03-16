using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]
[RequireComponent(typeof(UIDocument))]
public class SafeZone_BaseMobie : MonoBehaviour
{
    [Header("Play Mode Safe Area")]
    [Tooltip("Bật: dùng Screen.safeArea. Tắt: dùng PlayTop/Bottom/Left/Right")]
    public bool autoSafeZone = true;

    // // offset thủ công cho PlayMode (khi autoSafeZone = false)
    public float playLeft = 0f;
    public float playRight = 0f;
    public float playTop = 0f;
    public float playBottom = 0f;

    [Header("Editor Preview Safe Area")]
    [Tooltip("Bật: dùng offset Editor để xem trước trong GameView")]
    public bool useEditorOverride = true;

    public float editorLeft = 0f;
    public float editorRight = 0f;
    public float editorTop = 44f; // // ví dụ notch trên
    public float editorBottom = 34f; // // ví dụ home bar

    [Header("Debug Outline")]
    public bool showDebugOutline = true;
    public Color debugColor = Color.green;
    public float debugBorderWidth = 2f;

    UIDocument uiDocument;
    VisualElement root;          // // root UI
    VisualElement debugRect;     // // khung viền safe zone

    Rect lastSafeArea;
    ScreenOrientation lastOrientation;

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        if (root != null)
        {
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        ApplySafeArea();
    }

    void OnDisable()
    {
        if (root != null)
        {
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
    }

    void OnValidate()
    {
        // // thay đổi value trong Inspector -> áp lại
        ApplySafeArea();
    }

    void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // // panel size đổi (resize, đổi aspect...) -> áp lại
        ApplySafeArea();
    }

    void Update()
    {
        if (Application.isPlaying && autoSafeZone)
        {
            // // chỉ cần check khi đang dùng Screen.safeArea
            if (Screen.safeArea != lastSafeArea || Screen.orientation != lastOrientation)
            {
                ApplySafeArea();
            }
        }
    }

    void ApplySafeArea()
    {
        if (root == null)
            return;

        int screenW = Screen.width;
        int screenH = Screen.height;
        if (screenW <= 0 || screenH <= 0)
            return;

        Rect safe = GetCurrentSafeArea(screenW, screenH);
        lastSafeArea = safe;
        if (Application.isPlaying)
            lastOrientation = Screen.orientation;

        float left = safe.xMin;
        float right = screenW - safe.xMax;
        float bottom = safe.yMin;
        float top = screenH - safe.yMax;

        // // 1) Áp padding cho root (vùng con = safe zone)
        root.style.paddingLeft = left;
        root.style.paddingRight = right;
        root.style.paddingTop = top;
        root.style.paddingBottom = bottom;

        // // 2) Vẽ khung viền safe zone
        UpdateDebugOutline(left, right, top, bottom);
    }

    Rect GetCurrentSafeArea(float screenW, float screenH)
    {
        if (Application.isPlaying)
        {
            if (autoSafeZone)
            {
                // // PlayMode: dùng safeArea thật từ OS
                return Screen.safeArea;
            }
            else
            {
                // // PlayMode: dùng offset thủ công PlayTop/Bottom/Left/Right
                float x = playLeft;
                float width = screenW - playLeft - playRight;
                float y = playBottom;
                float height = screenH - playTop - playBottom;
                return new Rect(x, y, width, height);
            }
        }
        else
        {
            if (useEditorOverride)
            {
                // // Editor: dùng offset Editor để canh UI
                float x = editorLeft;
                float width = screenW - editorLeft - editorRight;
                float y = editorBottom;
                float height = screenH - editorTop - editorBottom;
                return new Rect(x, y, width, height);
            }
            else
            {
                // // Editor: không dùng safe zone -> full màn hình
                return new Rect(0, 0, screenW, screenH);
            }
        }
    }

    void EnsureDebugRect()
    {
        if (root == null)
            return;

        if (debugRect == null)
        {
            debugRect = new VisualElement();
            debugRect.name = "SafeAreaDebugOutline";
            debugRect.pickingMode = PickingMode.Ignore; // // không chặn click UI
            root.Add(debugRect); // // thêm cuối -> vẽ trên cùng
        }

        debugRect.style.position = Position.Absolute;
        debugRect.style.borderTopWidth = debugBorderWidth;
        debugRect.style.borderBottomWidth = debugBorderWidth;
        debugRect.style.borderLeftWidth = debugBorderWidth;
        debugRect.style.borderRightWidth = debugBorderWidth;

        debugRect.style.borderTopColor = debugColor;
        debugRect.style.borderBottomColor = debugColor;
        debugRect.style.borderLeftColor = debugColor;
        debugRect.style.borderRightColor = debugColor;
        debugRect.style.backgroundColor = new Color(0, 0, 0, 0); // // trong suốt
    }

    void UpdateDebugOutline(float left, float right, float top, float bottom)
    {
        if (!showDebugOutline)
        {
            if (debugRect != null)
                debugRect.style.display = DisplayStyle.None;
            return;
        }

        EnsureDebugRect();

        if (debugRect == null)
            return;

        debugRect.style.display = DisplayStyle.Flex;

        // // Khung viền nằm đúng vị trí safe zone (trong root full-screen)
        debugRect.style.left = left;
        debugRect.style.right = right;
        debugRect.style.top = top;
        debugRect.style.bottom = bottom;
    }
}
