using System;
using System.Collections.Generic;
using System.Linq;                // để dùng .ToList(), .FirstOrDefault()
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]                    // cho phép chạy cả trong Editor lẫn Play Mode
public class PageManager : MonoBehaviour
{
    //[Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Kết quả scan (chỉnh text tại đây)")
        , Tooltip("Banh mắt ra nhen, chỉnh tầm bậy là cút á")]
    public List<PageGroup> pages = new List<PageGroup>();   // nhóm theo page_Base

    public UIDocument UiDocument => uiDocument;                  //expose cho Editor dùng
    public List<PageGroup> Pages => pages;                       // để Editor đọc dữ liệu

    // STRUCT DỮ LIỆU

    [Serializable]
    public class PageGroup
    {
        [HideInInspector] public string containerName;              //name của page/popup
        [HideInInspector] public string containerType;              // "page_Base" hoặc "popup_Base"

        public List<TextAreaEntry> textAreas = new List<TextAreaEntry>();  // TẤT CẢ textArea ELEMENT
        public List<TextAreaEntry> labels = new();   // LABEL THƯỜNG
        public List<TextAreaEntry> tabButtons = new();   // TAB BUTTONS
        public List<TextFieldEntry> textFields = new();  //  TextField
        public List<ProgressBarEntry> progressBars = new(); // ProgressBarCustom
    }

    [Serializable]
    public enum EntryType
    {
        Label,
        TextAreaElement,
        TabButton,
        TextField, 
        ProgressBarCustom
    }

    [Serializable]
    public class TextAreaEntry
    {
        public EntryType type;
        public string elementName;
        public string fullPath;
        [TextArea(3, 5)]
        public string text;
    }

    // TextField có 2 text: label (caption) và value (input)
    [Serializable]
    public class TextFieldEntry
    {
        public string elementName;
        public string fullPath;

        [TextArea(1, 3)] public string labelText;   // "Text Field"
        [TextArea(1, 3)] public string valueText;   // "filler text"
    }

    [Serializable]
    public class ProgressBarEntry
    {
        public string elementName;
        public string fullPath;
        public float value;
        public float min;
        public float max;
        public float angle;
    }


    // PUBLIC API

    // 1) Scan toàn bộ UI, tìm page_Base + Label.textArea
    [ContextMenu("Scan Pages & TextAreas")]
    public void Scan()
    {
        pages.Clear();

        if (uiDocument == null)
        {
            Debug.LogWarning("PageTextManager: uiDocument chưa được gán!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("PageTextManager: rootVisualElement null!");
            return;
        }

        // 1) Tìm tất cả container: page_Base + popup_Base
        var containers = root.Query<VisualElement>().ToList()
            .Where(e => e.ClassListContains("page_Base") || e.ClassListContains("popup_Base"))
            .ToList();

        foreach (var container in containers)
        {
            string type =
                container.ClassListContains("page_Base") ? "page_Base" :
                container.ClassListContains("popup_Base") ? "popup_Base" :
                "unknown";

            var group = new PageGroup
            {
                containerName = string.IsNullOrEmpty(container.name) ? "(no-name-container)" : container.name,
                containerType = type,
                textAreas = new List<TextAreaEntry>()
            };

            // ===========================
            // A. TEXTAREA (Custom Element)
            // ===========================
            var textAreaElems = container.Query<TextArea>().ToList();
            foreach (var ta in textAreaElems)
            {
                group.textAreas.Add(new TextAreaEntry
                {
                    type = EntryType.TextAreaElement,
                    elementName = ta.name,
                    fullPath = BuildElementPath(ta),
                    text = ta.text
                });
            }

            // ===========================
            // B. LABEL thường (không phải con của TextArea / TextField)
            // ===========================
            var allLabels = container.Query<Label>().ToList();
            foreach (var lbl in allLabels)
            {
                if (lbl.GetFirstAncestorOfType<TextArea>() != null)
                    continue; // label con trong TextArea

                if (lbl.GetFirstAncestorOfType<TextField>() != null)
                    continue; // label con trong TextField → để TextFieldEntry xử lý

                group.labels.Add(new TextAreaEntry
                {
                    type = EntryType.Label,
                    elementName = string.IsNullOrEmpty(lbl.name) ? "(no-name-label)" : lbl.name,
                    fullPath = BuildElementPath(lbl),
                    text = lbl.text
                });
            }


            // ===========================
            // C. TAB BUTTON (Button có prefix "Tab_")
            // ===========================
            var tabButtons = container.Query<Button>().ToList()
                                      .Where(b => !string.IsNullOrEmpty(b.name)
                                               && b.name.StartsWith("Tab_"))
                                      .ToList();

            foreach (var btn in tabButtons)
            {
                // Ưu tiên lấy trực tiếp từ Button.text
                string txt = btn.text;

                // Fallback: nếu vì lý do gì đó text rỗng, thử lấy từ Label con
                if (string.IsNullOrEmpty(txt))
                {
                    var lblChild = btn.Q<Label>();
                    if (lblChild != null)
                        txt = lblChild.text;
                }

                group.tabButtons.Add(new TextAreaEntry
                {
                    type = EntryType.TabButton,
                    elementName = btn.name,
                    fullPath = BuildElementPath(btn),
                    text = txt
                });
            }

            // ===========================
            // D. TEXTFIELD ELEMENT
            // ===========================
            var textFields = container.Query<TextField>().ToList();

            foreach (var tf in textFields)
            {
                group.textFields.Add(new TextFieldEntry
                {
                    elementName = string.IsNullOrEmpty(tf.name) ? "(no-name-textField)" : tf.name,
                    fullPath = BuildElementPath(tf),
                    labelText = tf.label,   // caption bên trái ("Text Field")
                    valueText = tf.value    // default input ("filler text")
                });
            }

            // ===========================
            // E. PROGRESS BAR CUSTOM
            // ===========================
            var progressBars = container.Query<ProgressBarCustom>().ToList();

            foreach (var progressBar in progressBars)
            {
                group.progressBars.Add(new ProgressBarEntry
                {
                    elementName = string.IsNullOrEmpty(progressBar.name) ? "(no-name-progressBar)" : progressBar.name,
                    fullPath = BuildElementPath(progressBar),
                    value = progressBar.value,
                    min = progressBar.min,
                    max = progressBar.max,
                    angle = progressBar.angle
                });
            }

            pages.Add(group);
        }

        Debug.Log($"Scan xong: {containers.Count} container (page_Base + popup_Base).");
    }

    /// <summary>
    /// Helper: Unity Button trong UI Toolkit thường có Label con → ta đọc label con:
    /// </summary>
    /// <param name="btn"></param>
    /// <returns></returns>
    private string GetButtonText(Button btn)
    {
        // Button thường chứa Label con
        var lbl = btn.Q<Label>();
        return lbl != null ? lbl.text : string.Empty;
    }


    // 2) Áp text trong Inspector vào UI (gọi tự động ở OnEnable, hoặc tự gọi)
    [ContextMenu("Apply Text To UI")]
    public void ApplyTextToUI()
    {
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;
        if (root == null) return;

        foreach (var group in pages)
        {
            // 1) tìm container (page_Base / popup_Base)
            var containers = root.Query<VisualElement>().ToList()
                .Where(e => e.ClassListContains(group.containerType))
                .ToList();

            if (containers.Count == 0)
                continue;

            VisualElement targetContainer =
                containers.FirstOrDefault(c => c.name == group.containerName) ??
                containers[0];

            if (targetContainer == null)
                continue;

            // A. TEXT AREA ELEMENT
            foreach (var entry in group.textAreas)
            {
                var tas = FindElements<TextArea>(targetContainer, entry.elementName);
                foreach (var ta in tas)
                {
                    ta.SetText(entry.text);
                }
            }

            // B. LABEL THƯỜNG
            foreach (var entry in group.labels)
            {
                var labels = FindElements<Label>(targetContainer, entry.elementName);
                foreach (var lbl in labels)
                {
                    lbl.text = entry.text;
                }
            }

            // C. TAB BUTTON
            foreach (var entry in group.tabButtons)
            {
                var buttons = FindElements<Button>(targetContainer, entry.elementName);
                foreach (var btn in buttons)
                {
                    // 1) Button.text (builder dùng cái này)
                    btn.text = entry.text;

                    // 2) Nếu có Label con thì sync luôn
                    var lbl = btn.Q<Label>();
                    if (lbl != null)
                        lbl.text = entry.text;
                }
            }

            // D. TEXTFIELD (nếu Ní đã thêm)
            foreach (var entry in group.textFields)
            {
                var tfs = FindElements<TextField>(targetContainer, entry.elementName);
                foreach (var tf in tfs)
                {
                    tf.label = entry.labelText;
                    tf.value = entry.valueText;
                }
            }

            // E. PROGRESS BAR CUSTOM
            foreach (var entry in group.progressBars)
            {
                var progressBars = FindElements<ProgressBarCustom>(targetContainer, entry.elementName);
                foreach (var progressBar in progressBars)
                {
                    progressBar.min = entry.min;
                    progressBar.max = entry.max;
                    progressBar.angle = entry.angle;
                    progressBar.value = entry.value;
                }
            }


        }

        Debug.Log("ApplyTextToUI done (TextArea / Label / TabButton / TextField / ProgressBarCustom).");
    }

    /// <summary>
    /// Tìm element trong container theo type + name; nếu name rỗng thì lấy element đầu tiên kiểu đó.
    /// </summary>
    // Lấy tất cả element có name (nếu có), hoặc tất cả T trong container
    private List<T> FindElements<T>(VisualElement container, string elementName) where T : VisualElement
    {
        var query = container.Query<T>().ToList();

        if (!string.IsNullOrEmpty(elementName) &&
            !elementName.StartsWith("(no-name"))
        {
            return query.Where(e => e.name == elementName).ToList();
        }

        return query;
    }

    // Trường hợp vẫn muốn lấy 1 cái đầu tiên
    private T FindFirstElement<T>(VisualElement container, string elementName) where T : VisualElement
    {
        var list = FindElements<T>(container, elementName);
        return list.Count > 0 ? list[0] : null;
    }



    // UNITY HOOKS 

    private void OnEnable()
    {
        // // mỗi lần enable (Editor hoặc Play) → cố gắng apply text
        if (pages != null && pages.Count > 0)
        {
            //ApplyTextToUI();
        }
    }

    //  HELPER 

    // // Build path debug: root/page/.../label
    private string BuildElementPath(VisualElement element)
    {
        if (element == null)
            return string.Empty;

        var stack = new Stack<string>();
        var current = element;

        while (current != null)
        {
            string part = string.IsNullOrEmpty(current.name) ? current.GetType().Name : current.name;
            stack.Push(part);
            current = current.parent;
        }

        return string.Join("/", stack);
    }
}
