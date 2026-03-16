
using UnityEngine;
using UnityEngine.UIElements;

// // Đăng ký cho UXML bằng attribute
[UxmlElement]
public partial class HorizontalLayoutCenter : VisualElement
{
    // // Cho phép set spacing trực tiếp trong UXML: <HorizontalLayoutCenter spacing="8" />
    [UxmlAttribute("spacing")]
    public float spacing { get; set; } = 4f;

    public HorizontalLayoutCenter()
    {
        // // Lắng nghe thay đổi geometry để re-layout
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        DoCustomLayout();
    }

    private void DoCustomLayout()
    {
        // // Lấy rect thật của parent
        Rect rect = contentRect;
        float parentWidth = rect.width;
        float parentHeight = rect.height;

        if (parentWidth <= 0 || parentHeight <= 0)
            return;

        // // 1) Tính tổng width children
        float totalChildrenWidth = 0f;

        for (int i = 0; i < childCount; i++)
        {
            var child = this[i];
            float w = child.resolvedStyle.width;

            if (float.IsNaN(w) || w <= 0)
                continue; // // bỏ qua child chưa có width

            totalChildrenWidth += w;
            if (i < childCount - 1)
                totalChildrenWidth += spacing;
        }

        if (totalChildrenWidth <= 0)
            return;

        // // 2) Tính startX từ tâm
        float startX = (parentWidth - totalChildrenWidth) * 0.5f;
        float currentX = startX;

        // // 3) Đặt từng child
        for (int i = 0; i < childCount; i++)
        {
            var child = this[i];
            float w = child.resolvedStyle.width;
            float h = child.resolvedStyle.height;

            if (float.IsNaN(w) || w <= 0)
                continue;

            // // ép absolute để tự layout
            child.style.position = Position.Absolute;

            // // ngang: dãn từ giữa ra
            child.style.left = currentX;

            // // dọc: căn giữa parent
            if (float.IsNaN(h) || h <= 0)
            {
                // // fallback: nếu height child chưa resolve, coi như bằng parent
                h = parentHeight;
            }

            float top = (parentHeight - h) * 0.5f;
            child.style.top = top;

            currentX += w + spacing;
        }
    }

}
