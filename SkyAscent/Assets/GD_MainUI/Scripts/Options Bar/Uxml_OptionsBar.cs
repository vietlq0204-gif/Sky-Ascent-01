using UnityEngine;
using UnityEngine.UIElements;
namespace GD_MainUI.Scripts.Options_Bar
{
    public class Uxml_OptionsBar : MonoBehaviour
    {
        [Header("Setup")]
        public UIDocument mainDocument; // Kéo UIDocument chính vào đây
        public VisualTreeAsset settingsTemplate; // Kéo file SettingsPopup.uxml vào đây

        private VisualElement root;

        void OnEnable()
        {
            root = mainDocument.rootVisualElement;

            // 1. Tìm nút Setting ở màn hình chính
            var btnSettings = root.Q<Button>("options-bar__pause-button");

            // 2. Bắt sự kiện bấm nút
            if (btnSettings != null)
            {
                btnSettings.clicked += OpenSettings;
            }
        }

        void OpenSettings()
        {
            var safeAreaContainer = root.Q<VisualElement>("safe-area");

            if (safeAreaContainer == null)
            {
                Debug.LogError("Không tìm thấy cái hộp nào tên là 'safe-area' cả!");
                return;
            }
            
            // 3. "Đẻ" ra giao diện Settings từ file UXML
            VisualElement settingsUI = settingsTemplate.Instantiate();

            // 4. (Quan trọng) Setup cho cái Settings vừa đẻ ra
            // Vì Instantiate() trả về một TemplateContainer, nó chưa full màn hình đâu.
            settingsUI.style.flexGrow = 1; 
            settingsUI.style.width = Length.Percent(100);
            settingsUI.style.height = Length.Percent(100);
        
            // Add new Settings VE to safe-area
            safeAreaContainer.Add(settingsUI);
            
            // --- Xử lý nút tắt Settings (ngay bên trong cái vừa tạo) ---
            var btnClose = settingsUI.Q<Button>("settings__screen--close-button");
            if (btnClose != null)
            {
                // Khi bấm Close thì tự hủy chính cái UI này đi
                btnClose.clicked += () => root.Remove(settingsUI);
            }

            // 5. Gắn nó vào Root (Lúc này nó mới hiện lên màn hình)
            root.Add(settingsUI);
        }
    }
}
