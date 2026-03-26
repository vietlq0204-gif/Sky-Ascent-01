using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(menuName = "Strategy/Popup/New Popup Element Strategy SO")]
public class PopupElementStrategySO : BaseSO
{
    public PopupType popupType;
    public string Title;
    
    public ButtonPopupSO[] ButtonsMain;
    public ButtonPopupSO[] ButtonsSup;
    public ImagePopupSO[] ButtonsImage;

    public DescriptionPopupSO[] Descriptions;
    public InputFieldPopupSO[] InputFields;
}

[Serializable]
public class DescriptionPopupSO
{
    public int order;
    [TextArea]
    public string value;
}

[Serializable]
public class ButtonPopupSO
{
    public int order;
    public string NameElement;
    public string title;
}

[Serializable]
public class ImagePopupSO
{
    public int order;
    public string NameElement;
    public string title;
    public PopupImageAssetType imageType;
    public Texture2D texture;
    public RenderTexture renderTexture;
    public Sprite sprite;
    public VectorImage vectorImage;

    public bool TryGetBackground(out Background background)
    {
        background = default;

        switch (imageType)
        {
            case PopupImageAssetType.Texture:
                if (texture == null) return false;
                background = new Background { texture = texture };
                return true;

            case PopupImageAssetType.RenderTexture:
                if (renderTexture == null) return false;
                background = new Background { renderTexture = renderTexture };
                return true;

            case PopupImageAssetType.Sprite:
                if (sprite == null) return false;
                background = new Background { sprite = sprite };
                return true;

            case PopupImageAssetType.VectorImage:
                if (vectorImage == null) return false;
                background = new Background { vectorImage = vectorImage };
                return true;

            default:
                return false;
        }
    }
}

public enum PopupImageAssetType
{
    Texture,
    RenderTexture,
    Sprite,
    VectorImage,
}

[Serializable]
public class InputFieldPopupSO
{
    public int order;
    public string title;
    public string placeholder;
    public string value;
}
