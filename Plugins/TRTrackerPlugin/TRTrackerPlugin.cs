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
    [BepInPlugin("com.lolaiur.trtracker", "Tavern Tracker", "1.2.0")]
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
             try { File.WriteAllText(LogPath, "TRTracker 1.2.0\n"); } catch { }
             
             // Cleanup old
             var old = FindObjectOfType<TrackerManager>();
             if (old) Destroy(old.gameObject);

             GameObject go = new GameObject("TRTracker_Manager");
             DontDestroyOnLoad(go);
             go.AddComponent<TrackerManager>();
             
             try { new Harmony("com.lolaiur.trtracker").PatchAll(); } catch {}
        }
    }

    public class TrackerManager : MonoBehaviour
    {
        public static GameObject UI_OBJ;
        public static UIHandler UI;
        public static StatsHandler Stats;
        public static TimeHandler TimeCtrl;
        
        void Awake() {
             Stats = new StatsHandler();
             TimeCtrl = new TimeHandler();
             SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
             try {
                if(scene.name == "Gameplay") {
                    TRTrackerPatch.ResetDump();
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
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) {
                if (UI_OBJ != null) UI_OBJ.SetActive(!UI_OBJ.activeSelf);
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
                panelRT.anchorMin = new Vector2(0, 1); panelRT.anchorMax = new Vector2(0, 1);
                panelRT.pivot = new Vector2(0, 1);
                panelRT.anchoredPosition = new Vector2(20, -120);
                panelRT.sizeDelta = new Vector2(360, 420); // Height increased for new stats

                // --- BACKGROUND ---
                GameObject bg = new GameObject("Background");
                bg.transform.SetParent(panel.transform, false);
                Image bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0.15f, 0.1f, 0.05f, 0.98f); 
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
                ht.text = "TAVERN TRACKER 1.2.0";
                ht.alignment = TextAnchor.MiddleCenter;
                ht.color = new Color(1f, 0.8f, 0.4f);
                ht.fontSize = 14;
                RectTransform htRT = hTitle.GetComponent<RectTransform>();
                if (htRT==null) htRT = hTitle.AddComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero; htRT.anchorMax = Vector2.one;
                htRT.sizeDelta = Vector2.zero;
                htRT.anchoredPosition3D = new Vector3(0, 0, -0.1f); 

                // --- COLLAPSE BUTTON ---
                GameObject btnObj = new GameObject("CollapseBtn");
                btnObj.transform.SetParent(header.transform, false);
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
                ch.ExpandedHeight = 420; 
                ch.CollapsedHeight = 35; 
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
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(10, 25);
                trt.offsetMax = new Vector2(-10, -35);

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
        public RectTransform ContentRect;
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

    public class UIHandler : MonoBehaviour
    {
        public Text MainText;
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

    [HarmonyPatch(typeof(TavernManager), "NLLHCAJBECF", MethodType.Getter)]
    public static class TRTrackerPatch {
        private static DateTime lastUIUpdate = DateTime.MinValue;
        private static bool hasDumpedTM = false;
        public static void ResetDump() { hasDumpedTM = false; }
        
        static void Postfix(bool __result) { if ((DateTime.Now - lastUIUpdate).TotalSeconds > 0.1) { lastUIUpdate = DateTime.Now; if (TrackerManager.UI != null) Gather(__result); } }
        static void Gather(bool o) {
            try {
                 // Date
                 Type wt=Type.GetType("WorldTime, Assembly-CSharp");
                 object d=wt.GetProperty("HPJLLDAAEGG",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);
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
                 TavernReputation rep = TavernReputation.OFDGCPAEGOM;
                 int rawXP = TavernReputation.GetReputationExp(); 
                 PropertyInfo levelProp = typeof(TavernReputation).GetProperty("EFHAKBCILMG", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                 int level = (int)levelProp.GetValue(rep, null);
                 FieldInfo maxXPField = typeof(TavernReputation).GetField("EICJODGJHPD", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                 int maxXP = (int)maxXPField.GetValue(rep);

                 // Customers (Active)
                 TavernManager tm = TavernManager.OFDGCPAEGOM;
                 int occ = tm.customers.Count;
                 
                 // Customers (Total Served) - For Session Stats
                 int totalServed = 0;
                 try {
                     var tsMgr = TavernServiceManager.OFDGCPAEGOM;
                     if (tsMgr != null) {
                         var statsList = tsMgr.GetAllTavernStats(); // Returns List<TavernStats> (tavernStats)
                         if (statsList != null && statsList.Count > 0) {
                             var todayStats = statsList[statsList.Count - 1]; // Last entry is today
                             totalServed = todayStats.customersCount;
                         }
                     }
                 } catch {}

                 // Temp
                 PropertyInfo heatProp = typeof(TavernManager).GetProperty("IIOJBDFPLBM", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                 object heatVal = heatProp.GetValue(tm, null);
                 string heatStr = heatVal.ToString();
                 string heatColor = "white"; 
                 if (heatStr.Contains("Perfect")) heatColor = "green";
                 else if (heatStr.Contains("Cold") || heatStr.Contains("Freezing")) heatColor = "blue";
                 else if (heatStr.Contains("Hot") || heatStr.Contains("Warm")) heatColor = "red";
                 heatStr = string.Format("<color={0}>{1}</color>", heatColor, heatStr);

                 // Dirt Level
                 PropertyInfo dirtProp = typeof(TavernManager).GetProperty("GMEAACPPLAK", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
                         var zoneMgr = TavernZonesManager.OFDGCPAEGOM;
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
        
        public void OnToggle()
        {
            IsCollapsed = !IsCollapsed;
            if (PanelRect) {
                PanelRect.sizeDelta = new Vector2(PanelRect.sizeDelta.x, IsCollapsed ? CollapsedHeight : ExpandedHeight);
            }
            
            if (TrackerManager.UI != null && TrackerManager.UI.MainText != null) {
                TrackerManager.UI.MainText.gameObject.SetActive(!IsCollapsed);
            }
        }
    }
}
