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
    [BepInPlugin("com.lolaiur.trbarrels", "Tavern Barrels", "1.2.0")]
    public class TRBarrelsPlugin : BaseUnityPlugin
    {
        public static TRBarrelsPlugin Instance;
        public static GameObject UI_OBJ;

        void Awake()
        {
             Instance = this;
             SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateUI();
        }
        
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7) || (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.B))) {
                if (UI_OBJ != null) {
                    bool state = !UI_OBJ.activeSelf;
                    UI_OBJ.SetActive(state);
                }
            }
            
            if (UI_OBJ != null && UI_OBJ.activeSelf) {
                Canvas c = UI_OBJ.GetComponent<Canvas>();
                if (c != null && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null) {
                    c.worldCamera = Camera.main;
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
                c.renderMode = RenderMode.ScreenSpaceCamera; // To appear behind Overlay cursor
                c.worldCamera = Camera.main; 
                c.planeDistance = 5; 
                c.sortingOrder = 100; // High enough to be over world, low enough for UI?
                // Actually Overlay is always on top. So any Camera mode is fine. 
                CanvasScaler cs = UI_OBJ.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                UI_OBJ.AddComponent<GraphicRaycaster>();
                
                // === MAIN PANEL ===
                GameObject panel = new GameObject("BarrelsPanel");
                panel.transform.SetParent(UI_OBJ.transform, false);
                Image border = panel.AddComponent<Image>();
                border.color = new Color(0.6f, 0.4f, 0.2f); 
                RectTransform panelRT = panel.GetComponent<RectTransform>();
                panelRT.anchorMin = new Vector2(1, 1); panelRT.anchorMax = new Vector2(1, 1);
                panelRT.pivot = new Vector2(1, 1); 
                panelRT.anchoredPosition = new Vector2(-20, -120);
                panelRT.sizeDelta = new Vector2(360, 400); // Standardized to Match TRTracker

                // Background
                GameObject bg = new GameObject("InnerBg");
                bg.transform.SetParent(panel.transform, false);
                Image bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0.15f, 0.1f, 0.05f, 0.98f); 
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
                CreateHeaderText(header.transform, "Aging Stats", 0, 360, TextAnchor.MiddleCenter);

                WindowDestroyer drag = header.AddComponent<WindowDestroyer>(); 
                drag.TargetMover = panelRT;

                // === BODY CONTAINER ===
                GameObject body = new GameObject("Body");
                body.transform.SetParent(bg.transform, false);
                RectTransform bodyRT = body.AddComponent<RectTransform>();
                bodyRT.anchorMin = Vector2.zero; bodyRT.anchorMax = Vector2.one;
                bodyRT.offsetMin = Vector2.zero; bodyRT.offsetMax = new Vector2(0, -35); // Ends at bottom of header

                // === LABELS ROW ===
                GameObject labels = new GameObject("Labels");
                labels.transform.SetParent(body.transform, false);
                RectTransform labelsRT = labels.AddComponent<RectTransform>();
                labelsRT.anchorMin = new Vector2(0, 1); labelsRT.anchorMax = new Vector2(1, 1);
                labelsRT.pivot = new Vector2(0, 1);
                labelsRT.sizeDelta = new Vector2(0, 25);
                labelsRT.anchoredPosition = Vector2.zero;

                // Move Column Headers here
                CreateHeaderText(labels.transform, "Product", 10, 165, TextAnchor.MiddleLeft);
                CreateHeaderText(labels.transform, "Stage", 170, 235, TextAnchor.MiddleCenter);
                CreateHeaderText(labels.transform, "Progress", 240, 330, TextAnchor.MiddleRight);

                // === SCROLL VIEW ===
                GameObject scrollObj = new GameObject("Scroll View");
                scrollObj.transform.SetParent(body.transform, false); // Child of Body
                RectTransform scrollRT = scrollObj.AddComponent<RectTransform>();
                scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
                scrollRT.offsetMin = new Vector2(5, 5); 
                scrollRT.offsetMax = new Vector2(-15, -25); // Below Labels

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
                RectTransform viewRT = viewport.AddComponent<RectTransform>();
                viewRT.anchorMin = Vector2.zero; viewRT.anchorMax = Vector2.one;
                viewRT.sizeDelta = Vector2.zero;
                viewport.AddComponent<Mask>().showMaskGraphic = false;
                Image maskImg = viewport.AddComponent<Image>();
                sr.viewport = viewRT;

                // Content
                GameObject content = new GameObject("Content");
                content.transform.SetParent(viewport.transform, false);
                RectTransform contentRT = content.AddComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
                contentRT.pivot = new Vector2(0, 1);
                contentRT.sizeDelta = new Vector2(0, 0);
                sr.content = contentRT;

                // === SCROLLBAR === (Removed Visuals)
                GameObject scrollbarObj = new GameObject("Scrollbar Vertical");
                scrollbarObj.transform.SetParent(bg.transform, false);
                RectTransform sbRT = scrollbarObj.AddComponent<RectTransform>();
                sbRT.anchorMin = new Vector2(1, 0); sbRT.anchorMax = new Vector2(1, 1);
                sbRT.pivot = new Vector2(1, 1);
                sbRT.anchoredPosition = Vector2.zero; 
                sbRT.sizeDelta = Vector2.zero; // Hidden
                
                // No Image // Image sbImg = scrollbarObj.AddComponent<Image>();
                
                Scrollbar sb = scrollbarObj.AddComponent<Scrollbar>();
                sb.direction = Scrollbar.Direction.BottomToTop;
                sr.verticalScrollbar = sb;

                // Sliding Area
                GameObject slidingArea = new GameObject("Sliding Area");
                slidingArea.transform.SetParent(scrollbarObj.transform, false);
                RectTransform slideRT = slidingArea.AddComponent<RectTransform>();
                slideRT.anchorMin = Vector2.zero; slideRT.anchorMax = Vector2.one;

                // Handle (Hidden)
                GameObject handle = new GameObject("Handle");
                handle.transform.SetParent(slidingArea.transform, false);
                // Explicitly add RectTransform since we aren't adding Image
                RectTransform handleRT = handle.AddComponent<RectTransform>();
                handleRT.sizeDelta = Vector2.zero;
                sb.handleRect = handleRT;
                // sb.targetGraphic = handleImg;

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
            rt.anchoredPosition3D = new Vector3(xMin, 0, -0.5f); // Increased Z offset
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
            rt.anchoredPosition3D = new Vector3(xMin, 0, -0.5f); // Increased Z offset
            rt.sizeDelta = new Vector2(xMax - xMin, 0); 
            
            ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return t;
        }
    }

    /// <summary>Handles window dragging via header.</summary>
    public class WindowDestroyer : MonoBehaviour, IDragHandler
    {
        public RectTransform TargetMover;
        public void OnDrag(PointerEventData data) {
            if (TargetMover) TargetMover.anchoredPosition += data.delta;
        }
    }

    /// <summary>Handles window resizing via bottom-right corner grip.</summary>
    public class ResizeHandler : MonoBehaviour, IDragHandler
    {
        public RectTransform PanelRect;
        public Vector2 MinSize = new Vector2(200, 150);
        public Vector2 MaxSize = new Vector2(800, 800);

        public void OnDrag(PointerEventData data) {
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
        
        private float scanTimer = 0f;
        private List<MonoBehaviour> cachedBarrels = new List<MonoBehaviour>();
        
        // Memory Optimization
        private System.Text.StringBuilder _sbName = new System.Text.StringBuilder();
        private System.Text.StringBuilder _sbStage = new System.Text.StringBuilder();
        private System.Text.StringBuilder _sbTime = new System.Text.StringBuilder();

        void Update()
        {
            try {
                 // Scan Loop
                scanTimer += Time.deltaTime;
                if (scanTimer > 1.0f) 
                {
                    scanTimer = 0f;
                    ScanBarrels();
                    if (cachedBarrels.Count > 0) UpdateList();
                }
            } catch {}
        }

        void ScanBarrels()
        {
            try {
                cachedBarrels.Clear();
                MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>();
                foreach(var m in all) {
                    if (m != null && m.GetType().Name == "AgingBarrel") cachedBarrels.Add(m);
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
                    if (!b.enabled) continue;

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
                                            PropertyInfo wtP = Type.GetType("WorldTime, Assembly-CSharp").GetProperty("MFHEEJMONON", BindingFlags.Public | BindingFlags.Static);
                                            ulong current = (ulong)wtP.GetValue(null, null);
                                            
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
}
