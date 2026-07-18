using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Shared window-chrome helpers used by every TR panel mod. This file is compiled into each plugin
// DLL (each mod lists it in build_all_mods.ps1), so all panels share identical drag / collapse /
// resize / focus behavior. Namespace TRShared keeps it out of each mod's own namespace.
namespace TRShared
{
    public static class WindowLayerUtil
    {
        public static void BringToFront(Component component)
        {
            if (component == null) return;

            Canvas rootCanvas = component.GetComponentInParent<Canvas>();
            if (rootCanvas != null && rootCanvas.isRootCanvas)
            {
                rootCanvas.overrideSorting = true;
                rootCanvas.sortingOrder = 1000 + (int)((DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond) % 100000);
            }

            RectTransform rect = component.GetComponent<RectTransform>();
            if (rect != null) rect.SetAsLastSibling();
        }
    }

    public class WindowPointerFocus : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
        }
    }

    // Moves a panel by dragging. Used by TRStats and TRAutoloader headers.
    public class WindowDragger : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform TargetRect;
        public void OnPointerDown(PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
        }
        public void OnDrag(PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
            if (TargetRect != null) TargetRect.anchoredPosition += data.delta;
        }
    }

    // TRBar's drag helper; identical behavior to WindowDragger but with a TargetMover field name.
    public class WindowDestroyer : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform TargetMover;
        public void OnPointerDown(PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
        }
        public void OnDrag(PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
            if (TargetMover != null) TargetMover.anchoredPosition += data.delta;
        }
    }

    public class ResizeHandler : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform PanelRect;
        public Vector2 MinSize = new Vector2(200, 150);
        public Vector2 MaxSize = new Vector2(800, 800);

        public void OnPointerDown(PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
        }

        public void OnDrag(PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
            if (PanelRect == null) return;
            Vector2 size = PanelRect.sizeDelta;
            size.x += data.delta.x;
            size.y -= data.delta.y;
            size.x = Mathf.Clamp(size.x, MinSize.x, MaxSize.x);
            size.y = Mathf.Clamp(size.y, MinSize.y, MaxSize.y);
            PanelRect.sizeDelta = size;
        }
    }

    public class CollapseHandler : MonoBehaviour
    {
        public RectTransform PanelRect;
        public GameObject ContentObj;
        public float ExpandedHeight;
        public float CollapsedHeight;
        public bool IsCollapsed = false;
        public Text Label;

        public void OnToggle()
        {
            IsCollapsed = !IsCollapsed;
            if (PanelRect != null)
            {
                PanelRect.sizeDelta = new Vector2(PanelRect.sizeDelta.x, IsCollapsed ? CollapsedHeight : ExpandedHeight);
            }
            if (ContentObj != null)
            {
                ContentObj.SetActive(!IsCollapsed);
            }
            if (Label != null) Label.text = IsCollapsed ? "+" : "-";
        }
    }
}
