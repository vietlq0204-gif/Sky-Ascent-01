using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Hướng logic hiện tại (vừa là state vừa là kết quả swipe)
/// </summary>
public enum SwipeDirection
{
    None = 0,
    East,
    South,
    Center,
    West,
    North
}

public enum SwipeType
{
    None,
    InSession,
    InMenu
}


public class Swipe 
{
    [SerializeField] private DragDirection direction = DragDirection.Center;

    private Vector2 startPos, endPos;
    private const float swipeThreshold = 50f; // độ dài tối thiểu để tính là vuốt

    void Update()
    {
//#if UNITY_EDITOR || UNITY_STANDALONE
        MouseSwipe();
//#else
        //HandleTouchSwipe();
//#endif

    }


    private async void DefaultCoolDown()
    {
        await Task.Delay(2000);
    }


    /// <summary>
    /// Input chuột cho Editor và Standalone
    /// </summary>
    private void MouseSwipe()
    {
        if (Input.GetMouseButtonDown(0)) // Chuột trái nhấn xuống
        {
            startPos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0) ) // Chuột trái thả ra
        {
            endPos = Input.mousePosition;
            if (Vector2.Distance(startPos, endPos) >= swipeThreshold)
                HandleSwipe();
        }
    }

    /// <summary>
    /// Xử lý swipe hoàn chỉnh
    /// </summary>
    private void HandleSwipe()
    {
        DragDirection directionIntent = DetectSwipeIntent();
        ApplySwipeIntent(directionIntent);

        //Debug.Log($"[Swipe] Direction: {direction}");
        DrawSwipeLine();
    }

    /// <summary>
    /// Phát hiện intent swipe (ý định)
    /// </summary>
    /// <returns>Swipe intent theo trục</returns>
    private DragDirection DetectSwipeIntent()
    {
        Vector2 delta = endPos - startPos;

        // Nếu muốn TAP để về Center thì bật dòng dưới
        // if (delta.magnitude < tapThreshold) return SwipeDirection.Center;

        if (delta.magnitude < swipeThreshold)
            return DragDirection.None;

        bool isHorizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);

        if (isHorizontal)
            return delta.x > 0 ? DragDirection.East : DragDirection.West;

        return delta.y > 0 ? DragDirection.North : DragDirection.South;
    }

    /// <summary>
    /// Áp dụng intent để cập nhật trạng thái direction (có hỗ trợ vuốt về Center)
    /// </summary>
    /// <param name="intent">Hướng intent từ swipe</param>
    private void ApplySwipeIntent(DragDirection intent)
    {
        if (intent == DragDirection.None) return;

        // Nếu có Tap/Center thì cho về tâm luôn
        if (intent == DragDirection.Center)
        {
            direction = DragDirection.Center;
            return;
        }

        // Đang ở tâm: vuốt đi đâu thì sang đó
        if (direction == DragDirection.Center)
        {
            direction = intent;
            return;
        }

        // Đang ở rìa: vuốt "hướng về tâm" => Center, còn lại => đổi hướng (tuỳ design)
        bool towardCenter =
            (direction == DragDirection.North && intent == DragDirection.South) ||
            (direction == DragDirection.South && intent == DragDirection.North) ||
            (direction == DragDirection.East && intent == DragDirection.West) ||
            (direction == DragDirection.West && intent == DragDirection.East);

        direction = towardCenter ? DragDirection.Center : intent;
    }

//# if UNITY_EDITOR

    /// <summary>
    /// vẽ đường vuốt để debug
    /// </summary>
    private void DrawSwipeLine()
    {
        // DEBUG hiển thị đường vuốt 
        Vector3 startPosWorld = Camera.main.ScreenToWorldPoint(new Vector3(startPos.x, startPos.y, 10f));
        Vector3 endPosWorld = Camera.main.ScreenToWorldPoint(new Vector3(endPos.x, endPos.y, 10f));
        Debug.DrawLine(startPosWorld, endPosWorld, Color.cyan, 1.5f);

    }

//#endif
}
