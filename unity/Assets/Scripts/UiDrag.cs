// Drag something on the screen (a domino from your hand): it follows your finger and, when let go outside
// `home` (the hand), reports where. A plain tap still works as a tap (Unity doesn't click after a drag).
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UiDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action<Vector2> onDrop;
    public RectTransform home;
    RectTransform rt;
    Vector3 startPos, grab;
    int startIndex;

    void Awake() { rt = (RectTransform)transform; }

    public void OnBeginDrag(PointerEventData e)
    {
        startPos = rt.position; startIndex = rt.GetSiblingIndex();
        grab = rt.position - (Vector3)e.position;
        rt.SetAsLastSibling();
        rt.localScale = Vector3.one * 1.18f;
        Sfx.Tap();
    }

    public void OnDrag(PointerEventData e) { if (rt != null) rt.position = (Vector3)e.position + grab * 0.4f; }

    public void OnEndDrag(PointerEventData e)
    {
        if (rt == null) return;
        rt.position = startPos; rt.localScale = Vector3.one; rt.SetSiblingIndex(startIndex);
        if (home != null && RectTransformUtility.RectangleContainsScreenPoint(home, e.position, null)) return;   // put back
        onDrop?.Invoke(e.position);
    }
}
