using UnityEngine;

//public enum SwipeType
//{
//    None,
//    InSession,
//    InMenu
//}

public class SwipeInput : CoreEventBase, IInject<Core>
{
    private Core _core;

    private Vector2 startPos, endPos;
    [SerializeField] private bool isSwiping = false;
    [SerializeField] private bool IsCooldown = false;
    private int currentIndex = 0;
    private const float swipeThreshold = 50f; // độ dài tối thiểu để tính là vuốt

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseSwipe();
#else
        HandleTouchSwipe();
#endif

    }

    public void Inject(Core context) {_core = context;}

    public override void SubscribeEvents()
    {
        //CoreEvents.OnSwipe.Subscribe(OnCoolDown, Binder);
        CoreEvents.OnMoveAlongToPath.Subscribe(e => { if (e.IsEnd) OutCoolDown(); }, Binder);
    }

    //private void OnCoolDown(OnSwipeEvent swipe)
    //{
    //    IsCooldown = true;
    //    //Debug.Log("coolDown");
    //}

    private void OutCoolDown()
    {
        IsCooldown = false;
        //Debug.Log("Het coolDown roi ");
    }

    /// <summary>
    /// Input chuột cho Editor và Standalone
    /// </summary>
    private void HandleMouseSwipe()
    {
        if (Input.GetMouseButtonDown(0)) // Chuột trái nhấn xuống
        {
            startPos = Input.mousePosition;
            isSwiping = true;
        }

        if (Input.GetMouseButtonUp(0) && isSwiping) // Chuột trái thả ra
        {
            endPos = Input.mousePosition;
            if (Vector2.Distance(startPos, endPos) >= swipeThreshold)
                ChosseModeSwipe();
            isSwiping = false;
        }
    }

    /// <summary>
    /// Input cảm ứng cho Mobile
    /// </summary>
    private void HandleTouchSwipe()
    {
        if (Input.touchCount == 0) return;
        Touch touch = Input.GetTouch(0); // Lấy touch đầu tiên

        if (touch.phase == TouchPhase.Began) // bắt đầu vuốt
            startPos = touch.position;
        else if (touch.phase == TouchPhase.Ended) // kết thúc vuốt
        {
            endPos = touch.position;
            if (Vector2.Distance(startPos, endPos) >= swipeThreshold)
                ChosseModeSwipe();
        }
    }

    /// <summary>
    /// Chọn chế độ vuốt dựa trên CoreState
    private void ChosseModeSwipe()
    {
        if (IsCooldown) return;

        if (_core.StateMachine.CurrentStateType == typeof(OnMenuState))
        {
            DetectSwipeShowShip();
        }
        else if (_core.StateMachine.CurrentStateType == typeof(OnSessionState))
        {
            //DetectSwipeControlShip();
        }
    }

    /// <summary>
    /// Lấy hướng vuốt và chọn PointTarget tương ứng cho camera khi ở menu
    /// </summary>
    private int DetectSwipeShowShip()
    {
        // Quy ước:

        // Index 0 = đông
        // Index 1 = nam
        // Index 2 = trung tâm
        // Index 3 = tây
        // Index 4 = bắc 

        string mess = "";

        try
        {
            Vector2 delta = endPos - startPos; // vector từ start đến end

            // Hướng vuốt
            bool isHorizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            bool isVertical = !isHorizontal;

            // Vuốt dọc
            if (isVertical)
            {
                // Vuốt từ trên xuống
                if (delta.y < -swipeThreshold)
                {
                    if (currentIndex == 2)
                    {
                        currentIndex = 4;
                        mess = "Hướng bắc";
                    }
                    else
                    {
                        currentIndex = 2;
                        mess = "Trung tâm";
                    }
                }
                // Vuốt từ dưới lên
                else if (delta.y > swipeThreshold)
                {
                    if (currentIndex == 0)
                    {
                        currentIndex = 2;
                        mess = "Trung tâm";
                    }
                    else if (currentIndex == 1)
                    {
                        currentIndex = 2;
                        mess = "Trung tâm";
                    }
                    else if (currentIndex == 2)
                    {
                        currentIndex = 1;
                        mess = "Hướng nam";
                    }
                    else if (currentIndex == 3)
                    {
                        currentIndex = 2;
                        mess = "Trung tâm";
                    }
                    else if (currentIndex == 4)
                    {
                        currentIndex = 2;
                        mess = "Trung tâm";
                    }
                }
            }
            //  Vuốt ngang
            else if (isHorizontal && Mathf.Abs(delta.x) > swipeThreshold)
            {
                if (delta.x > 0)
                {
                    // trái sang phải (theo chiều kim đồng hồ)
                    switch (currentIndex)
                    {
                        case 0: currentIndex = 1; mess = "Hướng nam"; break;
                        case 1: currentIndex = 3; mess = "Hướng tây"; break;
                        case 3: currentIndex = 4; mess = "Hướng bắc"; break;
                        case 4: currentIndex = 0; mess = "Hướng đông"; break;
                        case 2: currentIndex = 3; mess = "Hướng tây"; break;
                    }
                }
                else
                {
                    // phải sang trái (ngược chiều kim đồng hồ)
                    switch (currentIndex)
                    {
                        case 0: currentIndex = 4; mess = "Hướng bắc"; break;
                        case 4: currentIndex = 3; mess = "Hướng tây"; break;
                        case 3: currentIndex = 1; mess = "Hướng nam"; break;
                        case 1: currentIndex = 0; mess = "Hướng đông"; break;
                        case 2: currentIndex = 0; mess = "Hướng đông"; break;
                    }
                }
            }

            Debug.Log($"[Swipe] Selected: {mess}");

            // kích hoạt sự kiện Swipe với type và index đã chọn
            //CoreEvents.OnSwipe.Raise(new OnSwipeEvent(SwipeType.InMenu, currentIndex));

            DrawSwipeLine();
            return currentIndex;
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.ToString());
            return -1;
        }

    }

    /// <summary>
    /// vẽ đường vuốt để debug
    /// </summary>
    private void DrawSwipeLine()
    {
        // DEBUG hiển thị đường vuốt 
        Vector3 startPosWorld = Camera.main.ScreenToWorldPoint(new Vector3(startPos.x, startPos.y, 10f));
        Vector3 endPosWorld = Camera.main.ScreenToWorldPoint(new Vector3(endPos.x, endPos.y, 10f));
        Debug.DrawLine(startPosWorld, endPosWorld, Color.cyan, 1.5f);

        //// DEBUG hiển thị vị trí target
        //Vector3 targetPos = target.transform.position;
        //Debug.DrawRay(targetPos, Vector3.up * 3, Color.yellow, 1.5f);
    }
}





