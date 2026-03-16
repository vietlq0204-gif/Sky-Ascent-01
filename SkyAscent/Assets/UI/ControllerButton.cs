using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine;

/// <summary>
/// ControllerButton không cần kế thừa MonoBehaviour
/// nhưng phải [Serializable] để hiện trong Inspector (nếu ní muốn)
/// </summary>
[Serializable]
public class ControllerButton
{
    [NonSerialized] private VisualElement _root;
    [NonSerialized] private List<VisualElement> _pages;
    [NonSerialized] private Dictionary<VisualElement, List<VisualElement>> _pagePanels;
    [NonSerialized] private Dictionary<VisualElement, DisplayStyle> _panelDefaultDisplay;

    // Init từ UIControllerBase
    public void Init(
        VisualElement root,
        List<VisualElement> pages,
        Dictionary<VisualElement, List<VisualElement>> pagePanels,
        Dictionary<VisualElement, DisplayStyle> panelDefaultDisplay)
    {
        _root = root;
        _pages = pages;
        _pagePanels = pagePanels;
        _panelDefaultDisplay = panelDefaultDisplay;
    }

    // Đăng ký nhiều button
    public void RegisterButtons(List<Button> buttons)
    {
        if (buttons == null) return;
        foreach (var btn in buttons)
            RegisterButton(btn);
    }

    // Đăng ký 1 button
    public void RegisterButton(Button btn)
    {
        if (btn == null) return;
        btn.RegisterCallback<ClickEvent>(OnButtonClickedUnified);
    }

    // Hủy đăng ký nhiều button
    public void UnregisterButtons(List<Button> buttons)
    {
        if (buttons == null) return;
        foreach (var btn in buttons)
            UnregisterButton(btn);
    }

    // Hủy đăng ký 1 button
    public void UnregisterButton(Button btn)
    {
        if (btn == null) return;
        btn.UnregisterCallback<ClickEvent>(OnButtonClickedUnified);
    }

    // Callback chung cho tất cả button
    private void OnButtonClickedUnified(ClickEvent ev)
    {
        var btn = ev.target as Button;
        if (btn == null) return;

        Debug.Log($"[ControllerButton] Clicked: {btn.name}");
        CoreEvents.OnUIButtonClick.Raise(new OnUIButtonClickEvent(btn.name));

        //HandleTabNavigation(btn);   // Tab_Page / Tab_Panel / Tab_Popup
        HandleUIPressEvent(btn);    // Bắn OnUIPress
    }

    // TAB NAVIGATION

    private void HandleTabNavigation(Button tab)
    {
        if (!tab.name.StartsWith("Tab_"))
            return;

        // // 1) Popup: Tab_* -> Popup_*
        string popupName = tab.name.Replace("Tab_", "Popup_");
        var targetPopup = _root.Query<VisualElement>(popupName).Class("popup_Base").First();
        if (targetPopup != null)
        {
            var cur = targetPopup.resolvedStyle.display;
            targetPopup.style.display = cur == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            return;
        }

        // // 2) Page: Tab_* -> Page_*
        string pageName = tab.name.Replace("Tab_", "Page_");
        var targetPage = _root.Query<VisualElement>(pageName).First();
        if (targetPage != null)
        {
            foreach (var p in _pages)
                p.style.display = DisplayStyle.None;

            targetPage.style.display = DisplayStyle.Flex;

            if (_pagePanels != null && _pagePanels.TryGetValue(targetPage, out var panels))
            {
                foreach (var pn in panels)
                    pn.style.display = _panelDefaultDisplay[pn];
            }

            return;
        }

        // // 3) Panel: Tab_* -> Panel_* trong cùng Page
        var parentPage = FindParentPageOf(tab);
        if (parentPage != null &&
            _pagePanels != null &&
            _pagePanels.TryGetValue(parentPage, out var pp))
        {
            string panelName = tab.name.Replace("Tab_", "Panel_");
            var targetPanel = parentPage.Query<VisualElement>(panelName).First();
            if (targetPanel != null)
            {
                foreach (var p in pp)
                    p.style.display = DisplayStyle.None;

                targetPanel.style.display = DisplayStyle.Flex;
            }
        }
    }

    private VisualElement FindParentPageOf(VisualElement element)
    {
        var current = element.parent;
        while (current != null)
        {
            if (_pages.Contains(current))
                return current;

            // // popup_Base cũng xem như 1 "page" riêng
            if (current.ClassListContains("popup_Base"))
                return current;

            current = current.parent;
        }

        return null;
    }

    // UIPress EVENT

    private void HandleUIPressEvent(Button btn)
    {
        // Tên Button trùng UIPress enum: Tab_Play, Tab_Cancel,...
        if (!Enum.TryParse(btn.name, out UIPress press))
            return;

        if (press == UIPress.None)
            return;

        CoreEvents.OnUIPress.Raise(new OnUIPressEvent(press));
    }
}
