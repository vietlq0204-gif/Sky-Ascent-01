using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class UxmlTextSyncWindow : EditorWindow
{
    private PageManager _pageManager;

    [MenuItem("Tools/UI/UXML Text Sync")]
    public static void ShowWindow()
    {
        var window = GetWindow<UxmlTextSyncWindow>("UXML Text Sync");
        window.minSize = new Vector2(400, 120);

    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("UXML Text Sync", EditorStyles.boldLabel);

        _pageManager = (PageManager)EditorGUILayout.ObjectField(
            "PageTextManager",
            _pageManager,
            typeof(PageManager),
            true);

        if (_pageManager == null)
        {
            EditorGUILayout.HelpBox("Kéo object có PageTextManager vào đây.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan lại UI (page_Base + popup_Base)"))
        {
            _pageManager.Scan();
            EditorUtility.SetDirty(_pageManager);
        }

        using (new EditorGUI.DisabledScope(_pageManager.Pages == null || _pageManager.Pages.Count == 0))
        {
            if (GUILayout.Button("Ghi text vào asset.uxml"))
            {
                WriteToUxml(_pageManager);
                _pageManager.Scan();
            }
        }
    }

    private void WriteToUxml(PageManager manager)
    {
        var uiDoc = manager.UiDocument;
        if (uiDoc == null)
        {
            Debug.LogError("UxmlTextSyncWindow: PageTextManager.UiDocument chưa gán.");
            return;
        }

        var vta = uiDoc.visualTreeAsset;
        if (vta == null)
        {
            Debug.LogError("UIDocument không có VisualTreeAsset.");
            return;
        }

        var path = AssetDatabase.GetAssetPath(vta);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError($"Không tìm thấy file UXML: {path}");
            return;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Lỗi đọc UXML: {ex.Message}");
            return;
        }

        // ============================
        // 1) Gom tất cả entry text-based (text)
        // ============================
        var allTextEntries = manager.Pages
            .SelectMany(g => g.textAreas
                              .Concat(g.labels)
                              .Concat(g.tabButtons))
            .Where(e => !string.IsNullOrEmpty(e.elementName)
                     && !e.elementName.StartsWith("(no-name"))
            .ToList();

        // Group theo elementName
        var groupedByName = allTextEntries
            .GroupBy(e => e.elementName);

        foreach (var group in groupedByName)
        {
            string elementName = group.Key;

            // Lấy tất cả node UXML có name = elementName
            var nodes = doc
                .Descendants()
                .Where(x => (string)x.Attribute("name") == elementName)
                .ToList();

            if (nodes.Count == 0)
                continue;

            // Giả định text hiện tại trong file là giống nhau cho tất cả node
            string docText = (string)nodes[0].Attribute("text") ?? string.Empty;

            // Entry nào khác với UXML -> coi như bị chỉnh sửa
            var changedEntries = group
                .Where(e => (e.text ?? string.Empty) != docText)
                .ToList();

            if (changedEntries.Count == 0)
                continue; // không có thay đổi nào cho name này

            // Chọn entry cuối cùng bị sửa làm source
            var source = changedEntries.Last();
            string newText = source.text ?? string.Empty;

            // Ghi vào TẤT CẢ node cùng name
            foreach (var n in nodes)
                n.SetAttributeValue("text", newText);
        }

        // ============================
        // 2) TextField: label + value
        // ============================
        WriteTextFieldToUxml(manager, doc);

        // ============================
        // 3) ProgressBarCustom: min + max + value + angle
        // ============================
        WriteProgressBarToUxml(manager, doc);

        try
        {
            doc.Save(path);
            AssetDatabase.Refresh();
            Debug.Log("UXML đã được update text thành công.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Lỗi ghi UXML: {ex.Message}");
        }
    }

    private void WriteTextFieldToUxml(PageManager manager, XDocument doc)
    {
        var allTF = manager.Pages
            .SelectMany(g => g.textFields)
            .Where(e => !string.IsNullOrEmpty(e.elementName)
                     && !e.elementName.StartsWith("(no-name"))
            .ToList();

        var groups = allTF.GroupBy(e => e.elementName);

        foreach (var group in groups)
        {
            string elementName = group.Key;

            var nodes = doc
                .Descendants()
                .Where(x => (string)x.Attribute("name") == elementName)
                .ToList();

            if (nodes.Count == 0)
                continue;

            // ===== LABEL =====
            string docLabel = (string)nodes[0].Attribute("label") ?? string.Empty;
            var changedLabelEntries = group
                .Where(e => (e.labelText ?? string.Empty) != docLabel)
                .ToList();

            if (changedLabelEntries.Count > 0)
            {
                var src = changedLabelEntries.Last();
                string newLabel = src.labelText ?? string.Empty;
                foreach (var n in nodes)
                    n.SetAttributeValue("label", newLabel);
            }

            // ===== VALUE =====
            string docValue = (string)nodes[0].Attribute("value") ?? string.Empty;
            var changedValueEntries = group
                .Where(e => (e.valueText ?? string.Empty) != docValue)
                .ToList();

            if (changedValueEntries.Count > 0)
            {
                var src = changedValueEntries.Last();
                string newValue = src.valueText ?? string.Empty;
                foreach (var n in nodes)
                    n.SetAttributeValue("value", newValue);
            }
        }
    }

    private void WriteProgressBarToUxml(PageManager manager, XDocument doc)
    {
        var allProgressBars = manager.Pages
            .SelectMany(g => g.progressBars)
            .Where(e => !string.IsNullOrEmpty(e.elementName)
                     && !e.elementName.StartsWith("(no-name"))
            .ToList();

        var groups = allProgressBars.GroupBy(e => e.elementName);

        foreach (var group in groups)
        {
            string elementName = group.Key;

            var nodes = doc
                .Descendants()
                .Where(x => (string)x.Attribute("name") == elementName)
                .ToList();

            if (nodes.Count == 0)
                continue;

            var source = group.Last();

            foreach (var node in nodes)
            {
                node.SetAttributeValue("value", source.value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                node.SetAttributeValue("min", source.min.ToString(System.Globalization.CultureInfo.InvariantCulture));
                node.SetAttributeValue("max", source.max.ToString(System.Globalization.CultureInfo.InvariantCulture));
                node.SetAttributeValue("angle", source.angle.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }


    /// <summary>
    /// Tìm node có name = elementName và cập nhật attribute text
    /// </summary>
    /// <summary>
    /// Cập nhật attribute (text / value / label) cho TẤT CẢ element có cùng name
    /// </summary>
    private void WriteNode(XDocument doc, string elementName, string newText, string attrName = "text")
    {
        if (string.IsNullOrEmpty(elementName) ||
            elementName.StartsWith("(no-name"))
            return;

        // Lấy TẤT CẢ node có name khớp
        var elems = doc
            .Descendants()
            .Where(e => (string)e.Attribute("name") == elementName)
            .ToList();

        if (elems.Count == 0)
            return;

        foreach (var elem in elems)
        {
            elem.SetAttributeValue(attrName, newText ?? string.Empty);
        }
    }


}

// Hành vi & giới hạn

//| Mục                     | Mô tả 
//| -------------------------------------------------------------------------------------------------------------------- 
//| Cách tìm node           | Theo `name` trong UXML; giả định mỗi name là unique.                                              |
//| Loại element hỗ trợ     | Bất cứ element nào có attr `name="..."` và attr `text` (Label, TextArea custom…).                 |
//| Ảnh hưởng               | Sửa trực tiếp file `.uxml` → UI Builder & Inspector đều thấy text mới.                            |
//| Không làm               | Không đụng tới USS, không sửa class, không đổi hierarchy.                                         |
//| Rủi ro                  | Nếu Ní đổi name trong UXML sau khi đã có entry → tool sẽ không tìm thấy node, entry đó bị bỏ qua. |
