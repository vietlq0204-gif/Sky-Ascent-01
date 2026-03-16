using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// ControllerPopup: lo phần popup + template + build theo strategy
[Serializable]
public class ControllerPopup
{
    [Header("Popup Strategies")]
    public List<PopupElementStrategySO> popupStrategies =
        new List<PopupElementStrategySO>(); // cấu hình trong Inspector

    [Header("Popup build settings")]
    public float popupElementDelay = 0.1f; // delay giữa mỗi element (giây)

    #region RUNTIME FIELDS (không serialize)

    [NonSerialized] private VisualElement _root;

    [NonSerialized] private VisualElement _popupRoot;   // // Popup_Popup
    [NonSerialized] private VisualElement _popupTop;    // // Top
    [NonSerialized] private VisualElement _popupMiddle; // // Middle
    [NonSerialized] private VisualElement _popupBottom; // // Bottom

    [NonSerialized] private VisualElement _popupTemplateBox; // // BoxTempleElement

    [NonSerialized] private VisualTreeAsset _titleAsset;
    [NonSerialized] private VisualTreeAsset _descriptionAsset;
    [NonSerialized] private VisualTreeAsset _inputAsset;
    [NonSerialized] private VisualTreeAsset _tabAsset;
    [NonSerialized] private VisualTreeAsset _tabImageAsset;

    [NonSerialized] private Action<Button> _registerButton;
    [NonSerialized] private Action<Button> _unregisterButton;
    [NonSerialized] private Label _currentTitleLabel;
    [NonSerialized] private PopupType _currentPopupType = PopupType.None;
    [NonSerialized] private readonly Dictionary<PopupType, Dictionary<string, Background>> _popupImageOverrides =
        new Dictionary<PopupType, Dictionary<string, Background>>();
    [NonSerialized] private readonly Dictionary<PopupType, Dictionary<int, string>> _popupDescriptionOverrides =
        new Dictionary<PopupType, Dictionary<int, string>>();

    #endregion

    #region INIT

    public void Init(
        VisualElement root,
        Action<Button> registerButton,
        Action<Button> unregisterButton)
    {
        _root = root;
        _registerButton = registerButton;
        _unregisterButton = unregisterButton;

        InitPopupLayoutAndTemplates();
    }

    private void InitPopupLayoutAndTemplates()
    {
        // // 1 popup chung: "Popup_Popup" hoặc popup_Base đầu tiên
        _popupRoot = _root.Q<VisualElement>("Popup_Popup");
        if (_popupRoot == null)
        {
            _popupRoot = _root.Query<VisualElement>().Class("popup_Base").First();
        }

        if (_popupRoot == null)
        {
            Debug.LogWarning("[UI] Không tìm thấy Popup_Popup.");
            return;
        }

        var container = _popupRoot.Q<VisualElement>("Container");
        _popupTop = container?.Q<VisualElement>("Top");
        _popupMiddle = container?.Q<VisualElement>("Middle");
        _popupBottom = container?.Q<VisualElement>("Bottom");

        _popupTemplateBox = _root.Q<VisualElement>("BoxTempleElement");
        if (_popupTemplateBox == null)
        {
            Debug.LogWarning("[UI] Không tìm thấy BoxTempleElement.");
            return;
        }

        _popupTemplateBox.style.display = DisplayStyle.None;

        // // Load template asset theo tên
        _titleAsset = GetTemplateAsset(_popupTemplateBox, "Title");
        _descriptionAsset = GetTemplateAsset(_popupTemplateBox, "Description");
        _inputAsset = GetTemplateAsset(_popupTemplateBox, "TextField");
        _tabAsset = GetTemplateAsset(_popupTemplateBox, "Tab_A1");
        _tabImageAsset = GetTemplateAsset(_popupTemplateBox, "Tab_Image");
    }

    private VisualTreeAsset GetTemplateAsset(VisualElement box, string assetName)
    {
        if (box == null) return null;

        var templates = box.Query<TemplateContainer>().ToList();
        foreach (var tc in templates)
        {
            var src = tc.templateSource;
            if (src != null && src.name == assetName)
                return src;
        }

        Debug.LogWarning($"[UI] Không tìm thấy template asset '{assetName}' trong BoxTempleElement.");
        return null;
    }

    private VisualElement InstantiateFrom(VisualTreeAsset asset)
    {
        if (asset == null) return null;
        TemplateContainer container = asset.CloneTree();
        return container;
    }

    #endregion

    #region BUILD ELEMENT HELPERS

    private List<PopupElementData> BuildElementList(PopupElementStrategySO strategy)
    {
        var elements = new List<PopupElementData>();

        // // Descriptions
        if (strategy.Descriptions != null)
        {
            foreach (var d in strategy.Descriptions)
            {
                if (d == null) continue;

                string descriptionValue = d.value;
                if (TryGetDescriptionOverride(strategy.popupType, d.order, out string overrideValue))
                    descriptionValue = overrideValue;

                elements.Add(new PopupElementData
                {
                    order = d.order,
                    type = PopupElementType.Description,
                    value = descriptionValue
                });
            }
        }

        // // InputFields
        if (strategy.InputFields != null)
        {
            foreach (var i in strategy.InputFields)
            {
                if (i == null) continue;
                elements.Add(new PopupElementData
                {
                    order = i.order,
                    type = PopupElementType.InputField,
                    title = i.title,
                    placeholder = i.placeholder,
                    value = i.value
                });
            }
        }

        // // Buttons Image
        if (strategy.ButtonsImage != null)
        {
            foreach (var b in strategy.ButtonsImage)
            {
                if (b == null) continue;

                Background imageBackground = default;
                bool hasBackgroundImage = TryGetOverrideBackground(strategy.popupType, b.NameElement, out imageBackground);
                if (!hasBackgroundImage)
                {
                    hasBackgroundImage = b.TryGetBackground(out imageBackground);
                }

                elements.Add(new PopupElementData
                {
                    order = b.order,
                    type = PopupElementType.ButtonImage,
                    title = b.title,
                    nameElement = b.NameElement,
                    backgroundImage = imageBackground,
                    hasBackgroundImage = hasBackgroundImage
                });

            }
        }

        // // Buttons Sup
        if (strategy.ButtonsSup != null)
        {
            foreach (var b in strategy.ButtonsSup)
            {
                if (b == null) continue;
                elements.Add(new PopupElementData
                {
                    order = b.order,
                    type = PopupElementType.ButtonSup,
                    title = b.title,
                    nameElement = b.NameElement
                });
            }
        }

        // // Buttons Main
        if (strategy.ButtonsMain != null)
        {
            foreach (var b in strategy.ButtonsMain)
            {
                if (b == null) continue;
                elements.Add(new PopupElementData
                {
                    order = b.order,
                    type = PopupElementType.ButtonMain,
                    title = b.title,
                    nameElement = b.NameElement
                });
            }
        }

        return elements;
    }

    private void BuildDescription(PopupElementData e)
    {
        if (_descriptionAsset == null) return;

        var inst = InstantiateFrom(_descriptionAsset);

        // // 1) ưu tiên custom TextArea nếu có
        var ta = inst.Q<TextArea>() ?? inst as TextArea;
        if (ta != null)
        {
            ta.SetText(e.value); // // API custom của Ní
        }
        else
        {
            var lbl = inst.Q<Label>() ?? inst as Label;
            if (lbl != null)
                lbl.text = e.value;
        }

        _popupMiddle.Add(inst);
    }

    private void BuildInputField(PopupElementData e)
    {
        if (_inputAsset == null) return;

        var inst = InstantiateFrom(_inputAsset);
        var tf = inst.Q<TextField>();
        if (tf != null)
        {
            tf.label = e.title;
            tf.tooltip = e.placeholder;
            tf.value = e.value;
        }

        _popupMiddle.Add(inst);
    }

    private void BuildButtonImage(PopupElementData e)
    {
        if (_tabImageAsset == null) return;

        var inst = InstantiateFrom(_tabImageAsset);
        var btn = inst.Q<Button>() ?? inst as Button;

        if (btn != null)
        {
            btn.text = e.title;

            if (!string.IsNullOrEmpty(e.nameElement))
                btn.name = e.nameElement;

            if (e.hasBackgroundImage)
                btn.style.backgroundImage = new StyleBackground(e.backgroundImage);

            _registerButton?.Invoke(btn);
        }

        _popupMiddle.Add(inst);
    }

    private void BuildButtonSup(PopupElementData e)
    {
        if (_tabAsset == null) return;

        var inst = InstantiateFrom(_tabAsset);
        var btn = inst.Q<Button>() ?? inst as Button;

        if (btn != null)
        {
            btn.text = e.title;

            // --- ĐẶT TÊN THEO NameElement ---
            if (!string.IsNullOrEmpty(e.nameElement))
                btn.name = e.nameElement;

            _registerButton?.Invoke(btn);
        }

        _popupMiddle.Add(inst);
    }

    private void BuildButtonMain(PopupElementData e)
    {
        if (_tabAsset == null) return;

        var inst = InstantiateFrom(_tabAsset);
        var btn = inst.Q<Button>() ?? inst as Button;

        if (btn != null)
        {
            btn.text = e.title;

            if (!string.IsNullOrEmpty(e.nameElement))
                btn.name = e.nameElement;

            _registerButton?.Invoke(btn);
        }

        _popupBottom.Add(inst);
    }

    #endregion

    #region PUBLIC API

    public void HidePopup()
    {
        if (_popupRoot != null)
            _popupRoot.style.display = DisplayStyle.None;

        _currentPopupType = PopupType.None;
        _currentTitleLabel = null;
    }

    public PopupType CurrentPopupType => _currentPopupType;

    public bool TryGetPopupTitle(PopupType type, out string title)
    {
        title = null;

        var strategy = popupStrategies.Find(s => s != null && s.popupType == type);
        if (strategy == null) return false;

        title = strategy.Title;
        return !string.IsNullOrEmpty(title);
    }

    public bool TryGetPopupImage(PopupType type, string elementName, out Background background)
    {
        background = default;

        if (string.IsNullOrEmpty(elementName))
            return false;

        var strategy = popupStrategies.Find(s => s != null && s.popupType == type);
        if (strategy?.ButtonsImage == null)
            return false;

        foreach (var imageElement in strategy.ButtonsImage)
        {
            if (imageElement == null)
                continue;

            if (!string.Equals(imageElement.NameElement, elementName, StringComparison.Ordinal))
                continue;

            if (!imageElement.TryGetBackground(out background))
                return false;
            return true;
        }

        return false;
    }

    public void SetPopupImageOverride(PopupType type, string elementName, Background background)
    {
        if (type == PopupType.None || string.IsNullOrEmpty(elementName) || !HasBackgroundAsset(background))
            return;

        if (!_popupImageOverrides.TryGetValue(type, out var imageMap))
        {
            imageMap = new Dictionary<string, Background>(StringComparer.Ordinal);
            _popupImageOverrides[type] = imageMap;
        }

        imageMap[elementName] = background;
    }

    public void SetPopupDescriptionOverride(PopupType type, int order, string value)
    {
        if (type == PopupType.None)
            return;

        if (!_popupDescriptionOverrides.TryGetValue(type, out var descriptionMap))
        {
            descriptionMap = new Dictionary<int, string>();
            _popupDescriptionOverrides[type] = descriptionMap;
        }

        descriptionMap[order] = value ?? string.Empty;
    }

    public void ClearPopupOverrides(PopupType type)
    {
        if (type == PopupType.None)
            return;

        _popupImageOverrides.Remove(type);
        _popupDescriptionOverrides.Remove(type);
    }

    public void SetCurrentTitle(string title)
    {
        if (_currentTitleLabel == null) return;
        _currentTitleLabel.text = title ?? string.Empty;
    }

    public IEnumerator BuildPopupCoroutine(PopupType type)
    {
        if (_popupTop == null || _popupMiddle == null || _popupBottom == null)
            yield break;

        var strategy = popupStrategies.Find(s => s.popupType == type);
        if (strategy == null)
        {
            Debug.LogWarning($"[UI] Không tìm thấy PopupElementStrategySO cho {type}");
            yield break;
        }

        // // Clear content cũ
        _popupTop.Clear();
        _popupMiddle.Clear();
        _popupBottom.Clear();
        _currentTitleLabel = null;
        _currentPopupType = type;

        // // Show khung popup
        _popupRoot.style.display = DisplayStyle.Flex;

        // =========================================================
        // 1) TITLE (build ngay, không delay)
        // =========================================================
        if (!string.IsNullOrEmpty(strategy.Title) && _titleAsset != null)
        {
            var titleRoot = InstantiateFrom(_titleAsset);
            var label = titleRoot.Q<Label>();
            if (label != null)
            {
                label.text = strategy.Title;
                _currentTitleLabel = label;
            }

            _popupTop.Add(titleRoot);
        }

        // // Nhường 1 frame cho khung + title
        yield return null;

        // =========================================================
        // 2) BUILD LIST ELEMENT TỪ STRATEGY
        // =========================================================

        List<PopupElementData> elements = BuildElementList(strategy);

        // Sort theo order
        elements.Sort((a, b) => a.order.CompareTo(b.order));

        // =========================================================
        // 3) BUILD TỪNG ELEMENT THEO ORDER + DELAY
        // =========================================================
        foreach (var e in elements)
        {
            switch (e.type)
            {
                case PopupElementType.Description:
                    BuildDescription(e);
                    break;

                case PopupElementType.InputField:
                    BuildInputField(e);
                    break;

                case PopupElementType.ButtonImage:
                    BuildButtonImage(e);
                    break;

                case PopupElementType.ButtonSup:
                    BuildButtonSup(e);
                    break;

                case PopupElementType.ButtonMain:
                    BuildButtonMain(e);
                    break;
            }

            if (popupElementDelay > 0f)
                yield return new WaitForSeconds(popupElementDelay);
            else
                yield return null;
        }
    }

    private bool TryGetOverrideBackground(PopupType type, string elementName, out Background background)
    {
        background = default;

        if (string.IsNullOrEmpty(elementName))
            return false;

        if (!_popupImageOverrides.TryGetValue(type, out var imageMap))
            return false;

        return imageMap.TryGetValue(elementName, out background) && HasBackgroundAsset(background);
    }

    private bool TryGetDescriptionOverride(PopupType type, int order, out string value)
    {
        value = null;

        if (!_popupDescriptionOverrides.TryGetValue(type, out var descriptionMap))
            return false;

        return descriptionMap.TryGetValue(order, out value);
    }

    private static bool HasBackgroundAsset(Background background)
    {
        return background.texture != null ||
               background.sprite != null ||
               background.renderTexture != null ||
               background.vectorImage != null;
    }

    #endregion
}
