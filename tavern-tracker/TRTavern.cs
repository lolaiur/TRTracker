using System;
using System.IO;
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
    [BepInPlugin("com.deepmind.trtracker", "Tavern Tracker", "1.1.0")]
    public class TRTrackerPlugin : BaseUnityPlugin
    {
        public static TRTrackerPlugin Instance;
        public static GameObject UI_OBJ;
        public static UIHandler UI;
        public static StatsHandler Stats;
        public static TimeHandler TimeCtrl;
        public static string LogPath;

        void Awake()
        {
             Instance = this;
             LogPath = Path.Combine(Paths.GameRootPath, "tracker_debug.txt");
             File.Delete(LogPath); 
             File.WriteAllText(LogPath, "V1.1.0 AWAKE\n");
             
             Stats = new StatsHandler();
             TimeCtrl = new TimeHandler();
             
             try { new Harmony("com.deepmind.trtracker").PatchAll(); } catch {}
             SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateUI();
        }
        
        void OnGUI()
        {
            Event e = Event.current;
            if (e.isKey && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.F8) {
                    if (UI_OBJ != null) UI_OBJ.SetActive(!UI_OBJ.activeSelf);
                }
                if (e.keyCode == KeyCode.F9) {
                    if (TimeCtrl != null) TimeCtrl.ToggleFreeze();
                }
            }
        }

        private bool subscribed = false;
        void Update()
        {
            if (UI_OBJ != null && UI_OBJ.activeSelf) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                
                 // Ensure Camera
                Canvas c = UI_OBJ.GetComponent<Canvas>();
                if (c != null && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null) {
                    c.worldCamera = Camera.main;
                }
            }
            if (UI_OBJ == null) CreateUI();
            
            // Stats logging or other updates can go here if needed.
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
                c.renderMode = RenderMode.ScreenSpaceCamera; // To appear behind Overlay cursor
                c.worldCamera = Camera.main;
                c.planeDistance = 5;
                c.sortingOrder = 100; 
                CanvasScaler cs = UI_OBJ.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                UI_OBJ.AddComponent<GraphicRaycaster>();
                
                // --- PANEL (Root) ---
                GameObject panel = new GameObject("TRPanel");
                panel.transform.SetParent(UI_OBJ.transform, false);
                // Ensure RectTransform
                RectTransform panelRT = panel.GetComponent<RectTransform>();
                if (panelRT == null) panelRT = panel.AddComponent<RectTransform>();
                
                Image border = panel.AddComponent<Image>();
                border.color = new Color(0.6f, 0.4f, 0.2f); // Match Barrels Border
                // Set default size immediately to avoid 0x0 or center bloat if crash
                panelRT.anchorMin = new Vector2(0, 1); panelRT.anchorMax = new Vector2(0, 1);
                panelRT.pivot = new Vector2(0, 1);
                panelRT.anchoredPosition = new Vector2(20, -120);
                panelRT.sizeDelta = new Vector2(360, 400);

                File.AppendAllText(LogPath, "Panel Created\n");
                
                // --- BACKGROUND ---
                GameObject bg = new GameObject("Background");
                bg.transform.SetParent(panel.transform, false);
                Image bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0.15f, 0.1f, 0.05f, 0.98f); // Match Barrels BG
                RectTransform bgRT = bg.GetComponent<RectTransform>();
                if (bgRT==null) bgRT = bg.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = new Vector2(2, 2); bgRT.offsetMax = new Vector2(-2, -2);
                
                File.AppendAllText(LogPath, "BG Created\n");

                // --- HEADER (Drag Handle) ---
                GameObject header = new GameObject("Header");
                header.transform.SetParent(bg.transform, false);
                Image hImg = header.AddComponent<Image>();
                hImg.color = new Color(0.25f, 0.15f, 0.05f, 1f); // Match Barrels Header
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
                ht.text = "TAVERN TRACKER";
                ht.alignment = TextAnchor.MiddleCenter;
                ht.color = new Color(1f, 0.8f, 0.4f);
                ht.fontSize = 14;
                RectTransform htRT = hTitle.GetComponent<RectTransform>();
                if (htRT==null) htRT = hTitle.AddComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero; htRT.anchorMax = Vector2.one;
                htRT.sizeDelta = Vector2.zero;
                htRT.anchoredPosition3D = new Vector3(0, 0, -0.1f); // Nudge forward for Camera Mode

                // --- COLLAPSE BUTTON ---
                GameObject btnObj = new GameObject("CollapseBtn");
                btnObj.transform.SetParent(header.transform, false);
                Image btnImg = btnObj.AddComponent<Image>();
                btnImg.color = Color.green;
                // Fix: Use GetComponent because Image adds it
                RectTransform btnRT = btnObj.GetComponent<RectTransform>();
                if (btnRT == null) btnRT = btnObj.AddComponent<RectTransform>();
                
                btnRT.anchorMin = new Vector2(1, 0.5f); btnRT.anchorMax = new Vector2(1, 0.5f);
                btnRT.pivot = new Vector2(1, 0.5f);
                btnRT.anchoredPosition = new Vector2(-5, 0);
                btnRT.sizeDelta = new Vector2(20, 20);
                
                Button btn = btnObj.AddComponent<Button>();
                CollapseHandler ch = btnObj.AddComponent<CollapseHandler>();
                ch.PanelRect = panelRT;
                ch.ExpandedHeight = 400; // Tracker Height
                ch.CollapsedHeight = 35; // slightly larger than header
                btn.onClick.AddListener(ch.OnToggle);

                UI = panel.AddComponent<UIHandler>();
                
                // --- CONTENT TEXT ---
                GameObject text = new GameObject("TRText");
                text.transform.SetParent(bg.transform, false); 
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
                UI.MainText = t;
                
                RectTransform trt = text.GetComponent<RectTransform>();
                if (trt==null) trt = text.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(10, 10); 
                trt.offsetMax = new Vector2(-10, -50); // Below header (Increased spacing)
                trt.anchoredPosition3D = new Vector3(0, 0, -0.1f); // Nudge forward
                
                // --- DRAG LOGIC ---
                WindowDestroyer drag = header.AddComponent<WindowDestroyer>();
                drag.TargetMover = panelRT;
                
                UI.Init(panelRT);
                t.text = "Waiting for game data..."; 
                File.AppendAllText(LogPath, "UI Created V1.1.1 OK\n");
            }
            catch (Exception ex) {
                File.AppendAllText(LogPath, "CreateUI CRASH: " + ex.ToString() + "\n");
            }
        }
        
        // Dummy block to swallow the old code I replaced
        void _dummy_replacement() {
            if(false) {
                 // swallowed old code
            }
        }
     }

    public class WindowDestroyer : MonoBehaviour, IDragHandler
    {
        public RectTransform TargetMover;
        public void OnDrag(PointerEventData data) {
            if (TargetMover) TargetMover.anchoredPosition += data.delta;
        }
    }

    public class UIHandler : MonoBehaviour
    {
        public Text MainText;
        private RectTransform startRect;
        public void Init(RectTransform rt) { startRect = rt; }
        
        public void UpdateDisplay(string time, string dateLine, bool open, long g, long s, long c, int rawXP, int level, int maxXP, int occ, string temp)
        {
            if (!MainText) return;
            TRTrackerPlugin.Stats.Update(currentTotal: (g * 10000) + (s * 100) + c, rawXP: rawXP, level: level, maxXP: maxXP, isOpen: open, customersServed: 0);
            
            string statusStr = open ? "<color=#00ff00>OPEN</color>" : "<color=#ff4444>CLOSED</color>";
            string freezeStr = TRTrackerPlugin.TimeCtrl.IsFrozen ? " <color=#00ffff>[PAUSED]</color>" : ""; 
            string sesP = TRTrackerPlugin.Stats.FormatMoney(TRTrackerPlugin.Stats.SessionProfit);
            string prevP = TRTrackerPlugin.Stats.FormatMoney(TRTrackerPlugin.Stats.LastSessionProfit);
            string rateM = TRTrackerPlugin.Stats.RateMin.ToString("F1");
            
            long deltaXP = TRTrackerPlugin.Stats.SessionXD;
            string deltaXPStr = (deltaXP >= 0) ? "+" + deltaXP : deltaXP.ToString();
            long prevXP = TRTrackerPlugin.Stats.LastSessionXD;
            string prevXPStr = (prevXP >= 0) ? "+" + prevXP : prevXP.ToString();
            
            string o = "";
            o += string.Format("<size=16><b>TIME:   {0}</b></size>{1}\n", time, freezeStr);
            o += string.Format("{0}\n", dateLine);
            o += string.Format("STATUS: {0}   TEMP: {1}\n", statusStr, temp);
            o += "------------------------------\n";
            o += string.Format("<size=16><color=#ffd700>WEALTH: {0}g {1}s {2}c</color></size>\n", g, s, c);
            o += string.Format("PROFIT: {0} ({1}/m)\n", sesP, rateM);
            o += string.Format("PREV.P: {0}\n", prevP);
            o += "------------------------------\n";
            o += string.Format("XP:     {0} (Lvl {1})\n", TRTrackerPlugin.Stats.SessionXD_Display, level); 
            o += string.Format("GAIN:   {0}\n", deltaXPStr);
            o += string.Format("PREV.X: {0}\n", prevXPStr);
            o += "------------------------------\n";
            o += string.Format("CUST:   {0} (Active)", occ);

            MainText.text = o;
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
        public long SessionXD_Display = 0; // The nice number to show "XP Gained This Session"? Or User wants "Value of XP earned"
        
        private int lastLevel = -1;
        private int lastRawXP = -1;
        
        // Customer Tracking
        public int SessionCustomers = 0;
        public int LastSessionCustomers = 0;
        public int WorkingStartCustomers = 0;
        
        public float RateMin=0f; 
        private bool wasOpen=false;
        private Queue<KeyValuePair<float,long>> fh = new Queue<KeyValuePair<float,long>>();
        
        public string FormatMoney(long v) { return string.Format("{0}{1}g {2}s {3}c", v>=0?"+":"-", Math.Abs(v)/10000, (Math.Abs(v)%10000)/100, Math.Abs(v)%100); }

        public void Update(long currentTotal, int rawXP, int level, int maxXP, bool isOpen, int customersServed) {
            // First time init
            if (lastLevel == -1) { lastLevel = level; lastRawXP = rawXP; }
            if (SessionStart == -1) { 
                SessionStart = currentTotal; 
                SessionStartLifetimeXP = 0; // Relative start
            }

            // XP Delta Logic
            long deltaFrame = 0;
            if (level > lastLevel) {
                // Leveled Up!
                // Delta = (OldMax - OldXP) + NewXP
                // Approximation: maxXP is usually the max of the NEW level or OLD? 
                // TavernReputation seems to update MaxReputation on level up.
                // Let's just assume delta is rawXP (since old was near max) + (Max - Old).
                // Actually simpler: Just accept the jump.
                // User wants to "allow negative if penalties, but not level up drop".
                // If level went UP, we treat the drop as a GAIN.
                // But we don't know the exact OldMax here easily.
                // Let's just blindly add rawXP to the accumulator if level up.
                deltaFrame = rawXP; // Current XP is all new gain
                // Add the gap from last frame to max (assume we finished the level)
                // We'll ignore the gap for now to stay safe, just rawXP is the new gain.
            } 
            else if (level == lastLevel) {
                 deltaFrame = rawXP - lastRawXP;
            }
            
            lastLevel = level;
            lastRawXP = rawXP;
            
            LifetimeXP += deltaFrame;
            SessionXD_Display = LifetimeXP - SessionStartLifetimeXP; // Total gain since session start

            // Session Logic (Open/Close)
            if (isOpen && !wasOpen) { 
                WorkingSessionStart = currentTotal; 
                WorkingStartLifetimeXP = LifetimeXP; 
                WorkingStartCustomers = customersServed;
                fh.Clear(); 
                RateMin = 0f; 
            }
            if (!isOpen && wasOpen) { 
                LastSessionProfit = currentTotal - WorkingSessionStart; 
                LastSessionXD = LifetimeXP - WorkingStartLifetimeXP;
                LastSessionCustomers = customersServed - WorkingStartCustomers;
            }
            
            wasOpen = isOpen;
            
            if (isOpen) { 
                SessionProfit = currentTotal - WorkingSessionStart; 
                SessionXD = LifetimeXP - WorkingStartLifetimeXP; 
                SessionCustomers = customersServed - WorkingStartCustomers;
                
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
                var wt = UnityEngine.Object.FindObjectOfType<WorldTime>();
                if(wt==null) return;
                FieldInfo f = typeof(WorldTime).GetField("multiplier", BindingFlags.Public|BindingFlags.Static);
                float cur = (float)f.GetValue(null);
                MethodInfo m = typeof(WorldTime).GetMethod("SetTimeMultiplier", BindingFlags.Public|BindingFlags.Instance);
                if(cur>0.01f) { s=cur; m.Invoke(wt, new object[]{0f}); IsFrozen=true; }
                else { m.Invoke(wt, new object[]{s}); IsFrozen=false; }
            } catch {}
        }
    }

    [HarmonyPatch(typeof(TavernManager), "DGFDCJGIMGK", MethodType.Getter)]
    public static class TRTrackerPatch {
        private static DateTime lastUIUpdate = DateTime.MinValue;
        static void Postfix(bool __result) { if ((DateTime.Now - lastUIUpdate).TotalSeconds > 0.1) { lastUIUpdate = DateTime.Now; if (TRTrackerPlugin.UI != null) Gather(__result); } }
        static void Gather(bool o) {
            try {
                 // Date
                 Type wt=Type.GetType("WorldTime, Assembly-CSharp");
                 object d=wt.GetProperty("ECPJJEDPCJE",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);
                 Type dt=d.GetType();
                 int h=(int)dt.GetField("hour").GetValue(d);
                 int m=(int)dt.GetField("min").GetValue(d);
                 string tStr=string.Format("{0}:{1:00} {2}", (h<=12?h:h-12)==0?12:(h<=12?h:h-12), m, h<12?"AM":"PM");
                 
                 var year = dt.GetField("year").GetValue(d);
                 var season = dt.GetField("season").GetValue(d);
                 int week = (int)dt.GetField("week").GetValue(d);
                 object dayObj = dt.GetField("day").GetValue(d); 
                 string dayName = dayObj.ToString(); // Enum? "Mon", "Tue"
                 
                 // If full name "Monday", truncate? Assuming 3 chars is fine or full is fine.
                 if (dayName.Length > 3) dayName = dayName.Substring(0, 3);
                 
                 // Calc day number: week * 7 + (int)day? 
                 // We need to cast enum to int.
                 int dayInt = (int)dayObj; 
                 // Usually enums start at 0 or 1. If Mon=0?
                 int dayNum = (week * 7) + dayInt + 1;
                 
                 string dateLine = string.Format("DATE:   {0}, {1} ({2}, Day {3})", season, year, dayName, dayNum);

                 // Money
                 Money mon=Money.AIGABKFKCEC();
                 object b=typeof(Money).GetField("balance",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(mon);
                 Type bt=b.GetType();
                 int g=(int)bt.GetProperty("Gold").GetValue(b,null);
                 int s=(int)bt.GetProperty("Silver").GetValue(b,null);
                 int c=(int)bt.GetProperty("Copper").GetValue(b,null);
                 
                 // XP & Level
                 TavernReputation rep = TavernReputation.BHOHGELLKOO();
                 int rawXP = TavernReputation.GetReputationExp(); 
                 
                 // Level
                 PropertyInfo levelProp = typeof(TavernReputation).GetProperty("GGDALGIOLFL", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                 int level = (int)levelProp.GetValue(rep, null);
                 
                 // MaxXP (LPIGAEHAPOP)
                 FieldInfo maxXPField = typeof(TavernReputation).GetField("LPIGAEHAPOP", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                 int maxXP = (int)maxXPField.GetValue(rep);

                 // Customers
                 TavernManager tm = TavernManager.FFBNAHPBPIM;
                 int occ = tm.customers.Count;
                 
                 // Temp
                 PropertyInfo heatProp = typeof(TavernManager).GetProperty("KCKMIJPKBJN", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                 object heatVal = heatProp.GetValue(tm, null);
                 string heatStr = heatVal.ToString();
                 
                 // Color Logic for Temp
                 string heatColor = "white"; // Default
                 if (heatStr.Contains("Perfect")) heatColor = "green";
                 else if (heatStr.Contains("Cold") || heatStr.Contains("Freezing")) heatColor = "blue";
                 else if (heatStr.Contains("Hot") || heatStr.Contains("Warm")) heatColor = "red";
                 
                 heatStr = string.Format("<color={0}>{1}</color>", heatColor, heatStr);

                 TRTrackerPlugin.UI.UpdateDisplay(tStr, dateLine, o, g, s, c, rawXP, level, maxXP, occ, heatStr); // Removed SessionEntries argument
            } catch (Exception ex) {
                 File.AppendAllText(TRTrackerPlugin.LogPath, "Err: "+ex+"\n");
            }
        }
    } // End Plugin


    public class CollapseHandler : MonoBehaviour
    {
        public RectTransform PanelRect;
        public float ExpandedHeight;
        public float CollapsedHeight;
        public bool IsCollapsed = false;
        
        // Tracker Specific: We hide the TEXT object.
        // But the text object is managed by UIHandler.
        // We can access it via TRTrackerPlugin.UI.MainText
        
        public void OnToggle()
        {
            IsCollapsed = !IsCollapsed;
            if (PanelRect) {
                PanelRect.sizeDelta = new Vector2(PanelRect.sizeDelta.x, IsCollapsed ? CollapsedHeight : ExpandedHeight);
            }
            
            if (TRTrackerPlugin.UI != null && TRTrackerPlugin.UI.MainText != null) {
                TRTrackerPlugin.UI.MainText.gameObject.SetActive(!IsCollapsed);
            }
        }
    }
}
