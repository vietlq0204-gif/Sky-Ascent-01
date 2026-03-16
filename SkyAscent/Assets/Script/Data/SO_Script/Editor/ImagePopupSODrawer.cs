using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ImagePopupSO))]
public class ImagePopupSODrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        position.height = EditorGUIUtility.singleLineHeight;
        property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        DrawNext(ref position, property.FindPropertyRelative("order"));
        DrawNext(ref position, property.FindPropertyRelative("NameElement"));
        DrawNext(ref position, property.FindPropertyRelative("title"));

        SerializedProperty imageTypeProp = property.FindPropertyRelative("imageType");
        DrawNext(ref position, imageTypeProp);

        PopupImageAssetType imageType = (PopupImageAssetType)imageTypeProp.enumValueIndex;
        switch (imageType)
        {
            case PopupImageAssetType.Texture:
                DrawNext(ref position, property.FindPropertyRelative("texture"), "Image");
                break;
            case PopupImageAssetType.RenderTexture:
                DrawNext(ref position, property.FindPropertyRelative("renderTexture"), "Image");
                break;
            case PopupImageAssetType.Sprite:
                DrawNext(ref position, property.FindPropertyRelative("sprite"), "Image");
                break;
            case PopupImageAssetType.VectorImage:
                DrawNext(ref position, property.FindPropertyRelative("vectorImage"), "Image");
                break;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        const int lineCount = 5;
        return (EditorGUIUtility.singleLineHeight * lineCount) + (VerticalSpacing * (lineCount - 1));
    }

    private static void DrawNext(ref Rect position, SerializedProperty property, string labelOverride = null)
    {
        if (property == null)
            return;

        GUIContent label = labelOverride == null
            ? new GUIContent(property.displayName)
            : new GUIContent(labelOverride);

        EditorGUI.PropertyField(position, property, label, true);
        position.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
    }
}
