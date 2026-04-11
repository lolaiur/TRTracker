using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TRBarrels
{
    [BepInPlugin("com.lolaiur.trbarrels", "Tavern Barrels", "1.3.0")]
    public class TRBarrelsPlugin : BaseUnityPlugin
    {
        public static TRBarrelsPlugin Instance;
        public static GameObject UI_OBJ;

        void Awake()
        {
             Instance = this;
             var loadMsg = new GameObject("LoadMsg").AddComponent<LoadStatusUI>();
             loadMsg.ModName = "TRBarrels";
             DontDestroyOnLoad(loadMsg.gameObject);
             SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateUI();
        }
        
        private bool _showUI = true;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) {
                if (UI_OBJ != null) {
                    _showUI = !_showUI;
                    UI_OBJ.SetActive(_showUI);
                }
            }
        }
        
        void CreateUI()
        {
            if (UI_OBJ != null) return;
            try
            {
                if (FindObjectOfType<EventSystem>() == null)
                {
                    GameObject es = new GameObject("TR_EventSystem");
                    es.AddComponent<EventSystem>();
                    es.AddComponent<StandaloneInputModule>();
                    DontDestroyOnLoad(es);
                }

                UI_OBJ = new GameObject("TRBarrelsCanvas");
                DontDestroyOnLoad(UI_OBJ);
                
                Canvas c = UI_OBJ.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 101;
                CanvasScaler cs = UI_OBJ.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                UI_OBJ.AddComponent<GraphicRaycaster>();
                
                // === MAIN PANEL ===
                GameObject panel = new GameObject("BarrelsPanel");
                panel.transform.SetParent(UI_OBJ.transform, false);
                Image border = panel.AddComponent<Image>();
                border.color = new Color(0.6f, 0.4f, 0.2f); 
                border.raycastTarget = false;
                RectTransform panelRT = panel.GetComponent<RectTransform>();
                panelRT.anchorMin = new Vector2(0, 1); panelRT.anchorMax = new Vector2(0, 1);
                panelRT.pivot = new Vector2(0, 1); 
                panelRT.anchoredPosition = new Vector2(20, -560);
                panelRT.sizeDelta = new Vector2(360, 400); // Standardized to Match TRTracker

                // Background
                GameObject bg = new GameObject("InnerBg");
                bg.transform.SetParent(panel.transform, false);
                Image bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0.15f, 0.1f, 0.05f, 0.98f); 
                bgImg.raycastTarget = true;
                bg.AddComponent<WindowPointerFocus>();
                RectTransform bgRT = bg.GetComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = new Vector2(2, 2); bgRT.offsetMax = new Vector2(-2, -2);

                // === HEADER ===
                GameObject header = new GameObject("Header");
                header.transform.SetParent(bg.transform, false);
                Image hImg = header.AddComponent<Image>();
                hImg.color = new Color(0.25f, 0.15f, 0.05f, 1f); 
                RectTransform headerRT = header.GetComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0, 1); headerRT.anchorMax = new Vector2(1, 1);
                headerRT.pivot = new Vector2(0, 1);
                headerRT.anchoredPosition = new Vector2(0, 0);
                headerRT.sizeDelta = new Vector2(0, 35); 

                // Title
                GameObject hTitle = new GameObject("Title");
                hTitle.transform.SetParent(header.transform, false);
                Text ht = hTitle.AddComponent<Text>();
                ht.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                ht.text = "AGING STATS 1.3.0 (F2)";
                ht.alignment = TextAnchor.MiddleCenter;
                ht.color = new Color(1f, 0.8f, 0.4f);
                ht.fontSize = 14;
                ht.raycastTarget = false;
                RectTransform htRT = hTitle.GetComponent<RectTransform>();
                if (htRT == null) htRT = hTitle.AddComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero;
                htRT.anchorMax = Vector2.one;
                htRT.sizeDelta = Vector2.zero;
                htRT.offsetMin = Vector2.zero;
                htRT.offsetMax = Vector2.zero;

                WindowDestroyer drag = header.AddComponent<WindowDestroyer>(); 
                drag.TargetMover = panelRT;

                // === BODY CONTAINER ===
                GameObject body = new GameObject("Body");
                body.transform.SetParent(bg.transform, false);
                RectTransform bodyRT = body.AddComponent<RectTransform>();
                bodyRT.anchorMin = Vector2.zero; bodyRT.anchorMax = Vector2.one;
                bodyRT.offsetMin = Vector2.zero; bodyRT.offsetMax = new Vector2(0, -35); // Ends at bottom of header

                // === SCROLL VIEW ===
                GameObject scrollObj = new GameObject("Scroll View");
                scrollObj.transform.SetParent(body.transform, false); // Child of Body
                RectTransform scrollRT = scrollObj.AddComponent<RectTransform>();
                scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
                scrollRT.offsetMin = new Vector2(5, 5); 
                scrollRT.offsetMax = new Vector2(-15, -5); // Fills inner body

                // --- COLLAPSE BUTTON ---
                GameObject btnObj = new GameObject("CollapseBtn");
                btnObj.transform.SetParent(header.transform, false); // Child of Header
                Image btnImg = btnObj.AddComponent<Image>();
                btnImg.color = Color.green;
                RectTransform btnRT = btnObj.GetComponent<RectTransform>();
                if (btnRT == null) btnRT = btnObj.AddComponent<RectTransform>();
                
                btnRT.anchorMin = new Vector2(1, 0.5f); btnRT.anchorMax = new Vector2(1, 0.5f);
                btnRT.pivot = new Vector2(1, 0.5f);
                btnRT.anchoredPosition = new Vector2(-5, 0);
                btnRT.sizeDelta = new Vector2(20, 20);
                
                Button btn = btnObj.AddComponent<Button>();
                CollapseHandler ch = btnObj.AddComponent<CollapseHandler>();
                ch.PanelRect = panelRT;
                ch.ContentObj = body; // Toggle the whole body (Labels + Scroll)
                ch.ExpandedHeight = 400; 
                ch.CollapsedHeight = 35; 
                btn.onClick.AddListener(ch.OnToggle);

                ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
                sr.horizontal = false;
                sr.vertical = true;
                sr.scrollSensitivity = 25f;
                sr.movementType = ScrollRect.MovementType.Elastic;
                sr.elasticity = 0.1f;
                sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                sr.viewport = null; 

                // Viewport
                GameObject viewport = new GameObject("Viewport");
                viewport.transform.SetParent(scrollObj.transform, false);
                viewport.AddComponent<WindowPointerFocus>();
                RectTransform viewRT = viewport.AddComponent<RectTransform>();
                viewRT.anchorMin = Vector2.zero; viewRT.anchorMax = Vector2.one;
                viewRT.sizeDelta = Vector2.zero;
                viewRT.offsetMin = Vector2.zero; viewRT.offsetMax = Vector2.zero;
                viewport.AddComponent<RectMask2D>();
                Image vImg = viewport.AddComponent<Image>();
                vImg.color = new Color(0, 0, 0, 0); // Transparent raycast target for scrolling
                vImg.raycastTarget = true;
                sr.viewport = viewRT;

                // Content
                GameObject content = new GameObject("Content");
                content.transform.SetParent(viewport.transform, false);
                RectTransform contentRT = content.AddComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
                contentRT.pivot = new Vector2(0, 1);
                contentRT.sizeDelta = new Vector2(0, 0);
                contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = Vector2.zero;
                sr.content = contentRT;

                // === SCROLLBAR ===
                GameObject scrollbarObj = new GameObject("Scrollbar Vertical");
                scrollbarObj.transform.SetParent(bg.transform, false);
                RectTransform sbRT = scrollbarObj.AddComponent<RectTransform>();
                sbRT.anchorMin = new Vector2(1, 0); sbRT.anchorMax = new Vector2(1, 1);
                sbRT.pivot = new Vector2(1, 1);
                sbRT.anchoredPosition = new Vector2(-5, -60); 
                sbRT.sizeDelta = new Vector2(15, -85);
                
                Image sbImg = scrollbarObj.AddComponent<Image>();
                sbImg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                sbImg.raycastTarget = false;
                
                Scrollbar sb = scrollbarObj.AddComponent<Scrollbar>();
                sb.direction = Scrollbar.Direction.BottomToTop;
                sr.verticalScrollbar = sb;

                // Sliding Area
                GameObject slidingArea = new GameObject("Sliding Area");
                slidingArea.transform.SetParent(scrollbarObj.transform, false);
                RectTransform slideRT = slidingArea.AddComponent<RectTransform>();
                slideRT.anchorMin = Vector2.zero; slideRT.anchorMax = Vector2.one;
                slideRT.sizeDelta = Vector2.zero;
                slideRT.offsetMin = Vector2.zero; slideRT.offsetMax = Vector2.zero;

                // Handle
                GameObject handle = new GameObject("Handle");
                handle.transform.SetParent(slidingArea.transform, false);
                RectTransform handleRT = handle.AddComponent<RectTransform>();
                handleRT.sizeDelta = Vector2.zero;
                handleRT.offsetMin = Vector2.zero; handleRT.offsetMax = Vector2.zero;
                
                Image handleImg = handle.AddComponent<Image>();
                handleImg.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                sb.handleRect = handleRT;
                sb.targetGraphic = handleImg;

                // --- RESIZE GRIP (Bottom-Right Triangle) ---
                GameObject grip = new GameObject("ResizeGrip");
                grip.transform.SetParent(bg.transform, false);
                RectTransform gripRT = grip.AddComponent<RectTransform>();
                gripRT.anchorMin = new Vector2(1, 0); gripRT.anchorMax = new Vector2(1, 0);
                gripRT.pivot = new Vector2(1, 0);
                gripRT.anchoredPosition = Vector2.zero;
                gripRT.sizeDelta = new Vector2(20, 20);

                Image gripImg = grip.AddComponent<Image>();
                gripImg.color = new Color(0.5f, 0.35f, 0.15f, 0.9f);

                GameObject tri = new GameObject("Triangle");
                tri.transform.SetParent(grip.transform, false);
                Text triText = tri.AddComponent<Text>();
                triText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                triText.text = "\u25E2";
                triText.fontSize = 16;
                triText.color = new Color(1f, 0.9f, 0.7f, 0.8f);
                triText.alignment = TextAnchor.MiddleCenter;
                RectTransform triRT = tri.GetComponent<RectTransform>();
                triRT.anchorMin = Vector2.zero; triRT.anchorMax = Vector2.one;
                triRT.sizeDelta = Vector2.zero;

                ResizeHandler rh = grip.AddComponent<ResizeHandler>();
                rh.PanelRect = panelRT;

                // === MANAGER ===
                BarrelManager mgr = panel.AddComponent<BarrelManager>();
                mgr.ContentRect = contentRT;
                mgr.TextName = CreateColumn(content.transform, "ColName", 10, 165, TextAnchor.UpperLeft);
                mgr.TextStage = CreateColumn(content.transform, "ColStage", 170, 235, TextAnchor.UpperCenter);
                mgr.TextTime = CreateColumn(content.transform, "ColTime", 240, 330, TextAnchor.UpperRight);
            }
            catch {}
        }
        
        void CreateHeaderText(Transform parent, string txt, float xMin, float xMax, TextAnchor align)
        {
            GameObject go = new GameObject("H_"+txt);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontStyle = FontStyle.Bold;
            t.fontSize = 14;
            t.color = new Color(1f, 0.8f, 0.4f);
            t.alignment = align; 
            t.text = txt; // Fixed missing text assignment
            
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(xMin, 0); // Removed Z offset
            rt.sizeDelta = new Vector2(xMax - xMin, 0); 
        }
        
        Text CreateColumn(Transform parent, string name, float xMin, float xMax, TextAnchor align)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.raycastTarget = false;
            Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            try { Font con = Font.CreateDynamicFontFromOSFont("Consolas", 14); if (con != null) f = con; } catch {}
            t.font = f;
            t.fontStyle = FontStyle.Normal;
            t.fontSize = 12;
            t.color = Color.white;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow; 
            
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); 
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(xMin, 0); // Removed Z offset
            rt.sizeDelta = new Vector2(xMax - xMin, 0); 
            
            ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return t;
        }
    }

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

    /// <summary>Handles window dragging via header.</summary>
    public class WindowDestroyer : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform TargetMover;
        public void OnPointerDown(PointerEventData data) {
            WindowLayerUtil.BringToFront(this);
        }
        public void OnDrag(PointerEventData data) {
            WindowLayerUtil.BringToFront(this);
            if (TargetMover) TargetMover.anchoredPosition += data.delta;
        }
    }

    /// <summary>Handles window resizing via bottom-right corner grip.</summary>
    public class ResizeHandler : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform PanelRect;
        public Vector2 MinSize = new Vector2(200, 150);
        public Vector2 MaxSize = new Vector2(800, 800);

        public void OnPointerDown(PointerEventData data) {
            WindowLayerUtil.BringToFront(this);
        }

        public void OnDrag(PointerEventData data) {
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

    public class BarrelManager : MonoBehaviour
    {
        public Text TextName;
        public Text TextStage;
        public Text TextTime;
        public RectTransform ContentRect;
        
        private float _scanInterval = 5f;
        private float _listUpdateInterval = 1f;
        private float _nextScanTime = 0f;
        private float _nextUpdateTime = 0f;
        private List<Component> cachedBarrels = new List<Component>();
        private Type _agingBarrelType;
        
        // Memory Optimization
        private System.Text.StringBuilder _sbName = new System.Text.StringBuilder();
        private System.Text.StringBuilder _sbStage = new System.Text.StringBuilder();
        private System.Text.StringBuilder _sbTime = new System.Text.StringBuilder();

        void Update()
        {
            try {
                if (!TRBarrelsPlugin.UI_OBJ || !TRBarrelsPlugin.UI_OBJ.activeInHierarchy) return;

                if (Time.unscaledTime >= _nextScanTime)
                {
                    _nextScanTime = Time.unscaledTime + _scanInterval;
                    ScanBarrels();
                }

                if (Time.unscaledTime >= _nextUpdateTime)
                {
                    _nextUpdateTime = Time.unscaledTime + _listUpdateInterval;
                    if (cachedBarrels.Count > 0) UpdateList();
                }
            } catch {}
        }

        void ScanBarrels()
        {
            try {
                if (_agingBarrelType == null)
                {
                    _agingBarrelType = Type.GetType("AgingBarrel, Assembly-CSharp");
                }

                cachedBarrels.Clear();
                if (_agingBarrelType == null) return;

                UnityEngine.Object[] all = FindObjectsOfType(_agingBarrelType);
                foreach(var m in all) {
                    Component component = m as Component;
                    if (component != null) cachedBarrels.Add(component);
                }
            } catch {}
        }
        
        class BarrelEntry {
             public string Name;
             public string Stage;
             public string Time;
             public bool IsEmpty;
             public double ProgressVal;
             public int StageVal; 
        }

        void UpdateList()
        {
            if (!TextName) return;
            try {
                List<BarrelEntry> entries = new List<BarrelEntry>();

                foreach (var b in cachedBarrels)
                {
                    if (b == null) continue;
                    if (!b.gameObject.activeInHierarchy) continue;
                    Behaviour behaviour = b as Behaviour;
                    if (behaviour != null && !behaviour.enabled) continue;

                    Renderer r = b.GetComponent<Renderer>();
                    if (r != null && !r.enabled) continue;

                    Type bType = b.GetType();
                    
                    FieldInfo slotsF = bType.GetField("inputSlot", BindingFlags.Public | BindingFlags.Instance);
                    if (slotsF == null) continue;
                    Array slots = (Array)slotsF.GetValue(b);
                    if (slots == null) continue;

                    for (int i = 0; i < slots.Length; i++)
                    {
                        BarrelEntry e = new BarrelEntry();
                        e.Name = "Unknown";
                        e.Stage = "---";
                        e.Time = "---";
                        e.IsEmpty = true;
                        e.ProgressVal = -1;
                        e.StageVal = -1;

                        try {
                            object slot = slots.GetValue(i);
                            if (slot == null) { entries.Add(e); continue; }
                            
                            int qty = 0;
                            try {
                                PropertyInfo stackP = slot.GetType().GetProperty("Stack", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                                if (stackP != null) qty = (int)stackP.GetValue(slot, null);
                            } catch {}

                            if (qty <= 0) {
                                e.Name = "<color=#888888>Empty</color>";
                                e.Stage = "<color=#888888>---</color>";
                                e.Time = "<color=#888888>---</color>";
                                e.IsEmpty = true;
                                entries.Add(e);
                                continue;
                            }
                            
                            e.IsEmpty = false;
                            
                            FieldInfo itemInstF = slot.GetType().GetField("itemInstance", BindingFlags.Public | BindingFlags.Instance);
                            object itemInst = (itemInstF != null) ? itemInstF.GetValue(slot) : null;
                            if (itemInst == null) { entries.Add(e); continue; }

                            // Name
                            string displayName = GetItemName(itemInst);
                            
                            if (displayName.Contains("-")) {
                                string[] parts = displayName.Split('-');
                                if (parts.Length > 1) displayName = parts[1].Trim();
                            }
                            displayName = displayName.Replace("(Food)", "").Replace("(Clone)", "").Trim();
                            e.Name = string.Format("{0} <size=11>(x{1})</size>", displayName, qty);

                            // Stage detection - scan int properties for stage value (1-4)
                            int stage = 0;
                            try {
                                Type itemType = itemInst.GetType();

                                // Scan int properties on itemInstance for stage value
                                foreach (PropertyInfo p in itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)) {
                                    if (p.PropertyType == typeof(int) && p.CanRead) {
                                        try {
                                            int val = (int)p.GetValue(itemInst, null);
                                            if (val >= 1 && val <= 4 && stage == 0) {
                                                stage = val;
                                                break;
                                            }
                                        } catch {}
                                    }
                                }
                            } catch {}
                            e.StageVal = stage;
                            
                            string stageStr = "Unaged";
                            if(stage==1) stageStr = "<color=blue>Young</color>";
                            if(stage==2) stageStr = "<color=green>Normal</color>";
                            if(stage==3) stageStr = "<color=purple>Reserve</color>";
                            if(stage>=4) stageStr = "<color=#FF4500>Grand R.</color>"; // Orange Red
                            e.Stage = stageStr;
                            
                            // Time
                            if (stage >= 4) {
                                e.Time = "<color=green>100.0%</color>";
                                e.ProgressVal = 101; 
                            } else {
                                FieldInfo timerF = bType.GetField("timer", BindingFlags.Public | BindingFlags.Instance);
                                Array timers = (Array)timerF.GetValue(b);
                                if (timers != null && timers.Length > i) {
                                    object t = timers.GetValue(i);
                                    if (t != null) {
                                        FieldInfo totalF = t.GetType().GetField("totalMinToFinish", BindingFlags.Public | BindingFlags.Instance);
                                        FieldInfo startF = t.GetType().GetField("dateStartedMin", BindingFlags.Public | BindingFlags.Instance);
                                        if (totalF != null && startF != null) {
                                            ulong total = (ulong)totalF.GetValue(t);
                                            ulong start = (ulong)startF.GetValue(t);
                                            ulong current = BarrelReflection.GetStaticValueByType<ulong>(Type.GetType("WorldTime, Assembly-CSharp"));
                                            
                                            if (total > 0) {
                                                double elapsed = (double)(current - start);
                                                double prog = (elapsed / (double)total) * 100.0;
                                                if (prog < 0) prog = 0;
                                                if (prog > 100) prog = 100;
                                                e.ProgressVal = prog;
                                                e.Time = prog.ToString("F1") + "%";
                                            } else {
                                                e.Time = "Wait";
                                            }
                                        }
                                    }
                                }
                            }
                            entries.Add(e);
                        }
                        catch {}
                    }
                }
                
                // Sort by Stage (Grand -> Empty). Then Progress.
                entries.Sort((a,b) => {
                    int r = b.StageVal.CompareTo(a.StageVal);
                    if (r != 0) return r;
                    r = b.ProgressVal.CompareTo(a.ProgressVal);
                    if (r != 0) return r;
                    return a.Name.CompareTo(b.Name);
                });
                
                _sbName.Clear();
                _sbStage.Clear();
                _sbTime.Clear();

                _sbName.AppendLine("<size=13><b>Product</b></size>");
                _sbStage.AppendLine("<size=13><b>Stage</b></size>");
                _sbTime.AppendLine("<size=13><b>Progress</b></size>");

                foreach(var e in entries) { 
                    _sbName.Append(e.Name).Append("\n");
                    _sbStage.Append(e.Stage).Append("\n");
                    _sbTime.Append(e.Time).Append("\n");
                }
                
                TextName.text = _sbName.ToString();
                TextStage.text = _sbStage.ToString();
                TextTime.text = _sbTime.ToString();

                if (ContentRect) {
                    float h = entries.Count * 18.0f; // Approx height
                    if (h < 300) h = 300;
                    ContentRect.sizeDelta = new Vector2(0, h);
                }

            } catch (Exception ex) {
                TextName.text = "UI Err: " + ex.Message;
            }
        }

        private string GetItemName(object itemInstance)
        {
            if (itemInstance == null) return "Unknown";
            try {
                // Get 'item' field from ItemInstance (Base class)
                Type t = itemInstance.GetType();
                FieldInfo fItem = null;
                while (t != null && fItem == null) {
                    fItem = t.GetField("item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    t = t.BaseType;
                }

                if (fItem != null) {
                    object itemObj = fItem.GetValue(itemInstance);
                    if (itemObj != null) {
                        
                        // 1. Try Native Localization (nameId -> LocalisationSystem)
                        try {
                            FieldInfo fNameId = itemObj.GetType().GetField("nameId", BindingFlags.Public | BindingFlags.Instance);
                            if (fNameId != null) {
                                string locKey = (string)fNameId.GetValue(itemObj);
                                if (!string.IsNullOrEmpty(locKey)) {
                                    string locName = LocalisationSystem.Get(locKey);
                                    if (!string.IsNullOrEmpty(locName)) return locName;
                                }
                            }
                            
                            // 1b. Try ID-based keys (Items/item_name_{id})
                            FieldInfo fId = itemObj.GetType().GetField("id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fId != null) {
                                int idVal = (int)fId.GetValue(itemObj);
                                string idKey = "Items/item_name_" + idVal;
                                string idName = LocalisationSystem.Get(idKey);
                                if (!string.IsNullOrEmpty(idName)) return idName;
                            }
                        } catch {}

                        // 2. Fallback: Asset Name (English/Internal)
                        PropertyInfo pName = itemObj.GetType().GetProperty("name"); 
                        if (pName != null) {
                             string name = (string)pName.GetValue(itemObj, null);
                             if (!string.IsNullOrEmpty(name)) return name;
                        }
                    }
                }
            } catch {}
            return "Unknown";
        }
    } // End Plugin

    public class CollapseHandler : MonoBehaviour
    {
        public RectTransform PanelRect;
        public GameObject ContentObj;
        public float ExpandedHeight;
        public float CollapsedHeight;
        public bool IsCollapsed = false;
        
        public void OnToggle()
        {
            IsCollapsed = !IsCollapsed;
            if (PanelRect) {
                PanelRect.sizeDelta = new Vector2(PanelRect.sizeDelta.x, IsCollapsed ? CollapsedHeight : ExpandedHeight);
            }
            if (ContentObj) {
                ContentObj.SetActive(!IsCollapsed);
            }
        }
    }

    public static class BarrelReflection
    {
        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static T GetStaticValueByType<T>(Type ownerType)
        {
            foreach (PropertyInfo prop in ownerType.GetProperties(AnyStatic))
            {
                if (prop.PropertyType == typeof(T) && prop.GetIndexParameters().Length == 0)
                {
                    try { return (T)prop.GetValue(null, null); } catch {}
                }
            }

            foreach (FieldInfo field in ownerType.GetFields(AnyStatic))
            {
                if (field.FieldType == typeof(T))
                {
                    try { return (T)field.GetValue(null); } catch {}
                }
            }

            return default(T);
        }
    }

    public class LoadStatusUI : MonoBehaviour {
        public string ModName = "";
        private float alpha = 1f;
        void OnGUI() {
            if (alpha <= 0) { Destroy(this.gameObject); return; }
            GUI.color = new Color(0.2f, 1f, 0.2f, alpha);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20; style.fontStyle = FontStyle.Bold;
            
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.Label(new Rect(21, 21, 400, 50), ModName + " Loaded!", style);
            GUI.color = new Color(0.2f, 1f, 0.2f, alpha);
            GUI.Label(new Rect(20, 20, 400, 50), ModName + " Loaded!", style);
            
            alpha -= Time.deltaTime / 5f;
        }
    }
}
