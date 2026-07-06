using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using BepInEx;
using HarmonyLib; 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TRTracker
{
    [BepInPlugin("com.lolaiur.trtracker", "Tavern Tracker", "1.3.1")]
    public class TRTrackerPlugin : BaseUnityPlugin
    {
        public static TRTrackerPlugin Instance;
        public static string LogPath;

        void Awake()
        {
             Instance = this;
             string logDir = Path.Combine(Paths.GameRootPath, "ModLogs");
             Directory.CreateDirectory(logDir);
             LogPath = Path.Combine(logDir, "tracker_debug.txt");
             try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { }
             try { File.WriteAllText(LogPath, "TRTracker 1.3.1\n"); } catch { }
             
             // Cleanup old
             var old = FindObjectOfType<TrackerManager>();
             if (old) Destroy(old.gameObject);

             GameObject go = new GameObject("TRTracker_Manager");
             DontDestroyOnLoad(go);
             go.AddComponent<TrackerManager>();
             
             var loadMsg = new GameObject("LoadMsg").AddComponent<LoadStatusUI>();
             loadMsg.ModName = "TRTracker";
             DontDestroyOnLoad(loadMsg.gameObject);
             
             try { new Harmony("com.lolaiur.trtracker").PatchAll(); } catch {}
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
            
            // Draw drop shadow
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.Label(new Rect(21, 21, 400, 50), ModName + " Loaded!", style);
            // Draw text
            GUI.color = new Color(0.2f, 1f, 0.2f, alpha);
            GUI.Label(new Rect(20, 20, 400, 50), ModName + " Loaded!", style);
            
            alpha -= Time.deltaTime / 5f;
        }
    }

    public class TrackerManager : MonoBehaviour
    {
        public static GameObject UI_OBJ;
        public static UIHandler UI;
        public static StatsHandler Stats;
        public static TimeHandler TimeCtrl;
        private Coroutine _refreshLoop;
        
        void Awake() {
             Stats = new StatsHandler();
             TimeCtrl = new TimeHandler();
             SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
             try {
                GameReflection.ClearSingletonCache();
                if(scene.name == "Gameplay") {
                    TRTrackerPatch.ResetDump();
                    EnsureRefreshLoop();
                }
                ForceRecreate();
             } catch {}
        }
        
        public void ForceRecreate() {
             if(UI_OBJ!=null) {
                 if(!UI_OBJ.activeSelf) UI_OBJ.SetActive(true);
                 return;
             }
             CreateUI();
        }

        void Start() {
            CreateUI();
            EnsureRefreshLoop();
        }

        void OnDestroy() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
            _refreshLoop = null;
        }

        private void EnsureRefreshLoop() {
            if (_refreshLoop == null) _refreshLoop = StartCoroutine(RefreshTrackerLoop());
        }

        private IEnumerator RefreshTrackerLoop() {
            var delay = new WaitForSecondsRealtime(0.25f);
            while (true) {
                try { TRTrackerPatch.Refresh(); } catch {}
                yield return delay;
            }
        }

        private bool _showUI = true;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) {
                if (UI_OBJ != null) {
                    _showUI = !_showUI;
                    UI_OBJ.SetActive(_showUI);
                }
            }
            if (Input.GetKeyDown(KeyCode.F9)) {
                if (TimeCtrl != null) TimeCtrl.ToggleFreeze();
            }
            
             if (UI_OBJ != null && UI_OBJ.activeSelf) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            if (UI_OBJ == null) CreateUI();
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

                UI_OBJ = new GameObject("TRTrackerCanvas");
                DontDestroyOnLoad(UI_OBJ);
                
                Canvas c = UI_OBJ.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay; 
                c.sortingOrder = 100; 
                
                CanvasScaler cs = UI_OBJ.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                
                UI_OBJ.AddComponent<GraphicRaycaster>();
                
                // --- PANEL (Root) ---
                GameObject panel = new GameObject("TRPanel");
                panel.transform.SetParent(UI_OBJ.transform, false);
                RectTransform panelRT = panel.GetComponent<RectTransform>();
                if (panelRT == null) panelRT = panel.AddComponent<RectTransform>();
                
                Image border = panel.AddComponent<Image>();
                border.color = new Color(0.6f, 0.4f, 0.2f); 
                border.raycastTarget = false;
                panelRT.anchorMin = new Vector2(0, 1); panelRT.anchorMax = new Vector2(0, 1);
                panelRT.pivot = new Vector2(0, 1);
                panelRT.anchoredPosition = new Vector2(20, -120);
                panelRT.sizeDelta = new Vector2(360, 420); // Height increased for new stats

                // --- BACKGROUND ---
                GameObject bg = new GameObject("Background");
                bg.transform.SetParent(panel.transform, false);
                Image bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0.15f, 0.1f, 0.05f, 0.98f); 
                bgImg.raycastTarget = true;
                bg.AddComponent<WindowPointerFocus>();
                RectTransform bgRT = bg.GetComponent<RectTransform>();
                if (bgRT==null) bgRT = bg.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = new Vector2(2, 2); bgRT.offsetMax = new Vector2(-2, -2);
                
                // --- HEADER ---
                GameObject header = new GameObject("Header");
                header.transform.SetParent(bg.transform, false);
                Image hImg = header.AddComponent<Image>();
                hImg.color = new Color(0.25f, 0.15f, 0.05f, 1f); 
                RectTransform headerRT = header.GetComponent<RectTransform>();
                if (headerRT==null) headerRT = header.AddComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0, 1); headerRT.anchorMax = new Vector2(1, 1);
                headerRT.pivot = new Vector2(0, 1);
                headerRT.anchoredPosition = new Vector2(0, 0);
                headerRT.sizeDelta = new Vector2(0, 30);
                
                // Header Title
                GameObject hTitle = new GameObject("Title");
                hTitle.transform.SetParent(header.transform, false);
                Text ht = hTitle.AddComponent<Text>();
                ht.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                ht.text = "TAVERN TRACKER 1.3.1 (F1)";
                ht.alignment = TextAnchor.MiddleCenter;
                ht.color = new Color(1f, 0.8f, 0.4f);
                ht.fontSize = 14;
                ht.raycastTarget = false;
                RectTransform htRT = hTitle.GetComponent<RectTransform>();
                if (htRT==null) htRT = hTitle.AddComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero; htRT.anchorMax = Vector2.one;
                htRT.sizeDelta = Vector2.zero;
                htRT.anchoredPosition = Vector2.zero; 

                // --- COLLAPSE BUTTON ---
                GameObject btnObj = new GameObject("CollapseBtn");
                btnObj.transform.SetParent(header.transform, false);
                Image btnImg = btnObj.AddComponent<Image>();
                btnImg.color = new Color(0.18f, 0.28f, 0.18f, 1f);
                RectTransform btnRT = btnObj.GetComponent<RectTransform>();
                if (btnRT == null) btnRT = btnObj.AddComponent<RectTransform>();
                
                btnRT.anchorMin = new Vector2(1, 0.5f); btnRT.anchorMax = new Vector2(1, 0.5f);
                btnRT.pivot = new Vector2(1, 0.5f);
                btnRT.anchoredPosition = new Vector2(-5, 0);
                btnRT.sizeDelta = new Vector2(18, 18);
                
                Button btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                GameObject btnLabelObj = new GameObject("Label");
                btnLabelObj.transform.SetParent(btnObj.transform, false);
                Text btnLabel = btnLabelObj.AddComponent<Text>();
                btnLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                btnLabel.text = "-";
                btnLabel.alignment = TextAnchor.MiddleCenter;
                btnLabel.color = Color.white;
                btnLabel.fontStyle = FontStyle.Bold;
                btnLabel.raycastTarget = false;
                RectTransform btnLabelRT = btnLabelObj.GetComponent<RectTransform>();
                btnLabelRT.anchorMin = Vector2.zero; btnLabelRT.anchorMax = Vector2.one;
                btnLabelRT.offsetMin = Vector2.zero; btnLabelRT.offsetMax = Vector2.zero;
                CollapseHandler ch = btnObj.AddComponent<CollapseHandler>();
                ch.PanelRect = panelRT;
                ch.ExpandedHeight = 420; 
                ch.CollapsedHeight = 35; 
                ch.Label = btnLabel;
                btn.onClick.AddListener(ch.OnToggle);

                UI = panel.AddComponent<UIHandler>();

                // --- SCROLL VIEW ---
                GameObject scrollObj = new GameObject("Scroll View");
                scrollObj.transform.SetParent(bg.transform, false);
                RectTransform scrollRT = scrollObj.AddComponent<RectTransform>();
                scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
                scrollRT.offsetMin = new Vector2(10, 25);
                scrollRT.offsetMax = new Vector2(-25, -35); // Margin for scrollbar
                
                ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
                sr.horizontal = false; sr.vertical = true;
                sr.scrollSensitivity = 25f; sr.movementType = ScrollRect.MovementType.Elastic;
                sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

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
                
                ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
                vlg.childControlHeight = true; vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

                // --- SCROLLBAR ---
                GameObject scrollbarObj = new GameObject("Scrollbar Vertical");
                scrollbarObj.transform.SetParent(bg.transform, false);
                RectTransform sbRT = scrollbarObj.AddComponent<RectTransform>();
                sbRT.anchorMin = new Vector2(1, 0); sbRT.anchorMax = new Vector2(1, 1);
                sbRT.pivot = new Vector2(1, 1);
                sbRT.anchoredPosition = new Vector2(-5, -35); 
                sbRT.sizeDelta = new Vector2(15, -60); // Offset top and bottom
                
                Image sbBgImg = scrollbarObj.AddComponent<Image>();
                sbBgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                sbBgImg.raycastTarget = false;
                
                Scrollbar sb = scrollbarObj.AddComponent<Scrollbar>();
                sb.direction = Scrollbar.Direction.BottomToTop;
                sr.verticalScrollbar = sb;

                GameObject slidingArea = new GameObject("Sliding Area");
                slidingArea.transform.SetParent(scrollbarObj.transform, false);
                RectTransform slideRT = slidingArea.AddComponent<RectTransform>();
                slideRT.anchorMin = Vector2.zero; slideRT.anchorMax = Vector2.one;
                slideRT.sizeDelta = Vector2.zero;
                slideRT.offsetMin = Vector2.zero; slideRT.offsetMax = Vector2.zero;

                GameObject handle = new GameObject("Handle");
                handle.transform.SetParent(slidingArea.transform, false);
                RectTransform handleRT = handle.AddComponent<RectTransform>();
                handleRT.sizeDelta = Vector2.zero;
                handleRT.offsetMin = Vector2.zero; handleRT.offsetMax = Vector2.zero;
                Image handleImg = handle.AddComponent<Image>();
                handleImg.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                sb.handleRect = handleRT;
                sb.targetGraphic = handleImg;

                UI.ContentObj = scrollObj; // Used for Collapse logic later

                // --- CONTENT TEXT ---
                GameObject text = new GameObject("TRText");
                text.transform.SetParent(content.transform, false);
                Text t = text.AddComponent<Text>();
                t.raycastTarget = false;

                Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                try { Font con = Font.CreateDynamicFontFromOSFont("Consolas", 14); if (con != null) f = con; } catch {}
                t.font = f;
                t.color = Color.white;
                t.fontSize = 14;
                t.lineSpacing = 1.15f;
                t.alignment = TextAnchor.UpperLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; 
                t.verticalOverflow = VerticalWrapMode.Truncate;
                UI.MainText = t;

                RectTransform trt = text.GetComponent<RectTransform>();
                // Layout controlled by VLG now

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

                // Triangle indicator via child
                GameObject tri = new GameObject("Triangle");
                tri.transform.SetParent(grip.transform, false);
                Text triText = tri.AddComponent<Text>();
                triText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                triText.text = "\u25E2"; // Unicode bottom-right triangle
                triText.fontSize = 16;
                triText.color = new Color(1f, 0.9f, 0.7f, 0.8f);
                triText.alignment = TextAnchor.MiddleCenter;
                triText.raycastTarget = false;
                RectTransform triRT = tri.GetComponent<RectTransform>();
                triRT.anchorMin = Vector2.zero; triRT.anchorMax = Vector2.one;
                triRT.sizeDelta = Vector2.zero;

                ResizeHandler rh = grip.AddComponent<ResizeHandler>();
                rh.PanelRect = panelRT;

                // --- DRAG LOGIC ---
                WindowDestroyer drag = header.AddComponent<WindowDestroyer>();
                drag.TargetMover = panelRT;

                UI.Init(panelRT);
                t.text = "Waiting for game data..."; 
            }
            catch (Exception ex) {
                File.AppendAllText(TRTrackerPlugin.LogPath, "CreateUI CRASH: " + ex.ToString() + "\n");
            }
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
        public RectTransform ContentRect;
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

    public class UIHandler : MonoBehaviour
    {
        public Text MainText;
        public GameObject ContentObj; // Scroll view object to toggle on collapse
        private RectTransform startRect;
        private System.Text.StringBuilder _sb = new System.Text.StringBuilder(512);

        public void Init(RectTransform rt) { startRect = rt; }

        public void UpdateDisplay(string time, string dateLine, bool open, long g, long s, long c, int rawXP, int level, int maxXP, int occ, string temp, string dirt, string comfortStr, int totalServed)
        {
            if (!MainText) return;
            TrackerManager.Stats.Update(currentTotal: (g * 10000) + (s * 100) + c, rawXP: rawXP, level: level, maxXP: maxXP, isOpen: open, customersActive: occ, totalServed: totalServed);

            _sb.Clear();
            _sb.AppendFormat("<size=16><b>TIME:   {0}</b></size>{1}\n", time, TrackerManager.TimeCtrl.IsFrozen ? " <color=#00ffff>[PAUSED]</color>" : "");
            _sb.Append(dateLine).Append("\n");
            _sb.AppendFormat("STATUS: {0}   TEMP: {1}\n", open ? "<color=#00ff00>OPEN</color>" : "<color=#ff4444>CLOSED</color>", temp);
            _sb.AppendFormat("COMFORT:{0}\n", comfortStr);
            _sb.AppendFormat("DIRT:   {0}\n", dirt);
            _sb.Append("------------------------------\n");
            _sb.AppendFormat("<size=16><color=#ffd700>WEALTH: {0}g {1}s {2}c</color></size>\n", g, s, c);
            _sb.AppendFormat("PROFIT: {0} ({1}/m)\n", TrackerManager.Stats.FormatMoney(TrackerManager.Stats.SessionProfit), TrackerManager.Stats.RateMin.ToString("F1"));
            _sb.AppendFormat("PREV.P: {0}\n", TrackerManager.Stats.FormatMoney(TrackerManager.Stats.LastSessionProfit));
            _sb.Append("------------------------------\n");
            _sb.AppendFormat("XP:     {0} (Lvl {1})\n", TrackerManager.Stats.SessionXD_Display, level);
            long deltaXP = TrackerManager.Stats.SessionXD;
            _sb.AppendFormat("GAIN:   {0}\n", deltaXP >= 0 ? "+" + deltaXP : deltaXP.ToString());
            long prevXP = TrackerManager.Stats.LastSessionXD;
            _sb.AppendFormat("PREV.X: {0}\n", prevXP >= 0 ? "+" + prevXP : prevXP.ToString());
            _sb.Append("------------------------------\n");
            _sb.AppendFormat("CUST:   {0} (Served: {1})", occ, TrackerManager.Stats.SessionCustomers);

            MainText.text = _sb.ToString();
        }
    }
    
    public class StatsHandler {
        public long SessionStart=-1, WorkingSessionStart=-1, LastSessionProfit=0, SessionProfit=0;
        
        // XP Tracking
        public long LifetimeXP = 0;
        public long SessionXD = 0;
        public long LastSessionXD = 0;
        public long SessionStartLifetimeXP = -1;
        public long WorkingStartLifetimeXP = -1;
        public long SessionXD_Display = 0; 
        
        private int lastLevel = -1;
        private int lastRawXP = -1;
        
        // Customer Tracking
        public int SessionCustomers = 0; // Served this session
        public int WorkingStartCustomersTotal = 0; // Total count when session started
        
        public float RateMin=0f; 
        private bool wasOpen=false;
        private Queue<KeyValuePair<float,long>> fh = new Queue<KeyValuePair<float,long>>();
        
        public string FormatMoney(long v) { return string.Format("{0}{1}g {2}s {3}c", v>=0?"+":"-", Math.Abs(v)/10000, (Math.Abs(v)%10000)/100, Math.Abs(v)%100); }

        public void Update(long currentTotal, int rawXP, int level, int maxXP, bool isOpen, int customersActive, int totalServed) {
            // First time init
            if (lastLevel == -1) { lastLevel = level; lastRawXP = rawXP; }
            if (SessionStart == -1) { 
                SessionStart = currentTotal; 
                SessionStartLifetimeXP = 0; 
            }

            // XP Delta Logic
            long deltaFrame = 0;
            if (level > lastLevel) {
                deltaFrame = rawXP; 
            } 
            else if (level == lastLevel) {
                 deltaFrame = rawXP - lastRawXP;
            }
            
            lastLevel = level;
            lastRawXP = rawXP;
            
            LifetimeXP += deltaFrame;
            SessionXD_Display = LifetimeXP - SessionStartLifetimeXP; 

            // Session Logic (Open/Close)
            if (isOpen && !wasOpen) { 
                WorkingSessionStart = currentTotal; 
                WorkingStartLifetimeXP = LifetimeXP; 
                WorkingStartCustomersTotal = totalServed; // Snapshot total at open
                fh.Clear(); 
                RateMin = 0f; 
            }
            if (!isOpen && wasOpen) { 
                LastSessionProfit = currentTotal - WorkingSessionStart; 
                LastSessionXD = LifetimeXP - WorkingStartLifetimeXP;
                // Session Customers is just the current delta
                SessionCustomers = totalServed - WorkingStartCustomersTotal;
            }
            
            wasOpen = isOpen;
            
            if (isOpen) { 
                SessionProfit = currentTotal - WorkingSessionStart; 
                SessionXD = LifetimeXP - WorkingStartLifetimeXP; 
                SessionCustomers = totalServed - WorkingStartCustomersTotal;
                if (SessionCustomers < 0) SessionCustomers = 0; // Safety
                
                float now = Time.unscaledTime;
                fh.Enqueue(new KeyValuePair<float,long>(now, currentTotal));
                while(fh.Count>0 && (now-fh.Peek().Key)>60f) fh.Dequeue();
                if(fh.Count>1) { float dt=fh.Last().Key-fh.Peek().Key; long dc=fh.Last().Value-fh.Peek().Value; if(dt>1f) RateMin=((dc/dt)*60f)/10000f; }
            } else { 
                SessionProfit=0; 
                SessionXD=0; 
                SessionCustomers=0;
                RateMin=0f; 
            }
        }
    }
    
    public class TimeHandler {
        public bool IsFrozen=false; private float s=1f;
        public void ToggleFreeze() {
            try {
                FieldInfo f = typeof(WorldTime).GetField("multiplier", BindingFlags.Public|BindingFlags.Static);
                if (f == null) return;
                float cur = (float)f.GetValue(null);
                MethodInfo m = typeof(WorldTime).GetMethod("SetTimeMultiplier", BindingFlags.Public|BindingFlags.Static);
                if(cur>0.01f) { s=cur; SetMultiplier(m, f, 0f); IsFrozen=true; }
                else { SetMultiplier(m, f, s); IsFrozen=false; }
            } catch {}
        }

        private void SetMultiplier(MethodInfo method, FieldInfo field, float value) {
            if (method != null) method.Invoke(null, new object[]{ value });
            else field.SetValue(null, value);
        }
    }

    public static class GameReflection
    {
        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Dictionary<Type, UnityEngine.Object> SingletonCache = new Dictionary<Type, UnityEngine.Object>();

        public static void ClearSingletonCache()
        {
            SingletonCache.Clear();
        }

        public static T FindSingleton<T>() where T : UnityEngine.Object
        {
            Type type = typeof(T);
            UnityEngine.Object cached;
            if (SingletonCache.TryGetValue(type, out cached))
            {
                T cachedValue = cached as T;
                if (cachedValue != null) return cachedValue;
                SingletonCache.Remove(type);
            }

            T found = UnityEngine.Object.FindObjectOfType<T>();
            if (found != null) {
                SingletonCache[type] = found;
                return found;
            }

            foreach (PropertyInfo prop in type.GetProperties(AnyStatic))
            {
                if (prop.PropertyType == type && prop.GetIndexParameters().Length == 0)
                {
                    try {
                        T value = prop.GetValue(null, null) as T;
                        if (value != null) {
                            SingletonCache[type] = value;
                            return value;
                        }
                    } catch {}
                }
            }

            foreach (FieldInfo field in type.GetFields(AnyStatic))
            {
                if (field.FieldType == type)
                {
                    try {
                        T value = field.GetValue(null) as T;
                        if (value != null) {
                            SingletonCache[type] = value;
                            return value;
                        }
                    } catch {}
                }
            }

            return null;
        }

        public static object GetStaticValueByType(Type ownerType, Type valueType)
        {
            foreach (PropertyInfo prop in ownerType.GetProperties(AnyStatic))
            {
                if (prop.PropertyType == valueType && prop.GetIndexParameters().Length == 0)
                {
                    try { return prop.GetValue(null, null); } catch {}
                }
            }

            foreach (FieldInfo field in ownerType.GetFields(AnyStatic))
            {
                if (field.FieldType == valueType)
                {
                    try { return field.GetValue(null); } catch {}
                }
            }

            return null;
        }

        public static PropertyInfo FindInstancePropertyByType(Type ownerType, Type valueType)
        {
            foreach (PropertyInfo prop in ownerType.GetProperties(AnyInstance))
            {
                if (prop.PropertyType == valueType && prop.GetIndexParameters().Length == 0)
                {
                    return prop;
                }
            }

            return null;
        }

        public static FieldInfo FindClosestIntField(object instance, int minimumValue)
        {
            FieldInfo best = null;
            int bestValue = int.MaxValue;

            foreach (FieldInfo field in instance.GetType().GetFields(AnyInstance))
            {
                if (field.FieldType != typeof(int)) continue;

                try {
                    int value = (int)field.GetValue(instance);
                    if (value >= minimumValue && value < bestValue)
                    {
                        best = field;
                        bestValue = value;
                    }
                } catch {}
            }

            return best;
        }
    }

    public static class TRTrackerPatch {
        private static DateTime lastUIUpdate = DateTime.MinValue;
        private const double UIUpdateIntervalSeconds = 0.25;
        public static void ResetDump() {}

        public static void Refresh() {
            if ((DateTime.Now - lastUIUpdate).TotalSeconds <= UIUpdateIntervalSeconds) return;
            lastUIUpdate = DateTime.Now;
            if (TrackerManager.UI == null || TrackerManager.UI.MainText == null || !TrackerManager.UI.MainText.gameObject.activeInHierarchy) return;

            TavernManager tm = GameReflection.FindSingleton<TavernManager>();
            if (tm == null) return;
            Gather(tm, GetOpenState(tm));
        }

        private static bool GetOpenState(TavernManager tm) {
            FieldInfo openField = typeof(TavernManager).GetField("_open", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (openField != null && openField.FieldType == typeof(bool)) {
                try { return (bool)openField.GetValue(tm); } catch {}
            }

            foreach (PropertyInfo prop in typeof(TavernManager).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                if (prop.PropertyType != typeof(bool) || prop.GetIndexParameters().Length != 0) continue;
                try {
                    MethodInfo getter = prop.GetGetMethod(true);
                    if (getter != null && getter.IsSpecialName) {
                        return (bool)prop.GetValue(tm, null);
                    }
                } catch {}
            }

            return false;
        }

        static void Gather(TavernManager tm, bool o) {
            try {
                 if (tm == null) return;

                 // Date
                 Type wt=Type.GetType("WorldTime, Assembly-CSharp");
                 object d = GameReflection.GetStaticValueByType(wt, typeof(GameDate));
                 if (d == null) return;
                 Type dt=d.GetType();
                 int h=(int)dt.GetField("hour").GetValue(d);
                 int m=(int)dt.GetField("min").GetValue(d);
                 string tStr=string.Format("{0}:{1:00} {2}", (h<=12?h:h-12)==0?12:(h<=12?h:h-12), m, h<12?"AM":"PM");
                 
                 var year = dt.GetField("year").GetValue(d);
                 var season = dt.GetField("season").GetValue(d);
                 int week = (int)dt.GetField("week").GetValue(d);
                 object dayObj = dt.GetField("day").GetValue(d); 
                 string dayName = dayObj.ToString(); 
                 if (dayName.Length > 3) dayName = dayName.Substring(0, 3);
                 int dayInt = (int)dayObj; 
                 int dayNum = (week * 7) + dayInt + 1;
                 string dateLine = string.Format("DATE:   {0}, {1} ({2}, Day {3})", season, year, dayName, dayNum);

                 // Money
                 Money mon=Money.GetInstance();
                 object b=typeof(Money).GetField("balance",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(mon);
                 Type bt=b.GetType();
                 int g=(int)bt.GetProperty("Gold").GetValue(b,null);
                 int s=(int)bt.GetProperty("Silver").GetValue(b,null);
                 int c=(int)bt.GetProperty("Copper").GetValue(b,null);
                 
                 // XP & Level
                 TavernReputation rep = GameReflection.FindSingleton<TavernReputation>();
                 if (rep == null) return;
                 int rawXP = TavernReputation.GetReputationExp(); 
                 PropertyInfo levelProp = GameReflection.FindInstancePropertyByType(typeof(TavernReputation), typeof(int));
                 int level = levelProp != null ? (int)levelProp.GetValue(rep, null) : 0;
                 FieldInfo maxXPField = GameReflection.FindClosestIntField(rep, rawXP);
                 int maxXP = maxXPField != null ? (int)maxXPField.GetValue(rep) : rawXP;

                 // Customers (Active)
                 int occ = tm.customers.Count;
                 
                 // Customers (Total Served) - For Session Stats
                 int totalServed = 0;
                 try {
                     var tsMgr = GameReflection.FindSingleton<TavernServiceManager>();
                     if (tsMgr != null) {
                         var statsList = tsMgr.GetAllTavernStats(); // Returns List<TavernStats> (tavernStats)
                         if (statsList != null && statsList.Count > 0) {
                             var todayStats = statsList[statsList.Count - 1]; // Last entry is today
                             totalServed = todayStats.customersCount;
                         }
                     }
                 } catch {}

                 // Temp
                 PropertyInfo heatProp = GameReflection.FindInstancePropertyByType(typeof(TavernManager), typeof(HeatLevel));
                 object heatVal = heatProp != null ? heatProp.GetValue(tm, null) : "Unknown";
                 string heatStr = heatVal.ToString();
                 string heatColor = "white"; 
                 if (heatStr.Contains("Perfect")) heatColor = "green";
                 else if (heatStr.Contains("Cold") || heatStr.Contains("Freezing")) heatColor = "blue";
                 else if (heatStr.Contains("Hot") || heatStr.Contains("Warm")) heatColor = "red";
                 heatStr = string.Format("<color={0}>{1}</color>", heatColor, heatStr);

                 // Dirt Level
                 PropertyInfo dirtProp = GameReflection.FindInstancePropertyByType(typeof(TavernManager), typeof(DirtLevel));
                 object dirtVal = dirtProp != null ? dirtProp.GetValue(tm, null) : "Unknown";
                 string dirtStr = dirtVal.ToString();
                 string dirtColor = "white";
                 if (dirtStr.Contains("Perfect") || dirtStr.Contains("Clean")) dirtColor = "green";
                 else if (dirtStr.Contains("Dirty") || dirtStr.Contains("Filthy") || dirtStr.Contains("Messy")) dirtColor = "red";
                 dirtStr = string.Format("<color={0}>{1}</color>", dirtColor, dirtStr);
                 
                 // Comfort Level (Zone Based)
                 string comfortStr = "N/A";
                 try {
                     // Get Local Player (1) - 0 is unused in array
                     // Fallback loop if needed
                     var player = PlayerController.GetPlayer(1);
                     if (player == null) {
                         for(int i=1;i<=4;i++) {
                             player = PlayerController.GetPlayer(i);
                             if(player!=null) break;
                         }
                     }
                     
                     if (player == null) {
                         comfortStr = "NoPlayer";
                     }
                     else {
                         // Get Zone Index and Zone Type
                         int zIndex = player.zoneIndex;

                         // Get Zone Manager
                         var zoneMgr = GameReflection.FindSingleton<TavernZonesManager>();
                         if (zoneMgr == null) {
                              comfortStr = "NoMgr";
                         }
                         else {
                             var zone = zoneMgr.GetTavernZone(zIndex);
                             if (zone == null) {
                                 comfortStr = "Outside";
                             }
                             else {
                                 int val = zone.comfort;
                                 string color = "white";
                                 if (val > 100) color = "green";
                                 else if (val < 0) color = "red";
                                 
                                 string zName = zone.zoneType.ToString();
                                 if (zName == "DiningRoom") zName = "Dining";
                                 else if (zName == "CraftingRoom") zName = "Crafting";
                                 else if (zName == "RentedRoom") zName = "Room";
                                 else if (zName == "WithoutZone") zName = "None";
                                 
                                 comfortStr = string.Format(" {0} <size=10>({1})</size>", val, zName);
                                 comfortStr = string.Format("<color={0}>{1}</color>", color, comfortStr);
                             }
                         }
                     }
                 } catch (Exception ex) {
                     File.AppendAllText(TRTrackerPlugin.LogPath, "COMFORT CRASH: " + ex.Message + "\n");
                     comfortStr = "Err";
                 }

                 if (TrackerManager.UI != null) {
                     TrackerManager.UI.UpdateDisplay(tStr, dateLine, o, g, s, c, rawXP, level, maxXP, occ, heatStr, dirtStr, comfortStr, totalServed); 
                 } 
                 
            } catch (Exception ex) {
                 File.AppendAllText(TRTrackerPlugin.LogPath, "Err: "+ex+"\n");
            }

        }
    } 

    public class CollapseHandler : MonoBehaviour
    {
        public RectTransform PanelRect;
        public float ExpandedHeight;
        public float CollapsedHeight;
        public bool IsCollapsed = false;
        public Text Label;
        
        public void OnToggle()
        {
            IsCollapsed = !IsCollapsed;
            if (PanelRect) {
                PanelRect.sizeDelta = new Vector2(PanelRect.sizeDelta.x, IsCollapsed ? CollapsedHeight : ExpandedHeight);
            }
            
            if (TrackerManager.UI != null) {
                if (TrackerManager.UI.ContentObj != null) TrackerManager.UI.ContentObj.gameObject.SetActive(!IsCollapsed);
                else if (TrackerManager.UI.MainText != null) TrackerManager.UI.MainText.gameObject.SetActive(!IsCollapsed);
            }
            if (Label != null) Label.text = IsCollapsed ? "+" : "-";
        }
    }
}
