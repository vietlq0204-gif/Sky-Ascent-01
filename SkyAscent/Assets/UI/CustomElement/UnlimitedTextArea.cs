using System.Text;                   // // dùng StringBuilder
using UnityEngine.UIElements;

[UxmlElement]                         // // cho phép dùng trong UXML
public partial class UnlimitedTextArea : VisualElement
{
    private readonly TextField _field;
    private bool _updatingInternally; // // tránh loop khi set value lại

    // // Text “thật” (đã bỏ ký tự mềm)
    [UxmlAttribute("text")]
    public string text
    {
        get => GetRawText();          // // luôn trả về text sạch
        set => SetRawText(value);     // // gán text sạch
    }

    // // Khoảng bao nhiêu ký tự thì chèn 1 breakpoint mềm (zero-width space)
    [UxmlAttribute("soft-break-interval")]
    public int softBreakInterval { get; set; } = 16;

    // // Không giới hạn chiều dài: maxLength = 0
    [UxmlAttribute("max-length")]
    public int maxLength
    {
        get => _field.maxLength;
        set => _field.maxLength = value; // // 0 = unlimited
    }

    public UnlimitedTextArea()
    {
        style.flexDirection = FlexDirection.Column;
        style.height = StyleKeyword.Auto; // // auto cao theo nội dung

        // // Ô nhập nội bộ
        _field = new TextField
        {
            multiline = true,         // // cho nhiều dòng
        };

        // // style để tự wrap + auto cao
        _field.style.whiteSpace = WhiteSpace.Normal; // // cho phép wrap
        _field.style.flexGrow = 1;
        _field.style.flexShrink = 0;
        _field.style.width = Length.Percent(100);
        _field.style.height = StyleKeyword.Auto;
        _field.style.overflow = Overflow.Visible;  // // đừng cắt text
        _field.maxLength = 0;                 // // không giới hạn

        Add(_field);

        // // Khi text đổi → xử lý chèn soft-break
        _field.RegisterValueChangedCallback(OnValueChanged);

        RegisterCallback<GeometryChangedEvent>(_ =>
        {
            // // đảm bảo container auto fit nội dung
            style.height = StyleKeyword.Auto;
            _field.style.height = StyleKeyword.Auto;
        });
    }

    // // Sự kiện khi user gõ / value đổi
    private void OnValueChanged(ChangeEvent<string> evt)
    {
        if (_updatingInternally)
            return;

        _updatingInternally = true;

        string raw = RemoveSoftBreaks(evt.newValue);                 // // bỏ break cũ
        string soft = InsertSoftBreaks(raw, softBreakInterval);       // // chèn lại theo rule

        _field.SetValueWithoutNotify(soft);                           // // gán lại không bắn event

        _updatingInternally = false;
    }

    // // Lấy text sạch (không có ký tự zero-width)
    public string GetRawText()
    {
        return RemoveSoftBreaks(_field.value);
    }

    // // Gán text sạch từ code
    public void SetRawText(string raw)
    {
        _updatingInternally = true;

        string soft = InsertSoftBreaks(raw ?? string.Empty, softBreakInterval);
        _field.SetValueWithoutNotify(soft);

        _updatingInternally = false;
    }

    // // Bỏ tất cả ký tự break mềm
    private string RemoveSoftBreaks(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        const char Zws = '\u200B'; // // zero-width space
        return s.Replace(Zws.ToString(), string.Empty);
    }

    // // Chèn zero-width space sau mỗi N ký tự “dính” (không có khoảng trắng)
    private string InsertSoftBreaks(string s, int interval)
    {
        if (string.IsNullOrEmpty(s) || interval <= 0)
            return s;

        const char Zws = '\u200B';

        var sb = new StringBuilder(s.Length + s.Length / interval);
        int runLength = 0; // // độ dài đoạn không có whitespace

        foreach (char c in s)
        {
            sb.Append(c);

            if (char.IsWhiteSpace(c))
            {
                runLength = 0;        // // reset khi gặp khoảng trắng
            }
            else
            {
                runLength++;
                if (runLength >= interval)
                {
                    sb.Append(Zws);   // // chèn điểm bẻ dòng mềm
                    runLength = 0;
                }
            }
        }

        return sb.ToString();
    }
}
