using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Khi thêm UpdateUI thì Unity auto thêm DLEventListener nếu chưa có
[RequireComponent(typeof(DLEventListener))]
[ExecuteAlways] // chạy cả EditMode và PlayMode
public class UpdateUI : MonoBehaviour
{
    public enum VariableUIValue
    {
        TMP_Text,
        Button,
        Slider,
        Image,
        Toggle
    }

    [SerializeField] private VariableUIValue variableType;
    [SerializeField] private bool InChild = true;
    [SerializeField] private Component targetComponent;

    private void Awake()
    {
        if (targetComponent == null)
        {
            Refresh();
        }
    }

    // Hàm để editor gọi
    public void Refresh()
    {
        switch (variableType)
        {
            case VariableUIValue.TMP_Text:
                targetComponent = InChild ? GetComponentInChildren<TMP_Text>() : GetComponent<TMP_Text>();
                break;
            case VariableUIValue.Button:
                targetComponent = InChild ? GetComponentInChildren<Button>() : GetComponent<Button>();
                break;
            case VariableUIValue.Slider:
                targetComponent = InChild ? GetComponentInChildren<Slider>() : GetComponent<Slider>();
                break;
            case VariableUIValue.Image:
                targetComponent = InChild ? GetComponentInChildren<Image>() : GetComponent<Image>();
                break;
            case VariableUIValue.Toggle:
                targetComponent = InChild ? GetComponentInChildren<Toggle>() : GetComponent<Toggle>();
                break;
        }
    }

    public void UpdateValue(int newValue)
    {
        if (targetComponent == null)
        {
            Debug.LogWarning($"[{name}] Chưa gán hoặc chưa Refresh {variableType}");
            return;
        }

        switch (variableType)
        {
            case VariableUIValue.TMP_Text:
                ((TMP_Text)targetComponent).text = newValue.ToString();
                break;
            case VariableUIValue.Slider:
                ((Slider)targetComponent).value = newValue;
                break;
            case VariableUIValue.Toggle:
                ((Toggle)targetComponent).isOn = newValue > 0;
                break;
                // Button, Image: xử lý riêng nếu cần
        }
    }
}
