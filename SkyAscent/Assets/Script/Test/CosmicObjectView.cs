using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Event Adapter:
/// - Chỉ subscribe CoreEvents và forward sang PathDragController.
/// - Không chứa input/thông số drag.
/// </summary>
public sealed class CosmicObjectView : CoreEventBase, IInject<Core>
{
    [SerializeField] private ViewObjectOnPathInput _input;
    Core _core;
    private IPathView _view;
    private PathDragController _controller;

    protected override void Awake()
    {
        base.Awake();

        _view = GetComponent<IPathView>();
        if (_view == null)
        {
            Debug.LogError("[CosmicObjectView] IPathView not found on same GameObject", this);
            return;
        }

        if (_input == null)
        {
            _input = GetComponent<ViewObjectOnPathInput>();
            if (_input == null)
                Debug.LogError("[CosmicObjectView] ViewObjectOnPathInput not found. Please add component.", this);
        }

        _controller = new PathDragController(_view, _input);
    }

    private void Update()
    {
        if (_controller == null) return;
        _controller.Tick(Time.deltaTime);
    }

    public override void SubscribeEvents()
    {
        // Chapter ready -> init
        CoreEvents.LoadChapter.Subscribe(e =>
        {
            if (e.typeData == LoadDataEvent.TypeData.Chapter && e.Completed)
            {
                _controller?.InitData();
            }
        }, Binder);

        // Drag input
        CoreEvents.Drag.Subscribe(e =>
        {
            //_controller?.OnDrag(e);
            OnDragForCosmicObjectView(e);
        }, Binder);

        // Target từ Map
        CoreEvents.MapDataEvent.Subscribe(e =>
        {
            if (e?.CosmicObjectSO == null) return;
            string key = !string.IsNullOrEmpty(e.CosmicObjectSO._name) ? e.CosmicObjectSO._name : e.CosmicObjectSO.name;
            _controller?.TargetGroupOnObjectToMiddle(key);
        }, Binder);

        // Target từ UI / hệ khác
        CoreEvents.TargetObject.Subscribe(e =>
        {
            if (e == null) return;
            if (e.TypeOfTarget != TargetObjectEvent.TypeTarget.Data_To_UI) return;
            if (e.CosmicObjectSO == null) return;

            string key = !string.IsNullOrEmpty(e.CosmicObjectSO._name) ? e.CosmicObjectSO._name : e.CosmicObjectSO.name;
            _controller?.TargetGroupOnObjectToMiddle(key);
        }, Binder);
    }

    private void OnDragForCosmicObjectView(DragInputEvent e)
    {
        if (_core.StateMachine.CurrentStateType != typeof(OnMenuState)) return;
        if (_core.SecondaryStateMachine.CurrentStateType != typeof(NoneStateSecondary)) return;
        //if (_core.SecondaryStateMachine.CurrentStateType == typeof(UpgradeState)) return;
        //if (_core.SecondaryStateMachine.CurrentStateType == typeof(SettingState)) return;

        _controller?.OnDrag(e);
    }
     
    public void Inject(Core context)
    {
        _core = context;
    }
}