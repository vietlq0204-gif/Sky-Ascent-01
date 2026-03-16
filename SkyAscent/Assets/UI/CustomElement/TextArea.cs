using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class TextArea : VisualElement
{
    private Label _label;

    [UxmlAttribute("text")]
    public string text
    {
        get => _label.text;
        set => _label.text = value;
    }

    [UxmlAttribute("align")]
    public TextAnchor align { get; set; } = TextAnchor.MiddleCenter;

    public TextArea()
    {
        // label chứa nội dung
        _label = new Label();
        _label.style.whiteSpace = WhiteSpace.Normal;  // // Cho phép xuống dòng
        _label.style.flexWrap = Wrap.Wrap;            // // Cho phép wrap
        _label.style.unityTextAlign = TextAnchor.UpperLeft;

        // quan trọng để auto-grow chiều cao
        _label.style.flexShrink = 0;
        _label.style.flexGrow = 1;

        // width 100% theo cha
        _label.style.width = Length.Percent(100);

        Add(_label);

        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // auto resize theo nội dung
        _label.style.height = StyleKeyword.Auto;
        style.height = StyleKeyword.Auto;

        // apply align
        _label.style.unityTextAlign = align;
    }


    //  PUBLIC API -----------

    // Gán text mới
    public void SetText(string value)
    {
        text = value;          // // dùng property, tránh đụng trực tiếp _label
    }

    // Thêm text (nối vào cuối)
    public void AppendText(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        text += value;
    }

    // Xoá toàn bộ nội dung
    public void ClearValue()
    {
        text = string.Empty;
    }
}
