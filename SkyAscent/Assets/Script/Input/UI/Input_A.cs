using UnityEngine;
using UnityEngine.UI;

public class Input_A : MonoBehaviour
{
    public void Play()
    {
        CoreEvents.OnUIPress.Raise(new OnUIPressEvent(UIPress.Tab_Play));
    }

}
