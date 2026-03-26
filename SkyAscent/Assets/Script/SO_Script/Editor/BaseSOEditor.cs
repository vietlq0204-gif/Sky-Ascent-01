using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BaseSO))]
public class BaseSOEditor : AutoEditor<BaseSO>
{
    //BaseSO baseSO;

    //protected override void OnEnable()
    //{
    //    base.OnEnable();
    //    baseSO = (BaseSO)target;
    //}

    //private void OnValidate()
    //{
    //    if (!Application.isPlaying)
    //    {
    //        if (string.IsNullOrEmpty(baseSO._name))
    //            baseSO._name = $"{GetType().Name}_AUTO";

    //        if (string.IsNullOrEmpty(baseSO.description))
    //            baseSO.description = $"[{baseSO._name}] chưa có mô tả.";

    //        //UbilityHelperUnityEditor.ApplyPresetNameIO(this, baseSO._name);
    //    }
    //}
}


