using System;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TRBarPlugin
{
    [BepInPlugin("com.lolaiur.trbar", "TRBar", "1.3.0")]
    [BepInProcess("TravellersRest.exe")]
    public class TRBarPlugin : BaseUnityPlugin
    {
        public static string LogPath;

        void Awake()
        {
            string logDir = Path.Combine(Paths.GameRootPath, "ModLogs");
            Directory.CreateDirectory(logDir);
            LogPath = Path.Combine(logDir, "bar_debug.txt");
            try { File.Delete(LogPath); } catch {}
            File.WriteAllText(LogPath, "TRBar 1.3.0\n");
            Logger.LogInfo("TRBar 1.3.0");
            
            // Cleanup old
            var old = FindObjectOfType<BarTrackerManager>();
            if (old) Destroy(old.gameObject);

            GameObject go = new GameObject("TRBar_Tracker");
            GameObject.DontDestroyOnLoad(go);
            go.AddComponent<BarTrackerManager>();
            
            var loadMsg = new GameObject("LoadMsg").AddComponent<LoadStatusUI>();
            loadMsg.ModName = "TRBar";
            DontDestroyOnLoad(loadMsg.gameObject);
            
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
             try {
                var mgr = FindObjectOfType<BarTrackerManager>();
                if (mgr == null) {
                    GameObject go = new GameObject("TRBar_Tracker");
                    GameObject.DontDestroyOnLoad(go);
                    go.AddComponent<BarTrackerManager>();
                } else {
                    mgr.ForceRecreate();
                }
             } catch (Exception ex) {
                 File.AppendAllText(LogPath, "SceneLoad Err: " + ex.Message + "\n");
             }
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

    public class BarTrackerManager : MonoBehaviour
    {
        // UI
        private GameObject _uiObj;
        private Text _mainText;
        private RectTransform _panelRT;
        private bool _showUI = true;
        
        // Data
        private class KegData
        {
            public string Name;
            public int Qty;
            public int Max;
            public float FlowRate; 
            public int Id;
            public int PreviousQty;
            public string Color;
        }

        private class FoodData
        {
            public string Name;
            public int Qty;
            public object ItemRef;
        }

        private List<KegData> _kegs = new List<KegData>();
        private List<FoodData> _food = new List<FoodData>();
        
        // Settings
        private float _updateInterval = 1.0f;
        private float _rescanInterval = 5.0f;
        private float _nextDispenserScanTime = 0f;
        private Coroutine _loopCoroutine;
        private DrinkDispenser[] _cachedDispensers = new DrinkDispenser[0];
        private FieldInfo _tavernOpenField;

        // Memory Optimization
        private System.Text.StringBuilder _sb = new System.Text.StringBuilder(512);


        
        void Start()
        {
            try {
                CreateUI();
                EnsureGameLoop();
            } catch {}
        }

        void OnEnable() { EnsureGameLoop(); }
        void OnDisable()
        {
            if (_loopCoroutine != null) {
                StopCoroutine(_loopCoroutine);
                _loopCoroutine = null;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                _showUI = !_showUI;
                if (_uiObj != null) _uiObj.SetActive(_showUI);
            }
        }

        public void ForceRecreate()
        {
            try {
                if (_uiObj == null) CreateUI();
                EnsureGameLoop();
            } catch {}
        }

        private void EnsureGameLoop()
        {
            if (_loopCoroutine == null) {
                _loopCoroutine = StartCoroutine(GameLoop());
            }
        }

        System.Collections.IEnumerator GameLoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(_updateInterval);
            while (true)
            {
                if (_uiObj == null) CreateUI();
                if (_showUI && _uiObj != null && _uiObj.activeInHierarchy) {
                    try {
                        ScanBar();
                        UpdateText();
                    } catch {}
                }
                yield return wait;
            }
        }

        private void CreateUI()
        {
            try {
                if (_uiObj != null) return;

                _uiObj = new GameObject("TRBarCanvas");
                DontDestroyOnLoad(_uiObj);

                Canvas c = _uiObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 103;

                CanvasScaler cs = _uiObj.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);

                _uiObj.AddComponent<GraphicRaycaster>();

                GameObject panel = new GameObject("TRBarPanel");
                panel.transform.SetParent(_uiObj.transform, false);

                Image border = panel.AddComponent<Image>();
                border.color = new Color(0.6f, 0.4f, 0.2f);
                border.raycastTarget = false;

                _panelRT = panel.GetComponent<RectTransform>();
                if (_panelRT == null) _panelRT = panel.AddComponent<RectTransform>();

                _panelRT.anchorMin = new Vector2(1, 1); _panelRT.anchorMax = new Vector2(1, 1);
                _panelRT.pivot = new Vector2(1, 1);
                _panelRT.anchoredPosition = new Vector2(-20, -600);
                _panelRT.sizeDelta = new Vector2(360, 400);

                GameObject bg = new GameObject("Background");
                bg.transform.SetParent(panel.transform, false);

                Image bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0.15f, 0.1f, 0.05f, 0.98f);
                bgImg.raycastTarget = true;
                bg.AddComponent<WindowPointerFocus>();

                RectTransform bgRT = bg.GetComponent<RectTransform>();
                if (bgRT == null) bgRT = bg.AddComponent<RectTransform>();

                bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = new Vector2(2, 2); bgRT.offsetMax = new Vector2(-2, -2);

                GameObject header = new GameObject("Header");
                header.transform.SetParent(bg.transform, false);

                Image hImg = header.AddComponent<Image>();
                hImg.color = new Color(0.25f, 0.15f, 0.05f, 1f);

                RectTransform headerRT = header.GetComponent<RectTransform>();
                if (headerRT == null) headerRT = header.AddComponent<RectTransform>();

                headerRT.anchorMin = new Vector2(0, 1); headerRT.anchorMax = new Vector2(1, 1);
                headerRT.pivot = new Vector2(0, 1);
                headerRT.anchoredPosition = Vector2.zero;
                headerRT.sizeDelta = new Vector2(0, 30);

                Font uiFont = null;
                try {
                    var fonts = Resources.FindObjectsOfTypeAll<Font>();
                    if (fonts != null && fonts.Length > 0) {
                        uiFont = fonts.FirstOrDefault(f => f.name == "Arial");
                        if (uiFont == null) uiFont = fonts[0];
                    }
                    if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                } catch {}

                try {
                    GameObject hTitle = new GameObject("Title");
                    hTitle.transform.SetParent(header.transform, false);
                    Text ht = hTitle.AddComponent<Text>();
                    if (uiFont != null) ht.font = uiFont;
                    ht.text = "TR BAR 1.3.0 (F3)";
                    ht.alignment = TextAnchor.MiddleCenter;
                    ht.color = new Color(1f, 0.8f, 0.4f);
                    ht.fontSize = 14;
                    ht.raycastTarget = false;

                    RectTransform htRT = hTitle.GetComponent<RectTransform>();
                    if (htRT == null) htRT = hTitle.AddComponent<RectTransform>();

                    htRT.anchorMin = Vector2.zero; htRT.anchorMax = Vector2.one;
                    htRT.offsetMin = Vector2.zero; htRT.offsetMax = Vector2.zero;
                } catch {}
                
                // Drag
                WindowDestroyer drag = header.AddComponent<WindowDestroyer>();
                drag.TargetMover = _panelRT;

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
                sbRT.sizeDelta = new Vector2(15, -60);
                
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

                // Text Content
                GameObject textObj = new GameObject("ContentText");
                textObj.transform.SetParent(content.transform, false); // Inside Scroll View
                _mainText = textObj.AddComponent<Text>();

                try {
                    Font con = Font.CreateDynamicFontFromOSFont("Consolas", 12);
                    if (con != null) _mainText.font = con;
                    else if (uiFont != null) _mainText.font = uiFont;
                }
                catch { if (uiFont != null) _mainText.font = uiFont; }

                _mainText.color = Color.white;
                _mainText.fontSize = 12;
                _mainText.alignment = TextAnchor.UpperLeft;
                _mainText.horizontalOverflow = HorizontalWrapMode.Overflow; 
                _mainText.verticalOverflow = VerticalWrapMode.Truncate;
                
                // Trt managed by VerticalLayoutGroup now

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
                rh.PanelRect = _panelRT;

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
                ch.PanelRect = _panelRT;
                ch.ContentObj = scrollObj; // Collapse the scroll view hierarchy
                ch.ExpandedHeight = 400;
                ch.CollapsedHeight = 35;
                btn.onClick.AddListener(ch.OnToggle);

                _mainText.text = "Waiting for data...";

            } catch (Exception ex) {
                try {
                    File.AppendAllText(TRBarPlugin.LogPath, "UI Err: " + ex.Message + "\n");
                    if (_uiObj != null) { Destroy(_uiObj); _uiObj = null; }
                } catch {}
            }
        }

        private void ScanBar()
        {
            try {
                // --- 1. Tavern Open State ---
                bool isOpen = false;
                if (TavernManager.GOKBJFAMHMJ != null)
                {
                    if (_tavernOpenField == null)
                    {
                        _tavernOpenField = typeof(TavernManager).GetField("_open", BindingFlags.Instance | BindingFlags.NonPublic);
                    }

                    if (_tavernOpenField != null)
                    {
                        isOpen = (bool)_tavernOpenField.GetValue(TavernManager.GOKBJFAMHMJ);
                    }
                }

                // --- 2. Track Taps (Kegs) ---
                var currentMap = _kegs.ToDictionary(k => k.Id, k => k);
                _kegs.Clear();

                if (_cachedDispensers.Length == 0 || Time.unscaledTime >= _nextDispenserScanTime)
                {
                    _cachedDispensers = FindObjectsOfType<DrinkDispenser>();
                    _nextDispenserScanTime = Time.unscaledTime + _rescanInterval;
                }

                var barInv = BarMenuInventory.GetInstance();

                foreach (var d in _cachedDispensers)
                {
                    if (d == null || !d.isActiveAndEnabled || d.slots == null || d.slots.Length == 0 || d.slots[0].itemInstance == null) {
                        continue;
                    }

                    var slot = d.slots[0];
                    int uniqueId = d.GetInstanceID();

                    int qty = 0;
                    string name = "Unknown";
                    string colorHex = "#FFFFFF";
                    
                    try {
                        FieldInfo fSpriteColor = d.GetType().GetField("_spriteColor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fSpriteColor != null) {
                            object scObj = fSpriteColor.GetValue(d);
                            if (scObj != null) {
                                var cField = scObj.GetType().GetField("color", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (cField != null) {
                                    Color c = (Color)cField.GetValue(scObj);
                                    colorHex = "#" + ColorUtility.ToHtmlStringRGB(c);
                                }
                            }
                        }
                    } catch {}

                    var keg = slot.itemInstance as OldKegInstance;

                    if (keg != null) {
                         qty = keg.beersLeft;
                         name = GetItemName(keg);
                    } else if (slot.itemInstance is FoodInstance) {
                         name = GetItemName(slot.itemInstance);
                         int stack = 1;
                         try {
                              FieldInfo fStack = slot.GetType().GetField("stack", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); 
                              if (fStack != null) stack = (int)fStack.GetValue(slot);
                              else {
                                  PropertyInfo pStack = slot.GetType().GetProperty("Stack");
                                  if (pStack != null) stack = (int)pStack.GetValue(slot, null);
                              }
                         } catch {}
                         qty = stack;
                    } else {
                        continue;
                    }
                    
                    if (string.IsNullOrEmpty(name) || name == "Unknown") {
                         string raw = slot.itemInstance.ToString();
                         if (raw.Contains("-")) {
                             var parts = raw.Split('-');
                             if (parts.Length > 1) name = parts[1].Trim();
                             if (name.EndsWith("(Food)")) name = name.Replace("(Food)", "").Trim();
                         }
                    }

                    if (!string.IsNullOrEmpty(name)) {
                        if (name.Contains(" - ")) {
                            var parts = name.Split(new string[]{" - "}, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1) name = parts[parts.Length - 1].Trim();
                        }
                        if (name.EndsWith("(Food)")) name = name.Replace("(Food)", "").Trim();
                    }
                    if (string.IsNullOrEmpty(name)) name = "Unknown";

                    float rate = 0;
                    KegData oldData;
                    if (currentMap.TryGetValue(uniqueId, out oldData))
                    {
                        int diff = oldData.PreviousQty - qty;
                        if (diff > 0 && isOpen && diff < 20) rate = diff * (60f / _updateInterval);
                        
                        if (diff == 0) rate = Mathf.Lerp(oldData.FlowRate, 0f, 0.1f);
                        else rate = Mathf.Lerp(oldData.FlowRate, rate, 0.5f);

                        oldData.PreviousQty = qty;
                        oldData.Qty = qty;
                        oldData.FlowRate = rate;
                        oldData.Name = name;
                        oldData.Color = colorHex; 
                        _kegs.Add(oldData);
                    }
                    else
                    {
                        _kegs.Add(new KegData { Id = uniqueId, Name = name, Qty = qty, Max = 20, PreviousQty = qty, FlowRate = 0, Color = colorHex });
                    }
                }
                _kegs.Sort((a,b) => a.Name.CompareTo(b.Name));

                // --- 3. Track Food ---
                _food.Clear();
                // barInv is already initialized above for the dump logic
                if (barInv != null && barInv.slots != null)
                {
                    var counts = new Dictionary<string, FoodData>();
                    foreach (var slot in barInv.slots)
                    {
                        if (slot != null && slot.itemInstance != null)
                        {
                            string name = GetItemName(slot.itemInstance);

                            if (!string.IsNullOrEmpty(name)) {
                                if (name.Contains(" - ")) {
                                    var parts = name.Split(new string[]{" - "}, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length > 1) name = parts[parts.Length - 1].Trim();
                                }
                                if (name.EndsWith("(Food)")) name = name.Replace("(Food)", "").Trim();
                            }

                            if (string.IsNullOrEmpty(name)) name = "Unknown";
                            
                            // Get Stack Size
                            int stack = 1;
                            try {
                                  var t = slot.GetType();
                                  FieldInfo f = t.GetField("stack", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                  if (f != null) stack = (int)f.GetValue(slot);
                                  else {
                                      PropertyInfo p = t.GetProperty("Stack");
                                      if (p != null) stack = (int)p.GetValue(slot, null);
                                  }
                            } catch {}

                            // Aggregation with ItemRef
                            if (counts.ContainsKey(name)) {
                                counts[name].Qty += stack;
                            } else {
                                counts[name] = new FoodData { Name = name, Qty = stack, ItemRef = slot.itemInstance };
                            }
                        }
                    }
                    foreach (var foodData in counts.Values) _food.Add(foodData);
                }

            } catch (Exception ex) {
                 File.AppendAllText(TRBarPlugin.LogPath, "Scan Err: " + ex.Message + "\n");
            }
        }


        private void UpdateText()
        {
            if (_mainText == null) return;
            
            try {
                _sb.Clear();
                _sb.AppendLine("<size=13><b>TAPS (Flow/min)</b></size>");
                if (_kegs.Count == 0) _sb.AppendLine("No Taps Found");
                
                foreach (var k in _kegs)
                {
                    string c = "white";
                    if (k.Qty >= 20) c = "green";
                    else if (k.Qty >= 11) c = "cyan";
                    else if (k.Qty >= 5) c = "white";
                    else c = "red";
                    
                    string qty = k.Qty > 0 ? k.Qty.ToString() : "EMPTY";
                    
                    // Add Color Dot (Bigger)
                    string dot = "";
                    if (!string.IsNullOrEmpty(k.Color)) {
                        dot = "<size=18><color=" + k.Color + ">●</color></size> ";
                    }

                    _sb.AppendLine(string.Format("{0}{1,-15} <color={2}>{3,5}</color> {4,5:F1}", dot, k.Name.Length>15?k.Name.Substring(0,14):k.Name, c, qty, k.FlowRate));
                }
                
                _sb.AppendLine();
                _sb.AppendLine("--------------------------");
                _sb.AppendLine("<size=13><b>FOOD</b></size>");
                foreach (var f in _food)
                {
                     if (f.Name == "Unknown" || f.Qty <= 0) continue;
                     
                     // Translate known Spanish names if they slip through
                     string dispName = GetItemName(f.ItemRef);
                     if ((string.IsNullOrEmpty(dispName) || dispName == "Unknown") && !string.IsNullOrEmpty(f.Name)) {
                         dispName = f.Name; 
                     }
                     _sb.AppendLine(string.Format("{0,-20} {1,5}", dispName.Length>20?dispName.Substring(0,19):dispName, f.Qty));
                }
                _mainText.text = _sb.ToString();
            } catch {
                _mainText.text = "Error";
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

    public class WindowPointerFocus : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
    {
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData data)
        {
            WindowLayerUtil.BringToFront(this);
        }
    }

    /// <summary>Handles window dragging via header.</summary>
    public class WindowDestroyer : MonoBehaviour, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IPointerDownHandler
    {
        public RectTransform TargetMover;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData data) {
            WindowLayerUtil.BringToFront(this);
        }
        public void OnDrag(UnityEngine.EventSystems.PointerEventData data) {
            WindowLayerUtil.BringToFront(this);
            if (TargetMover) TargetMover.anchoredPosition += data.delta;
        }
    }

    /// <summary>Handles window resizing via bottom-right corner grip.</summary>
    public class ResizeHandler : MonoBehaviour, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IPointerDownHandler
    {
        public RectTransform PanelRect;
        public Vector2 MinSize = new Vector2(200, 150);
        public Vector2 MaxSize = new Vector2(800, 800);

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData data) {
            WindowLayerUtil.BringToFront(this);
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData data) {
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
