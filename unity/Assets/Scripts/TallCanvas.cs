// A canvas laid out for a phone held sideways (844 × 390) that also works held upright (ludo is played that way):
// on a tall screen it is measured against 430 × 844 instead, so its buttons and text keep their size.
using UnityEngine;
using UnityEngine.UI;

public class TallCanvas : MonoBehaviour
{
    public static readonly Vector2 Wide = new Vector2(844, 390), Tall = new Vector2(430, 844);
    public static bool Upright => Screen.height > Screen.width;
    CanvasScaler scaler;

    void Update()
    {
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        if (scaler != null) scaler.referenceResolution = Upright ? Tall : Wide;
    }
}
