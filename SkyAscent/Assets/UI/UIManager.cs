using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum UIPress
{
    None,

    Tab_QuitApp,

    Tab_Tab_Apply,
    Tab_Cancel,
    Tab_Back,

    Tab_Next,
    Tab_Previor,

    #region Menu
    Tab_Home,
    Tab_Setting,
    Tab_Shop,
    Tab_Upgrade,
    Tab_Add_Coin,
    Tab_Add_Health,

    #endregion

    #region Setting Page
    Tab_GeneralSetting,
    Tab_OtherSetting,

    #endregion

    #region Popup
    Tab_ClosePopup,
    Tab_AdRewardCoin,
    Tab_AdAddHealth,
    Tab_CoinAddHealth,
    Tab_Popup_Home,
    Tab_Popup_ContinueSession,
    
    Tab_Popup_Revive,
    #endregion

    #region Session
    Tab_Play,
    Tab_Continue,
    Tab_Pause

    #endregion
}

public enum StatisticalPageType // làm sau nhé, vì đang dùng popup để thay thế
{
    Crististi_Vistory,
    Crististi_GameOver,
}

public enum PopupType
{
    None = 0,
    Popup_Pause = 1,
    Popup_GameOver = 2,
    Popup_Vistory = 3,

    Popup_CountNumbers = 4,
    Popup_AddCoin = 5,
    Popup_AddHeath = 6,
    Popup_ViewObject = 7,
}

public enum PopupElementType
{
    Description,
    InputField,
    ButtonImage,
    ButtonSup,
    ButtonMain
}

public class PopupElementData
{
    public int order;
    public PopupElementType type;

    public string title;       // button / input
    public string placeholder; // input
    public string value;       // desc / input

    public string nameElement;
    public Background backgroundImage;
    public bool hasBackgroundImage;
}

public partial class UIManager : CoreEventBase, IInject<Core>, IInject<ControllerButton>, IInject<ControllerPopup>
{
    private const string RewardCoinSourceImageName = "Img_AdsRewardCoin";
    private const string RewardCoinTargetImageName = "Img_ViewObject";
    private const int RewardCoinDescriptionOrder = 1;
    private const string MasterSoundSliderName = "Sound_Bt";
    private const string MusicSoundSliderName = "Music_Bt";
    private const string UISoundSliderName = "UI_Bt";
    private const string GamePlaySoundSliderName = "GamePlay_Bt";

    #region Reference // Variable

    private Core _core;
    private SoundManager _soundManager;
    private VisualElement _root;


    private List<VisualElement> _pages;
    private Dictionary<VisualElement, List<VisualElement>> _pagePanels;
    private Dictionary<VisualElement, DisplayStyle> _panelDefaultDisplay;

    // Cache lookup O(1)
    private readonly Dictionary<string, VisualElement> _pageByName = new();
    private readonly Dictionary<string, VisualElement> _panelByName = new();
    private readonly Dictionary<string, List<VisualElement>> _valueByName = new();

    [SerializeField, TextArea(5, 20)]
    private string _debugPageKeys;

    [SerializeField, TextArea(5, 20)]
    private string _debugPanelKeys;

    [SerializeField, TextArea(5, 20)]
    private string _debugValueKeys;

    private List<Button> _buttons;
    private Coroutine _popupBuildRoutine;
    private Coroutine _popupCountdownRoutine;

    [SerializeField] private ControllerButton _buttonController;
    [SerializeField] private ControllerPopup _popupController;

    private readonly Dictionary<string, SliderInt> _soundSliders = new();
    private readonly Dictionary<SliderInt, EventCallback<ChangeEvent<int>>> _soundSliderCallbacks = new();

    #endregion

    #region unity lyfecycle

    protected override void Awake()
    {
        base.Awake();

        _pages = new List<VisualElement>();
        _pagePanels = new Dictionary<VisualElement, List<VisualElement>>();
        _panelDefaultDisplay = new Dictionary<VisualElement, DisplayStyle>();
        _buttons = new List<Button>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // Lấy root
        _root = GetComponent<UIDocument>().rootVisualElement;

        // Scan UI tree (page + panel + button)
        ClearCaches();
        ScanPagesAndPanels(_root);
        ScanButtons(_root);
        ScanValueElements(_root);
        BindAudioSettingsUI(_root);

        // Init controller con
        _buttonController.Init(_root, _pages, _pagePanels, _panelDefaultDisplay);
        _buttonController.RegisterButtons(_buttons);

        _popupController.Init(
            _root,
            RegisterButton,   // cho phép popup spawn button mới và đăng ký callback
            UnregisterButton  // (phòng khi ní muốn hủy sau này)
        );

        RefreshInspectorDebugKeys();
    }

    private void Start()
    {
        SetAllPages(false); // ẩn tất cả page lúc đầu, tránh lỗi hiển thị không mong muốn
        ControlPage("Page_Home", true); // bật home page làm trang mặc định khi vào menu
    }

    protected void OnDisable()
    {
        UnbindAudioSettingsUI();

        // Hủy đăng ký callback button
        _buttonController.UnregisterButtons(_buttons);
    }

    #endregion

    #region Inject
    public void Inject(Core context) { _core = context; }
    public void Inject(ControllerButton context) { _buttonController = context; }
    public void Inject(ControllerPopup context) { _popupController = context; }

    #endregion

    #region Events

    public override void SubscribeEvents()
    {
        CoreEvents.OnUIPress.Subscribe(e => OnUIPress(e.UIPress), Binder);

        //CoreEvents.OnQuitApp.Subscribe(e => OnQuitApp(), Binder);

        // tắt hết popup khi mở menu
        CoreEvents.OnMenu.Subscribe(e => ControlPopupPage(PopupType.None, !e.IsOpenMenu), Binder);

        // bật home page khi vào menu (state menu)
        CoreEvents.OnMenu.Subscribe(e => ControlHomePage(e.IsOpenMenu), Binder);
        // bật upgrade page khi vào (state upgrade)
        CoreEvents.UpdgadePanel.Subscribe(e => ControlUpgradePage(e.IsOpenUpgradePanel), Binder);
        // bật setting page khi mở setting panel (state setting)
        CoreEvents.SettinngPanel.Subscribe(e => ControlSettingPage(e.IsOpenSettingPanel), Binder);
        CoreEvents.PlayerFife.Subscribe(e => OnPlayerFife(e), Binder);
        CoreEvents.OnPauseSession.Subscribe(e => OnPauseSession(e), Binder);
        CoreEvents.OnContinueSession.Subscribe(e => OnContinueSession(e), Binder);
        CoreEvents.OnResumeSession.Subscribe(e => OnResumeSession(e), Binder);
        CoreEvents.RewardedAdResult.Subscribe(e => OnRewardedAdResult(e), Binder);


        // tắt tất cả page khi vào session mới
        CoreEvents.OnNewSession.Subscribe(e => { OnNewsession(); ; }, Binder);
        // bật play page khi vào điểm xuất phát
        CoreEvents.OnSession.Subscribe(e => { OnSession(e.Started); }, Binder);
        // tắt play page khi chuẩn bị kết thúc session
        CoreEvents.OnPrepareEnd.Subscribe(e => OnPrepareEnd(), Binder);
        // dù có hoàn thành hay không thì cũng hiện popup kết thúc
        CoreEvents.OnEndSession.Subscribe(e => OnEndSession(e.IsComplete, e.PopupType), Binder);

        //// bật home page khi di chuyển đến cuối đường đi (camera trở về home view point)
        //CoreEvents.OnMoveAlongToPath.Subscribe(e => { if (e.IsEnd) ControlHomePage(e.IsEnd); }, Binder);

    }

    private void OnUIPress(UIPress press)
    {
        switch (press)
        {
            case UIPress.Tab_Add_Coin:
                StopPopupCountdown();
                _popupController.ClearPopupOverrides(PopupType.Popup_ViewObject);
                ControlPopupPage(PopupType.Popup_AddCoin, true);
                break;
            case UIPress.Tab_Add_Health:
                StopPopupCountdown();
                _popupController.ClearPopupOverrides(PopupType.Popup_ViewObject);
                ControlPopupPage(PopupType.Popup_AddHeath, true);
                break;
            case UIPress.Tab_ClosePopup:
                _popupController.ClearPopupOverrides(PopupType.Popup_ViewObject);
                ControlPopupPage(PopupType.None, false);
                break;
            case UIPress.Tab_GeneralSetting:
                ControlGeneralSettingPanel(true);
                break;
            case UIPress.Tab_OtherSetting:
                ControlOtherSettingPanel(true);
                break;
            default:
                //Debug.LogWarning($"[UI] Unhandled UIPress: {press}");
                break;
        }
    }

    #endregion

    #region profession

    private void OnNewsession()
    {
        // Tắt tất cả page để chuẩn bị cho session mới
        SetAllPages(false);
    }

    private void OnSession(bool started)
    {
        // Bật page chơi khi session bắt đầu
        ControlPlayPage(started);
    }

    private void OnPrepareEnd()
    {
        // Tắt page chơi để chuẩn bị kết thúc session
        ControlPlayPage(false);

    }

    private void OnEndSession(bool isComplete, PopupType popupType)
    {
        // Bắn popup tương ứng
        //var popupType = isComplete ? PopupType.Popup_Vistory : PopupType.Popup_GameOver;
        if (popupType == PopupType.None) return;

        StopPopupCountdown();
        ControlPopupPage(popupType, true);
    }

    private void OnPlayerFife(PlayerFifeEvent e)
    {
        if (e == null) return;
        if (e.state == PlayerFifeState.dead)
        {
            StopPopupCountdown();
            ControlPopupPage(PopupType.Popup_GameOver, true);
            return;
        }

        if (e.state == PlayerFifeState.Revive)
        {
            ControlPopupPage(PopupType.None, false);
        }
    }

    private void OnPauseSession(OnPauseSessionEvent e)
    {
        if (e == null) return;

        StopPopupCountdown();
        ControlPopupPage(e.PopupType, true);
    }

    private void OnContinueSession(OnContinueSessionEvent e)
    {
        if (e == null) return;
        if (e.ContinueType != ContinueSessionType.Revive) return;

        StopPopupCountdown();
        _popupCountdownRoutine = StartCoroutine(RunPopupCountdown(e.PopupType));
    }

    private void OnResumeSession(OnResumeSessionEvent e)
    {
        if (e == null) return;

        StopPopupCountdown();
        ControlPopupPage(PopupType.None, false);
    }

    private void OnRewardedAdResult(RewardedAdResultEvent e)
    {
        if (e == null) return;
        if (e.Placement != RewardedAdPlacement.AddCoin) return;
        if (e.ResultType != RewardedAdResultType.RewardEarned) return;

        PrepareRewardCoinResultPopup();
        StopPopupCountdown();
        ControlPopupPage(PopupType.Popup_ViewObject, true);
    }

    private void PrepareRewardCoinResultPopup()
    {
        _popupController.ClearPopupOverrides(PopupType.Popup_ViewObject);
        _popupController.SetPopupDescriptionOverride(
            PopupType.Popup_ViewObject,
            RewardCoinDescriptionOrder,
            $"Gain {ItemManager.DefaultRewardedAdCoinAmount} Coin");

        if (_popupController.TryGetPopupImage(
                PopupType.Popup_AddCoin,
                RewardCoinSourceImageName,
                out Background rewardCoinImage))
        {
            _popupController.SetPopupImageOverride(
                PopupType.Popup_ViewObject,
                RewardCoinTargetImageName,
                rewardCoinImage);
        }
    }

    private IEnumerator RunPopupCountdown(PopupType popupType)
    {
        ControlPopupPage(popupType, true);

        yield return null;

        if (!_popupController.TryGetPopupTitle(popupType, out string title) ||
            !int.TryParse(title, out int countFrom))
        {
            countFrom = 3;
        }

        for (int count = countFrom; count >= 0; count--)
        {
            _popupController.SetCurrentTitle(count.ToString());

            if (count == 0)
                break;

            yield return new WaitForSeconds(1f);
        }

        _popupCountdownRoutine = null;
        ControlPopupPage(PopupType.None, false);
        CoreEvents.OnResumeSession.Raise(new OnResumeSessionEvent(ResumeSessionType.Revive));
    }

    #endregion

    #region Scan UI tree

    private void ScanPagesAndPanels(VisualElement root)
    {
        // Nếu root null => fail fast
        if (root == null) return;

        // Pages
        var pages = root.Query<VisualElement>(className: "page_Base").ToList();
        foreach (var page in pages)
        {
            if (page == null || string.IsNullOrEmpty(page.name))
                continue;

            _pages.Add(page);

            // Cache page by name
            if (_pageByName.ContainsKey(page.name))
            {
                // Duplicate name => rất nguy hiểm
                Debug.LogWarning($"[UI] Duplicate page name detected: {page.name}");
            }
            _pageByName[page.name] = page;

            // Panels inside a page (tuỳ vào convention của Ní)
            var panels = page.Query<VisualElement>(className: "panel").ToList(); // ưu tiên tìm theo class "panel" trước để tránh quét quá nhiều element con khác
            if (panels.Count == 0) // fallback: nếu không tìm thấy panel nào theo class, thì tìm theo convention name "Panel_*"
            {
                panels = new List<VisualElement>();
                page.Query<VisualElement>().ForEach(p =>
                {
                    if (!string.IsNullOrEmpty(p.name) && p.name.StartsWith("Panel_", StringComparison.Ordinal))
                        panels.Add(p);
                });
            }

            _pagePanels[page] = panels;

            foreach (var panel in panels)
            {
                if (panel == null) continue;

                if (!_panelDefaultDisplay.ContainsKey(panel))
                    _panelDefaultDisplay[panel] = panel.style.display.value;

                if (!string.IsNullOrEmpty(panel.name))
                {
                    if (_panelByName.ContainsKey(panel.name))
                        Debug.LogWarning($"[UI] Duplicate panel name detected: {panel.name}");

                    _panelByName[panel.name] = panel;
                }
            }
        }

        Debug.Log($"[UI] Scanned Pages: {_pages.Count}, Panels: {_panelByName.Count}");
    }

    private void ScanButtons(VisualElement root)
    {
        _buttons = root.Query<Button>().ToList();
    }

    /// <summary>
    /// Scan all UI elements whose name starts with "Value_".
    /// Supports duplicated names across many pages, including ProgressBarCustom.
    /// </summary>
    /// <param name="root">Root visual element.</param>
    /// <remarks>
    /// Example:
    /// - Page_Home/Value_Coin
    /// - Page_Play/Value_Coin
    /// - Page_Result/Value_Coin
    /// All of them will be cached under the same key: "Value_Coin".
    /// </remarks>
    private void ScanValueElements(VisualElement root)
    {
        if (root == null) return;

        var elements = root.Query<VisualElement>().ToList();
        int totalFound = 0;
        int uniqueKeys = 0;

        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (element == null) continue;
            if (string.IsNullOrEmpty(element.name)) continue;

            if (!element.name.StartsWith("Value_", StringComparison.Ordinal))
                continue;

            if (!_valueByName.TryGetValue(element.name, out var list))
            {
                list = new List<VisualElement>();
                _valueByName[element.name] = list;
                uniqueKeys++;
            }

            list.Add(element);
            totalFound++;
        }

        foreach (var kv in _valueByName)
        {
            if (kv.Value.Count > 1)
            {
                Debug.Log($"[UIManager] Multi-bind value key: {kv.Key}, count={kv.Value.Count}");
            }
        }

        Debug.Log($"[UIManager] Scanned Value Elements: total={totalFound}, uniqueKeys={uniqueKeys}");
    }

    private void ClearCaches()
    {
        _pages.Clear();
        _buttons.Clear();

        _pagePanels.Clear();
        _panelDefaultDisplay.Clear();

        _pageByName.Clear();
        _panelByName.Clear();
        _valueByName.Clear();
    }

    /// <summary>
    /// Update debug strings so you can inspect cached keys in Inspector.
    /// </summary>
    /// <remarks>
    /// Unity cannot serialize Dictionary, so we expose keys via strings.
    /// </remarks>
    public void RefreshInspectorDebugKeys()
    {
        _debugPageKeys = BuildKeyDump(_pageByName);
        _debugPanelKeys = BuildKeyDump(_panelByName);
        _debugValueKeys = BuildKeyDump(_valueByName);
    }

    /// <summary>
    /// Build a readable dump of dictionary keys.
    /// </summary>
    /// <returns>Multiline key list.</returns>
    private string BuildKeyDump(Dictionary<string, VisualElement> dict)
    {
        if (dict == null || dict.Count == 0) return "(empty)";

        var keys = new List<string>(dict.Keys);
        keys.Sort(StringComparer.Ordinal);

        return string.Join("\n", keys);
    }

    /// <summary>
    /// Build a readable dump of dictionary keys and element counts.
    /// </summary>
    /// <returns>Multiline key list with counts.</returns>
    private string BuildKeyDump(Dictionary<string, List<VisualElement>> dict)
    {
        if (dict == null || dict.Count == 0) return "(empty)";

        var keys = new List<string>(dict.Keys);
        keys.Sort(StringComparer.Ordinal);

        var lines = new List<string>(keys.Count);
        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var count = dict[key] != null ? dict[key].Count : 0;
            lines.Add($"{key} ({count})");
        }

        return string.Join("\n", lines);
    }

    #endregion

    #region Button Register wrapper

    private void RegisterButton(Button btn)
    {
        if (btn == null) return;
        if (!_buttons.Contains(btn))
            _buttons.Add(btn);

        _buttonController.RegisterButton(btn);
    }

    private void UnregisterButton(Button btn)
    {
        if (btn == null) return;
        _buttonController.UnregisterButton(btn);
        _buttons.Remove(btn);
    }

    #endregion

    #region Page Control

    private void ControlHomePage(bool turnOn)
    {
        ControlPage("Page_Home", turnOn);

        ControlPage("Page_Setting", !turnOn);
        ControlPage("Page_Upgrade", !turnOn);

        if (turnOn)
            ControlPlayPage(false);
    }

    private void ControlSettingPage(bool turnOn)
    {
        ControlPage("Page_Setting", turnOn);

        if (turnOn)
        {
            RefreshAudioSettingsUI();
            ControlPopupPage(PopupType.None, false);
            return;
        }

        bool isMenuState = _core != null && _core.StateMachine.CurrentStateType == typeof(OnMenuState);
        if (isMenuState)
        {
            ControlPage("Page_Home", true);
        }
        else
        {
            ControlPlayPage(true);
        }
    }

    /// <summary>
    /// trurnOn = true khi muốn bật page upgrade, false khi muốn tắt
    /// </summary>
    /// <param name="turnOn"></param>
    private void ControlUpgradePage(bool turnOn)
    {
        ControlPage("Page_Upgrade", turnOn);

        ControlPage("Page_Home", !turnOn);

    }

    private void ControlPlayPage(bool turnOn)
    {
        ControlPage("Page_Play", turnOn);
    }

    private void ControlPopupPage(PopupType type, bool turnOn)
    {
        if (!turnOn)
        {
            PopupType currentPopupType = _popupController != null ? _popupController.CurrentPopupType : PopupType.None;
            StopPopupBuildRoutine();
            StopPopupCountdown();
            _popupController.HidePopup();
            CoreEvents.OnPopupState.Raise(new OnPopupStateEvent(currentPopupType, false));
            return;
        }

        StopPopupBuildRoutine();
        CoreEvents.OnPopupState.Raise(new OnPopupStateEvent(type, true));
        _popupBuildRoutine = StartCoroutine(BuildPopupRoutine(type));
    }

    private void StopPopupBuildRoutine()
    {
        if (_popupBuildRoutine == null) return;

        StopCoroutine(_popupBuildRoutine);
        _popupBuildRoutine = null;
    }

    private void StopPopupCountdown()
    {
        if (_popupCountdownRoutine == null) return;

        StopCoroutine(_popupCountdownRoutine);
        _popupCountdownRoutine = null;
    }

    private IEnumerator BuildPopupRoutine(PopupType type)
    {
        yield return _popupController.BuildPopupCoroutine(type);
        _popupBuildRoutine = null;
    }

    #endregion

    #region Panel Control

    private void ControlGeneralSettingPanel(bool turnOn)
    {
        //ControlOtherSettingPanel(!turnOn);

        SetPanel("Panel_OtherSetting", !turnOn);
        SetPanel("Panel_GeneralSetting", turnOn);
    }

    private void ControlOtherSettingPanel(bool turnOn)
    {
        SetPanel("Panel_GeneralSetting", !turnOn);
        SetPanel("Panel_OtherSetting", turnOn);
    }

    #endregion

    #region Ubility

    private void SetPanel(string panelName, bool turnOn)
    {
        if (string.IsNullOrEmpty(panelName))
            return;
        if (!_panelByName.TryGetValue(panelName, out var targetPanel) || targetPanel == null)
        {
            Debug.LogWarning($"[UIController] Panel not found in cache: {panelName}. Did you scan rootVisualElement?");
            return;
        }
        var defaultDisplay = _panelDefaultDisplay.ContainsKey(targetPanel) ? _panelDefaultDisplay[targetPanel] : DisplayStyle.Flex;
        targetPanel.style.display = turnOn ? defaultDisplay : DisplayStyle.None;
    }

    /// <summary>
    /// Show/hide a page by cached name lookup (O(1)).
    /// </summary>
    /// <param name="pageName">UXML element name of the page (e.g. "Page_Home")</param>
    /// <param name="turnOn">True to show, false to hide.</param>
    private void ControlPage(string pageName, bool turnOn)
    {
        // Guard
        if (string.IsNullOrEmpty(pageName))
            return;

        if (!_pageByName.TryGetValue(pageName, out var targetPage) || targetPage == null)
        {
            Debug.LogWarning($"[UI] Page not found in cache: {pageName}. Did you scan rootVisualElement?");
            return;
        }

        // Hide all pages (nếu đây là behavior của bạn)
        if (turnOn)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                var p = _pages[i];
                if (p != null) p.style.display = DisplayStyle.None;
            }

            targetPage.style.display = DisplayStyle.Flex;
        }
        else
        {
            targetPage.style.display = DisplayStyle.None;
        }
    }

    /// <summary>
    /// Set display for all pages without needing any page name.
    /// </summary>
    /// <remarks>
    /// Use this to globally hide/show pages (e.g. during loading, popup, transitions).
    /// </remarks>
    /// <param name="turnOn">True to show, false to hide.</param>
    /// <param name="displayWhenOn">Display style when enabled (default Flex).</param>
    /// <returns>Number of pages affected.</returns>
    private int SetAllPages(bool turnOn, DisplayStyle displayWhenOn = DisplayStyle.Flex)
    {
        if (_pages == null || _pages.Count == 0) return 0;

        var style = turnOn ? displayWhenOn : DisplayStyle.None;

        int count = 0;
        for (int i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            if (page == null) continue;

            page.style.display = style;
            count++;
        }
        Debug.Log($"[UI] SetAllPages: turnOn={turnOn}, affected={count}");

        return count;
    }

    /// <summary>
    /// Set display for pages by a rule, without referencing page names.
    /// </summary>
    /// <remarks>
    /// match(page)==true => set to onStyle; otherwise set to offStyle.
    /// This is O(n) over pages and avoids string lookups.
    /// </remarks>
    /// <param name="match">Rule to decide which pages are enabled.</param>
    /// <param name="onStyle">Display style for matched pages.</param>
    /// <param name="offStyle">Display style for non-matched pages.</param>
    /// <returns>Number of pages matched.</returns>
    private int SetPagesByRule(Func<VisualElement, bool> match, DisplayStyle onStyle = DisplayStyle.Flex, DisplayStyle offStyle = DisplayStyle.None)
    {
        if (match == null) return 0;
        if (_pages == null || _pages.Count == 0) return 0;

        int matched = 0;

        for (int i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            if (page == null) continue;

            bool isOn = match(page);
            page.style.display = isOn ? onStyle : offStyle;

            if (isOn) matched++;
        }

        return matched;
    }

    #endregion

    #region Audio Settings

    private void BindAudioSettingsUI(VisualElement root)
    {
        UnbindAudioSettingsUI();
        ResolveSoundManager();

        BindSoundSlider(root, MasterSoundSliderName, null);
        BindSoundSlider(root, MusicSoundSliderName, SoundChannel.Music);
        BindSoundSlider(root, UISoundSliderName, SoundChannel.UI);
        BindSoundSlider(root, GamePlaySoundSliderName, SoundChannel.GamePlay);

        RefreshAudioSettingsUI();
    }

    private void UnbindAudioSettingsUI()
    {
        foreach (var pair in _soundSliderCallbacks)
        {
            if (pair.Key == null)
                continue;

            pair.Key.UnregisterValueChangedCallback(pair.Value);
        }

        _soundSliderCallbacks.Clear();
        _soundSliders.Clear();
    }

    private void BindSoundSlider(VisualElement root, string sliderName, SoundChannel? channel)
    {
        if (root == null || string.IsNullOrEmpty(sliderName))
            return;

        SliderInt slider = root.Q<SliderInt>(sliderName);
        if (slider == null)
            return;

        EventCallback<ChangeEvent<int>> callback = evt => OnSoundSliderValueChanged(channel, evt.newValue);
        slider.RegisterValueChangedCallback(callback);

        _soundSliders[sliderName] = slider;
        _soundSliderCallbacks[slider] = callback;
    }

    private void OnSoundSliderValueChanged(SoundChannel? channel, int newValue)
    {
        ResolveSoundManager();
        if (_soundManager == null)
            return;

        int clampedValue = Mathf.Clamp(newValue, 0, 100);

        if (channel.HasValue)
        {
            _soundManager.SetChannelVolumePercent(channel.Value, clampedValue);
            return;
        }

        _soundManager.SetMasterVolumePercent(clampedValue);
    }

    private void RefreshAudioSettingsUI()
    {
        ResolveSoundManager();
        if (_soundManager == null)
            return;

        SetSoundSliderValue(MasterSoundSliderName, _soundManager.GetMasterVolumePercent());
        SetSoundSliderValue(MusicSoundSliderName, _soundManager.GetChannelVolumePercent(SoundChannel.Music));
        SetSoundSliderValue(UISoundSliderName, _soundManager.GetChannelVolumePercent(SoundChannel.UI));
        SetSoundSliderValue(GamePlaySoundSliderName, _soundManager.GetChannelVolumePercent(SoundChannel.GamePlay));
    }

    private void SetSoundSliderValue(string sliderName, int value)
    {
        if (!_soundSliders.TryGetValue(sliderName, out SliderInt slider) || slider == null)
            return;

        slider.SetValueWithoutNotify(Mathf.Clamp(value, 0, 100));
    }

    private void ResolveSoundManager()
    {
        if (_soundManager != null)
            return;

        _soundManager = SoundManager.Instance != null
            ? SoundManager.Instance
            : FindFirstObjectByType<SoundManager>();
    }

    #endregion
}

// API
public partial class UIManager
{
    /// <summary>
    /// Set text for all cached value elements by name.
    /// </summary>
    /// <param name="valueName">Element name, for example "Value_Coin".</param>
    /// <param name="text">New displayed text.</param>
    /// <returns>True if at least one element was updated successfully.</returns>
    /// <remarks>
    /// If many pages contain the same value key, all of them will be updated.
    /// Works for custom controls by searching a TextElement inside each cached root.
    /// </remarks>
    public bool SetValueText(string valueName, string text)
    {
        if (string.IsNullOrEmpty(valueName))
            return false;

        if (!_valueByName.TryGetValue(valueName, out var elements) || elements == null || elements.Count == 0)
        {
            Debug.LogWarning($"[UIController] Value element not found in cache: {valueName}");
            return false;
        }

        bool updatedAny = false;
        string safeText = text ?? string.Empty;

        for (int i = 0; i < elements.Count; i++)
        {
            var rootElement = elements[i];
            if (rootElement == null) continue;

            var textElement = rootElement as TextElement ?? rootElement.Q<TextElement>();
            if (textElement == null)
            {
                Debug.LogWarning($"[UIController] No TextElement found inside: {valueName} at index={i}");
                continue;
            }

            textElement.text = safeText;
            updatedAny = true;
        }

        return updatedAny;
    }

    /// <summary>
    /// Set numeric value for all cached ProgressBarCustom elements by name.
    /// </summary>
    /// <param name="valueName">Element name, for example "Value_Heath".</param>
    /// <param name="value">New progress value in the bar's min/max range.</param>
    /// <returns>True if at least one ProgressBarCustom was updated.</returns>
    public bool SetValueFloat(string valueName, float value)
    {
        if (string.IsNullOrEmpty(valueName))
            return false;

        if (!_valueByName.TryGetValue(valueName, out var elements) || elements == null || elements.Count == 0)
        {
            Debug.LogWarning($"[UIController] Value element not found in cache: {valueName}");
            return false;
        }

        bool updatedAny = false;

        for (int i = 0; i < elements.Count; i++)
        {
            var rootElement = elements[i];
            if (rootElement == null) continue;

            if (rootElement is ProgressBarCustom progressBar)
            {
                progressBar.value = value;
                updatedAny = true;
                continue;
            }

            var nestedProgressBar = rootElement.Q<ProgressBarCustom>();
            if (nestedProgressBar == null)
            {
                Debug.LogWarning($"[UIController] No ProgressBarCustom found inside: {valueName} at index={i}");
                continue;
            }

            nestedProgressBar.value = value;
            updatedAny = true;
        }

        return updatedAny;
    }

    public bool SetProgressValue(string valueName, float value)
    {
        return SetValueFloat(valueName, value);
    }

    /// <summary>
    /// Set integer text for a cached value element.
    /// </summary>
    /// <param name="valueName">Element name, for example "Value_Coin".</param>
    /// <param name="value">Integer value to display.</param>
    /// <returns>True if updated successfully, otherwise false.</returns>
    public bool SetValueInt(string valueName, int value)
    {
        return SetValueText(valueName, value.ToString());
    }
}

#if UNITY_EDITOR

[UnityEditor.CustomEditor(typeof(UIManager))]
public class UIUIManagerEditor : UnityEditor.Editor
{
    private void Reset()
    {
        if (target is UIManager controller)
        {
            controller.RefreshInspectorDebugKeys();
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Refresh Debug Keys"))
        {

        }
    }
}

# endif
