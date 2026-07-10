using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TRStats
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("TravellersRest.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static string LogPath;

        // Configuration entries
        public static ConfigEntry<float> PlayerSpeedMultiplier;
        public static ConfigEntry<int> ExtraCustomerCapacity;
        public static ConfigEntry<float> PriceModifier;
        public static ConfigEntry<KeyCode> ToggleUIKey;

        // New toggles
        public static ConfigEntry<bool> InfiniteCoal;
        public static ConfigEntry<bool> InfiniteWater;
        public static ConfigEntry<bool> SkipMinePiecePoolPrewarm;

        void Awake()
        {
            Instance = this;

            // Setup logging
            string logDir = Path.Combine(Paths.GameRootPath, "ModLogs");
            Directory.CreateDirectory(logDir);
            LogPath = Path.Combine(logDir, "trstats_debug.txt");
            try { File.Delete(LogPath); } catch { }
            File.WriteAllText(LogPath, "TRStats 1.3.4\n");
            Logger.LogInfo("TRStats 1.3.4");

            // Initialize config
            PlayerSpeedMultiplier = Config.Bind("Player", "SpeedMultiplier", 1.0f,
                new ConfigDescription("Player movement speed multiplier", new AcceptableValueRange<float>(0.5f, 5.0f)));

            ExtraCustomerCapacity = Config.Bind("Tavern", "ExtraCustomerCapacity", 0,
                new ConfigDescription("Extra customer capacity bonus", new AcceptableValueRange<int>(-50, 100)));

            PriceModifier = Config.Bind("Tavern", "PriceModifier", 0f,
                new ConfigDescription("Price modifier percentage", new AcceptableValueRange<float>(-50f, 100f)));

            ToggleUIKey = Config.Bind("UI", "ToggleKey", KeyCode.F4,
                "Key to toggle the stats UI");

            // New toggles
            InfiniteCoal = Config.Bind("Cheats", "InfiniteCoal", false,
                "When enabled, fuel/coal is never consumed");

            InfiniteWater = Config.Bind("Cheats", "InfiniteWater", false,
                "When enabled, water buckets are never emptied");

            SkipMinePiecePoolPrewarm = Config.Bind("Compatibility", "SkipMinePiecePoolPrewarm", true,
                "Skips the updated game's mine-piece pool prewarm that can hard-crash while loading saves");

            // Initialize patch-specific config
            Patches.InitializeConfig(Config);

            // Cleanup old manager if exists
            var old = FindObjectOfType<StatsManager>();
            if (old) Destroy(old.gameObject);

            // Create manager
            GameObject go = new GameObject("TRStats_Manager");
            GameObject.DontDestroyOnLoad(go);
            go.AddComponent<StatsManager>();

            File.AppendAllText(LogPath, "Manager created\n");

            // Subscribe to scene loading
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            // Apply Harmony patches
            try { new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(); } catch (Exception ex) {
                File.AppendAllText(LogPath, "Harmony error: " + ex.Message + "\n");
            }
            Patches.LogPatchDiagnostics();
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try {
                File.AppendAllText(LogPath, "Scene loaded: " + scene.name + "\n");
                var mgr = FindObjectOfType<StatsManager>();
                if (mgr == null) {
                    File.AppendAllText(LogPath, "Manager null, recreating\n");
                    GameObject go = new GameObject("TRStats_Manager");
                    GameObject.DontDestroyOnLoad(go);
                    go.AddComponent<StatsManager>();
                } else {
                    mgr.ForceRecreate();
                }
            } catch (Exception ex) {
                File.AppendAllText(LogPath, "SceneLoad Err: " + ex.Message + "\n");
            }
        }
    }

    public class StatsManager : MonoBehaviour
    {
        private GameObject _uiObj;
        private RectTransform _panelRT;
        private GameObject _contentObj;
        private bool _showUI = true;
        private float _expandedHeight = 550f;
        private float _collapsedHeight = 35f;

        // UI Elements
        private Text _speedText;
        private Slider _speedSlider;
        private Text _capacityText;
        private Slider _capacitySlider;
        private Text _priceText;
        private Slider _priceSlider;
        private Text _workAvoidText;
        private Slider _workAvoidSlider;
        private Toggle _infiniteCoalToggle;
        private Toggle _infiniteWaterToggle;
        private Text _infoText;
        private Coroutine _loopCoroutine;
        private float _infoUpdateInterval = 0.5f;

        void Start()
        {
            try {
                File.AppendAllText(Plugin.LogPath, "StatsManager Start\n");
                CreateUI();
                EnsureGameLoop();
            } catch (Exception ex) {
                File.AppendAllText(Plugin.LogPath, "Start error: " + ex.Message + "\n");
            }
        }

        void OnEnable()
        {
            EnsureGameLoop();
        }

        void OnDisable()
        {
            if (_loopCoroutine != null)
            {
                StopCoroutine(_loopCoroutine);
                _loopCoroutine = null;
            }
        }

        public void ForceRecreate()
        {
            try {
                if (_uiObj == null) CreateUI();
                EnsureGameLoop();
            } catch { }
        }

        void EnsureGameLoop()
        {
            if (_loopCoroutine == null)
            {
                _loopCoroutine = StartCoroutine(GameLoop());
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(Plugin.ToggleUIKey.Value))
            {
                _showUI = !_showUI;
                if (_uiObj != null) _uiObj.SetActive(_showUI);
            }
        }

        IEnumerator GameLoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(_infoUpdateInterval);
            while (true)
            {
                if (_uiObj == null) CreateUI();
                if (_showUI && _uiObj != null && _uiObj.activeInHierarchy)
                {
                    UpdateInfoText();
                }
                yield return wait;
            }
        }

        void UpdateInfoText()
        {
            if (_infoText == null) return;
            try {
                string info = "";
                PlayerController player = PlayerController.GetPlayer(1);
                if (player != null)
                {
                    info = "Speed: " + player.speed.ToString("F2") + "  ";
                }

                try {
                    ReputationInfo repInfo = ReputationDBAccessor.GetReputation(TavernReputation.GetMilestoneMaster());
                    if (repInfo != null)
                    {
                        info += "Capacity: " + repInfo.customersCapacity;
                    }
                } catch { }

                _infoText.text = info;
            } catch { }
        }

        void CreateUI()
        {
            try {
                if (_uiObj != null) return;

                // Ensure EventSystem exists
                if (FindObjectOfType<EventSystem>() == null)
                {
                    GameObject es = new GameObject("TRStats_EventSystem");
                    es.AddComponent<EventSystem>();
                    es.AddComponent<StandaloneInputModule>();
                    GameObject.DontDestroyOnLoad(es);
                }

                _uiObj = new GameObject("TRStatsCanvas");
                GameObject.DontDestroyOnLoad(_uiObj);

                Canvas c = _uiObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 102;

                CanvasScaler cs = _uiObj.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);

                _uiObj.AddComponent<GraphicRaycaster>();

                // Panel
                GameObject panel = new GameObject("TRStatsPanel");
                panel.transform.SetParent(_uiObj.transform, false);

                Image border = panel.AddComponent<Image>();
                border.color = new Color(0.5f, 0.3f, 0.1f);
                border.raycastTarget = false;

                _panelRT = panel.GetComponent<RectTransform>();
                if (_panelRT == null) _panelRT = panel.AddComponent<RectTransform>();

                _panelRT.anchorMin = new Vector2(1, 1);
                _panelRT.anchorMax = new Vector2(1, 1);
                _panelRT.pivot = new Vector2(1, 1);
                _panelRT.anchoredPosition = new Vector2(-20, -20);
                _panelRT.sizeDelta = new Vector2(380, _expandedHeight);

                // Background
                GameObject bg = new GameObject("Background");
                bg.transform.SetParent(panel.transform, false);
                Image bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0.1f, 0.08f, 0.05f, 0.95f);
                bgImg.raycastTarget = true;
                bg.AddComponent<WindowPointerFocus>();
                RectTransform bgRT = bg.GetComponent<RectTransform>();
                if (bgRT == null) bgRT = bg.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = new Vector2(3, 3);
                bgRT.offsetMax = new Vector2(-3, -3);

                // Header
                GameObject header = new GameObject("Header");
                header.transform.SetParent(bg.transform, false);
                Image hImg = header.AddComponent<Image>();
                hImg.color = new Color(0.2f, 0.12f, 0.05f);
                RectTransform headerRT = header.GetComponent<RectTransform>();
                if (headerRT == null) headerRT = header.AddComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0, 1);
                headerRT.anchorMax = new Vector2(1, 1);
                headerRT.pivot = new Vector2(0.5f, 1);
                headerRT.anchoredPosition = Vector2.zero;
                headerRT.sizeDelta = new Vector2(0, 30);

                // Header Title
                GameObject hTitle = new GameObject("Title");
                hTitle.transform.SetParent(header.transform, false);
                Text ht = hTitle.AddComponent<Text>();
                ht.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                ht.text = "TR CHEATS 1.3.4 (F4)";
                ht.alignment = TextAnchor.MiddleCenter;
                ht.color = new Color(1f, 0.8f, 0.4f);
                ht.fontSize = 14;
                ht.raycastTarget = false;
                RectTransform htRT = hTitle.GetComponent<RectTransform>();
                if (htRT == null) htRT = hTitle.AddComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero;
                htRT.anchorMax = Vector2.one;
                htRT.sizeDelta = Vector2.zero;

                // Drag handler
                WindowDragger drag = header.AddComponent<WindowDragger>();
                drag.TargetRect = _panelRT;

                // Collapse Button
                GameObject collapseBtn = new GameObject("CollapseBtn");
                collapseBtn.transform.SetParent(header.transform, false);
                Image collapseBtnImg = collapseBtn.AddComponent<Image>();
                collapseBtnImg.color = new Color(0.18f, 0.28f, 0.18f, 1f);
                RectTransform collapseBtnRT = collapseBtn.GetComponent<RectTransform>();
                collapseBtnRT.anchorMin = new Vector2(1, 0.5f);
                collapseBtnRT.anchorMax = new Vector2(1, 0.5f);
                collapseBtnRT.pivot = new Vector2(1, 0.5f);
                collapseBtnRT.anchoredPosition = new Vector2(-5, 0);
                collapseBtnRT.sizeDelta = new Vector2(18, 18);

                Button collapseButton = collapseBtn.AddComponent<Button>();
                collapseButton.targetGraphic = collapseBtnImg;
                GameObject collapseLabelObj = new GameObject("Label");
                collapseLabelObj.transform.SetParent(collapseBtn.transform, false);
                Text collapseLabel = collapseLabelObj.AddComponent<Text>();
                collapseLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                collapseLabel.text = "-";
                collapseLabel.alignment = TextAnchor.MiddleCenter;
                collapseLabel.color = Color.white;
                collapseLabel.fontStyle = FontStyle.Bold;
                collapseLabel.raycastTarget = false;
                RectTransform collapseLabelRT = collapseLabelObj.GetComponent<RectTransform>();
                collapseLabelRT.anchorMin = Vector2.zero;
                collapseLabelRT.anchorMax = Vector2.one;
                collapseLabelRT.offsetMin = Vector2.zero;
                collapseLabelRT.offsetMax = Vector2.zero;
                CollapseHandler ch = collapseBtn.AddComponent<CollapseHandler>();
                ch.PanelRect = _panelRT;
                ch.ExpandedHeight = _expandedHeight;
                ch.CollapsedHeight = _collapsedHeight;
                ch.Label = collapseLabel;
                collapseButton.onClick.AddListener(ch.OnToggle);

                // --- SCROLL VIEW ---
                GameObject scrollObj = new GameObject("Scroll View");
                scrollObj.transform.SetParent(bg.transform, false);
                RectTransform scrollRT = scrollObj.AddComponent<RectTransform>();
                scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
                scrollRT.offsetMin = new Vector2(5, 25);
                scrollRT.offsetMax = new Vector2(-20, -35); // Margin for scrollbar
                
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

                // Content area (will be hidden when collapsed)
                _contentObj = new GameObject("Content");
                _contentObj.transform.SetParent(viewport.transform, false);
                RectTransform contentRT = _contentObj.AddComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0, 1);
                contentRT.anchorMax = new Vector2(1, 1);
                contentRT.pivot = new Vector2(0, 1);
                contentRT.sizeDelta = new Vector2(0, 0);
                contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = Vector2.zero;
                sr.content = contentRT;
                
                ContentSizeFitter csf = _contentObj.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                VerticalLayoutGroup vlg = _contentObj.AddComponent<VerticalLayoutGroup>();
                vlg.childControlHeight = true; vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

                // --- SCROLLBAR ---
                GameObject scrollbarObj = new GameObject("Scrollbar Vertical");
                scrollbarObj.transform.SetParent(bg.transform, false);
                RectTransform sbRT = scrollbarObj.AddComponent<RectTransform>();
                sbRT.anchorMin = new Vector2(1, 0); sbRT.anchorMax = new Vector2(1, 1);
                sbRT.pivot = new Vector2(1, 1);
                sbRT.anchoredPosition = new Vector2(-2, -35); 
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

                ch.ContentObj = scrollObj;

                float yPos = 0;

                // === PLAYER SECTION ===
                yPos = CreateSectionHeader(_contentObj.transform, "PLAYER", yPos);
                yPos = CreateSliderRow(_contentObj.transform, "Speed Multiplier:", 0.5f, 5f, Plugin.PlayerSpeedMultiplier.Value, yPos, out _speedText, out _speedSlider);
                _speedSlider.onValueChanged.AddListener(delegate { OnSpeedChanged(); });

                // === TAVERN SECTION ===
                yPos -= 5;
                yPos = CreateSectionHeader(_contentObj.transform, "TAVERN", yPos);
                yPos = CreateSliderRow(_contentObj.transform, "Extra Capacity:", -50, 100, Plugin.ExtraCustomerCapacity.Value, yPos, out _capacityText, out _capacitySlider);
                _capacitySlider.wholeNumbers = true;
                _capacitySlider.onValueChanged.AddListener(delegate { OnCapacityChanged(); });

                yPos = CreateSliderRow(_contentObj.transform, "Price Modifier %:", -50, 100, Plugin.PriceModifier.Value, yPos, out _priceText, out _priceSlider);
                _priceSlider.onValueChanged.AddListener(delegate { OnPriceChanged(); });

                // === EMPLOYEES SECTION ===
                yPos -= 5;
                yPos = CreateSectionHeader(_contentObj.transform, "EMPLOYEES", yPos);
                yPos = CreateSliderRow(_contentObj.transform, "Work Avoidance:", 0.1f, 3f, Patches.EmployeeWorkAvoidanceMultiplier.Value, yPos, out _workAvoidText, out _workAvoidSlider);
                _workAvoidSlider.onValueChanged.AddListener(delegate { OnWorkAvoidChanged(); });

                // === CHEATS SECTION ===
                yPos -= 5;
                yPos = CreateSectionHeader(_contentObj.transform, "CHEATS", yPos);
                yPos = CreateToggleRow(_contentObj.transform, "Infinite Coal/Fuel", Plugin.InfiniteCoal.Value, yPos, out _infiniteCoalToggle);
                _infiniteCoalToggle.onValueChanged.AddListener(delegate { Plugin.InfiniteCoal.Value = _infiniteCoalToggle.isOn; });

                yPos = CreateToggleRow(_contentObj.transform, "Infinite Water Bucket", Plugin.InfiniteWater.Value, yPos, out _infiniteWaterToggle);
                _infiniteWaterToggle.onValueChanged.AddListener(delegate { Plugin.InfiniteWater.Value = _infiniteWaterToggle.isOn; });

                // === ACTION BUTTONS ===
                yPos -= 10;
                yPos = CreateButton(_contentObj.transform, "Water All Crops", yPos, delegate { WaterAllCrops(); });
                yPos = CreateButton(_contentObj.transform, "Insta-Grow All Crops", yPos, delegate { InstaGrowAllCrops(); });
                yPos = CreateButton(_contentObj.transform, "Fill Animal Water", yPos, delegate { CareForAnimals(); });

                yPos -= 5;
                yPos = CreateButton(_contentObj.transform, "Apply Speed", yPos, delegate { ApplyChanges(); });
                yPos = CreateButton(_contentObj.transform, "Reset to Defaults", yPos, delegate { ResetToDefaults(); });

                // Info Text at bottom
                yPos -= 5;
                GameObject infoObj = new GameObject("InfoText");
                infoObj.transform.SetParent(_contentObj.transform, false);
                LayoutElement infoLayout = infoObj.AddComponent<LayoutElement>();
                infoLayout.minHeight = 90;
                _infoText = infoObj.AddComponent<Text>();
                _infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _infoText.text = "Loading...";
                _infoText.color = new Color(0.6f, 0.6f, 0.6f);
                _infoText.fontSize = 11;
                _infoText.alignment = TextAnchor.UpperLeft;
                _infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _infoText.verticalOverflow = VerticalWrapMode.Overflow;
                ContentSizeFitter infoFitter = infoObj.AddComponent<ContentSizeFitter>();
                infoFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                RectTransform infoRT = infoObj.GetComponent<RectTransform>();
                infoRT.anchorMin = new Vector2(0, 1);
                infoRT.anchorMax = new Vector2(1, 1);
                infoRT.pivot = new Vector2(0, 1);
                infoRT.anchoredPosition = new Vector2(10, yPos);
                infoRT.sizeDelta = new Vector2(-20, 20);

                // Resize Grip
                GameObject grip = new GameObject("ResizeGrip");
                grip.transform.SetParent(bg.transform, false);
                RectTransform gripRT = grip.AddComponent<RectTransform>();
                gripRT.anchorMin = new Vector2(1, 0);
                gripRT.anchorMax = new Vector2(1, 0);
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
                triRT.anchorMin = Vector2.zero;
                triRT.anchorMax = Vector2.one;
                triRT.sizeDelta = Vector2.zero;

                ResizeHandler rh = grip.AddComponent<ResizeHandler>();
                rh.PanelRect = _panelRT;

                File.AppendAllText(Plugin.LogPath, "UI Created Successfully!\n");

            } catch (Exception ex) {
                File.AppendAllText(Plugin.LogPath, "CreateUI Error: " + ex.ToString() + "\n");
            }
        }

        float CreateSectionHeader(Transform parent, string text, float yPos)
        {
            GameObject headerObj = new GameObject(text + "_Header");
            headerObj.transform.SetParent(parent, false);
            LayoutElement le = headerObj.AddComponent<LayoutElement>();
            le.minHeight = 20;

            Text headerText = headerObj.AddComponent<Text>();
            headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            headerText.text = "=== " + text + " ===";
            headerText.color = new Color(1f, 0.8f, 0.4f);
            headerText.fontSize = 12;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            RectTransform headerRT = headerObj.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, yPos);
            headerRT.sizeDelta = new Vector2(0, 20);
            return yPos - 22;
        }

        float CreateSliderRow(Transform parent, string label, float min, float max, float defaultVal, float yPos, out Text valueText, out Slider slider)
        {
            GameObject rowObj = new GameObject(label + "_Row");
            rowObj.transform.SetParent(parent, false);
            LayoutElement leRow = rowObj.AddComponent<LayoutElement>();
            leRow.minHeight = 45;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.text = label;
            labelText.color = new Color(0.9f, 0.85f, 0.7f);
            labelText.fontSize = 12;
            labelText.alignment = TextAnchor.MiddleLeft;
            RectTransform labelRT = labelObj.GetComponent<RectTransform>();
            if (labelRT == null) labelRT = labelObj.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 1);
            labelRT.anchorMax = new Vector2(0, 1);
            labelRT.pivot = new Vector2(0, 1);
            labelRT.anchoredPosition = new Vector2(10, -5);
            labelRT.sizeDelta = new Vector2(140, 20);

            GameObject valObj = new GameObject("Value");
            valObj.transform.SetParent(rowObj.transform, false);
            valueText = valObj.AddComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            valueText.text = defaultVal.ToString("F2");
            valueText.color = new Color(0.4f, 1f, 0.4f);
            valueText.fontSize = 12;
            valueText.alignment = TextAnchor.MiddleRight;
            RectTransform valRT = valObj.GetComponent<RectTransform>();
            if (valRT == null) valRT = valObj.AddComponent<RectTransform>();
            valRT.anchorMin = new Vector2(1, 1);
            valRT.anchorMax = new Vector2(1, 1);
            valRT.pivot = new Vector2(1, 1);
            valRT.anchoredPosition = new Vector2(-10, -5); 
            valRT.sizeDelta = new Vector2(70, 20);

            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(rowObj.transform, false);
            slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultVal;

            RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
            if (sliderRT == null) sliderRT = sliderObj.AddComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0, 0);
            sliderRT.anchorMax = new Vector2(1, 0);
            sliderRT.pivot = new Vector2(0.5f, 0);
            sliderRT.anchoredPosition = new Vector2(0, 5);
            sliderRT.sizeDelta = new Vector2(-20, 16);

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.15f, 0.1f);
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            if (bgRT == null) bgRT = bgObj.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            slider.targetGraphic = bgImg;

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1, 0.75f);
            fillAreaRT.sizeDelta = new Vector2(-10, 0);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.6f, 0.4f, 0.1f);
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            if (fillRT == null) fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;
            slider.fillRect = fillRT;

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero; handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.sizeDelta = new Vector2(-10, 0);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.9f, 0.7f, 0.3f);
            RectTransform handleRT = handle.GetComponent<RectTransform>();
            if (handleRT == null) handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(12, 0);
            slider.handleRect = handleRT;

            return yPos;
        }

        float CreateToggleRow(Transform parent, string label, bool defaultVal, float yPos, out Toggle toggle)
        {
            GameObject rowObj = new GameObject(label + "_Row");
            rowObj.transform.SetParent(parent, false);
            LayoutElement le = rowObj.AddComponent<LayoutElement>();
            le.minHeight = 25;
            RectTransform rowRT = rowObj.GetComponent<RectTransform>();
            if (rowRT == null) rowRT = rowObj.AddComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(1, 1);
            rowRT.pivot = new Vector2(0.5f, 1);
            rowRT.anchoredPosition = new Vector2(0, yPos);
            rowRT.sizeDelta = new Vector2(0, 25);

            // Toggle background
            GameObject toggleBg = new GameObject("Background");
            toggleBg.transform.SetParent(rowObj.transform, false);
            Image bgImg = toggleBg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.15f, 0.1f);
            RectTransform bgRT = toggleBg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.5f);
            bgRT.anchorMax = new Vector2(0, 0.5f);
            bgRT.pivot = new Vector2(0, 0.5f);
            bgRT.anchoredPosition = new Vector2(10, 0);
            bgRT.sizeDelta = new Vector2(22, 22);

            // Checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggleBg.transform, false);
            Image checkImg = checkmark.AddComponent<Image>();
            checkImg.color = new Color(0.4f, 1f, 0.4f);
            RectTransform checkRT = checkmark.GetComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.1f, 0.1f);
            checkRT.anchorMax = new Vector2(0.9f, 0.9f);
            checkRT.sizeDelta = Vector2.zero;

            // Toggle component
            toggle = rowObj.AddComponent<Toggle>();
            toggle.isOn = defaultVal;
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.text = label;
            labelText.color = new Color(0.9f, 0.85f, 0.7f);
            labelText.fontSize = 12;
            labelText.alignment = TextAnchor.MiddleLeft;
            RectTransform labelRT = labelObj.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = new Vector2(40, 0);
            labelRT.offsetMax = new Vector2(-10, 0);

            return yPos - 28;
        }

        float CreateButton(Transform parent, string text, float yPos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(text + "_Button");
            btnObj.transform.SetParent(parent, false);
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 28;

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.2f, 0.1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(onClick);

            RectTransform btnRT = btnObj.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0, 1);
            btnRT.anchorMax = new Vector2(1, 1);
            btnRT.pivot = new Vector2(0.5f, 1);
            btnRT.anchoredPosition = new Vector2(0, yPos);
            btnRT.sizeDelta = new Vector2(-20, 28);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            Text btnText = txtObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            btnText.text = text;
            btnText.color = new Color(1f, 0.9f, 0.6f);
            btnText.fontSize = 12;
            btnText.alignment = TextAnchor.MiddleCenter;
            RectTransform txtRT = txtObj.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;

            return yPos - 32;
        }

        void OnSpeedChanged()
        {
            if (_speedSlider == null || _speedText == null) return;
            Plugin.PlayerSpeedMultiplier.Value = _speedSlider.value;
            _speedText.text = _speedSlider.value.ToString("F2") + "x";
        }

        void OnCapacityChanged()
        {
            if (_capacitySlider == null || _capacityText == null) return;
            Plugin.ExtraCustomerCapacity.Value = (int)_capacitySlider.value;
            string sign = _capacitySlider.value >= 0 ? "+" : "";
            _capacityText.text = sign + ((int)_capacitySlider.value).ToString();
        }

        void OnPriceChanged()
        {
            if (_priceSlider == null || _priceText == null) return;
            Plugin.PriceModifier.Value = _priceSlider.value;
            string sign = _priceSlider.value >= 0 ? "+" : "";
            _priceText.text = sign + _priceSlider.value.ToString("F0") + "%";
        }

        void OnWorkAvoidChanged()
        {
            if (_workAvoidSlider == null || _workAvoidText == null) return;
            Patches.EmployeeWorkAvoidanceMultiplier.Value = _workAvoidSlider.value;
            _workAvoidText.text = _workAvoidSlider.value.ToString("F1") + "x";
        }

        void WaterAllCrops()
        {
            try {
                int count = 0;
                FertileSoil[] soils = FindObjectsOfType<FertileSoil>();
                foreach (FertileSoil soil in soils)
                {
                    if (soil != null)
                    {
                        soil.daysUntilDry = 5;
                        count++;
                    }
                }
                File.AppendAllText(Plugin.LogPath, "Watered " + count + " soil tiles\n");
            } catch (Exception ex) {
                File.AppendAllText(Plugin.LogPath, "WaterAllCrops Error: " + ex.Message + "\n");
            }
        }

        void CareForAnimals()
        {
            try {
                int water = 0;
                // Standard water bowls (cows, sheep, pigs, and chickens on a standard water feeder).
                // FindObjectsOfType returns an empty array when none exist, so a farm with only hen
                // houses (or no animals at all) is handled gracefully.
                AnimalFeederWater[] waters = FindObjectsOfType<AnimalFeederWater>();
                foreach (AnimalFeederWater feeder in waters)
                {
                    if (feeder == null) continue;
                    try {
                        // FillFeeder clamps to the feeder's maxAmount for its current level, so a
                        // large amount tops it off. The fill happens before the UI/online calls, so
                        // even if those throw, the water is still set.
                        feeder.FillFeeder(1, 9999);
                        water++;
                    } catch (Exception ex) {
                        File.AppendAllText(Plugin.LogPath, "CareForAnimals water feeder error: " + ex.Message + "\n");
                    }
                }

                // Hen-house water feeders. They have no public FillFeeder, so set currentAmount to the
                // max for the feeder's level and refresh the sprite, mirroring the game's own fill.
                int henWater = 0;
                AnimalFeederWaterHenHouse[] henHouses = FindObjectsOfType<AnimalFeederWaterHenHouse>();
                foreach (AnimalFeederWaterHenHouse feeder in henHouses)
                {
                    if (feeder == null) continue;
                    try {
                        int level = GetFeederLevel(feeder);
                        int[] maxAmount = feeder.maxAmount;
                        int max = (maxAmount != null && level >= 0 && level < maxAmount.Length) ? maxAmount[level] : 0;
                        if (max > 0) {
                            feeder.currentAmount = max;
                            if (feeder.farmBuilding != null) {
                                try { feeder.farmBuilding.UpdateAnimalsState(); } catch { }
                            }
                            feeder.UpdateSprite();
                            henWater++;
                        }
                    } catch (Exception ex) {
                        File.AppendAllText(Plugin.LogPath, "CareForAnimals hen-house error: " + ex.Message + "\n");
                    }
                }

                File.AppendAllText(Plugin.LogPath, "Care for animals: filled " + water + " water feeders and " + henWater + " hen-house water feeders\n");
            } catch (Exception ex) {
                File.AppendAllText(Plugin.LogPath, "CareForAnimals Error: " + ex.Message + "\n");
            }
        }

        private static FieldInfo _feederLevelField;
        private static int GetFeederLevel(AnimalFeeder feeder)
        {
            if (feeder == null) return 0;
            try {
                if (_feederLevelField == null) {
                    _feederLevelField = typeof(AnimalFeeder).GetField("_level", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_feederLevelField != null) return (int)_feederLevelField.GetValue(feeder);
            } catch { }
            return 0;
        }

        void InstaGrowAllCrops()
        {
            try {
                int count = 0;
                Growable[] growables = FindObjectsOfType<Growable>();
                foreach (Growable g in growables)
                {
                    if (g != null && !g.grown && !g.isDead)
                    {
                        // Get the crop setter to find max growth stage
                        CropSetter cropSetter = g.cropSetter;
                        if (cropSetter != null)
                        {
                            Crop crop = TRStatsReflection.GetInstancePropertyValueByType<Crop>(cropSetter);
                            if (crop != null && crop.growingSprites != null)
                            {
                                int maxStage = crop.growingSprites.Length - 1;

                                // Use reflection to set private _currentGrowState
                                FieldInfo growStateField = typeof(Growable).GetField("_currentGrowState", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (growStateField != null)
                                {
                                    growStateField.SetValue(g, maxStage);
                                }

                                g.grown = true;
                                g.daysPlanted = crop.daysToGrow;

                                // Make the crop harvestable
                                try {
                                    if (cropSetter.harvestable != null)
                                    {
                                        cropSetter.harvestable.isHarvestable = true;
                                    }
                                } catch { }

                                // Call SetUniqueCropHarvestable to update visual state
                                try {
                                    cropSetter.SetUniqueCropHarvestable();
                                } catch { }

                                // Update visuals
                                try {
                                    cropSetter.UpdateCropVisual(maxStage);
                                } catch { }

                                // Trigger OnGrow event
                                try {
                                    g.OnGrow(maxStage);
                                } catch { }

                                count++;
                            }
                        }
                    }
                }
                File.AppendAllText(Plugin.LogPath, "Grew " + count + " crops\n");
            } catch (Exception ex) {
                File.AppendAllText(Plugin.LogPath, "InstaGrowAllCrops Error: " + ex.Message + "\n");
            }
        }

        void ApplyChanges()
        {
            try {
                PlayerController player = PlayerController.GetPlayer(1);
                if (player != null)
                {
                    player.speed = 1f * Plugin.PlayerSpeedMultiplier.Value;
                }

                PlayerController player2 = PlayerController.GetPlayer(2);
                if (player2 != null)
                {
                    player2.speed = 1f * Plugin.PlayerSpeedMultiplier.Value;
                }

                File.AppendAllText(Plugin.LogPath, "Applied speed: " + Plugin.PlayerSpeedMultiplier.Value + "\n");
            } catch (Exception ex) {
                File.AppendAllText(Plugin.LogPath, "Apply Error: " + ex.Message + "\n");
            }
        }

        void ResetToDefaults()
        {
            Plugin.PlayerSpeedMultiplier.Value = 1.0f;
            Plugin.ExtraCustomerCapacity.Value = 0;
            Plugin.PriceModifier.Value = 0f;
            Patches.EmployeeWorkAvoidanceMultiplier.Value = 1.0f;
            Plugin.InfiniteCoal.Value = false;
            Plugin.InfiniteWater.Value = false;

            if (_speedSlider != null) _speedSlider.value = 1.0f;
            if (_capacitySlider != null) _capacitySlider.value = 0;
            if (_priceSlider != null) _priceSlider.value = 0f;
            if (_workAvoidSlider != null) _workAvoidSlider.value = 1.0f;
            if (_infiniteCoalToggle != null) _infiniteCoalToggle.isOn = false;
            if (_infiniteWaterToggle != null) _infiniteWaterToggle.isOn = false;

            PlayerController player = PlayerController.GetPlayer(1);
            if (player != null) player.speed = 1f;

            File.AppendAllText(Plugin.LogPath, "Reset to defaults\n");
        }
    }

    internal static class TRStatsReflection
    {
        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static T FindSingleton<T>() where T : class
        {
            Type type = typeof(T);

            foreach (PropertyInfo prop in type.GetProperties(AnyStatic))
            {
                if (prop.PropertyType != type || prop.GetIndexParameters().Length != 0) continue;
                try
                {
                    T value = prop.GetValue(null, null) as T;
                    if (value != null) return value;
                }
                catch { }
            }

            foreach (FieldInfo field in type.GetFields(AnyStatic))
            {
                if (field.FieldType != type) continue;
                try
                {
                    T value = field.GetValue(null) as T;
                    if (value != null) return value;
                }
                catch { }
            }

            return UnityEngine.Object.FindObjectOfType(type) as T;
        }

        public static T GetInstancePropertyValueByType<T>(object instance) where T : class
        {
            if (instance == null) return null;

            foreach (PropertyInfo prop in instance.GetType().GetProperties(AnyInstance))
            {
                if (prop.PropertyType != typeof(T) || prop.GetIndexParameters().Length != 0) continue;
                try
                {
                    T value = prop.GetValue(instance, null) as T;
                    if (value != null) return value;
                }
                catch { }
            }

            return null;
        }

        public static T GetInstanceFieldValueByType<T>(object instance) where T : class
        {
            if (instance == null) return null;

            foreach (FieldInfo field in instance.GetType().GetFields(AnyInstance))
            {
                if (field.FieldType != typeof(T)) continue;
                try
                {
                    T value = field.GetValue(instance) as T;
                    if (value != null) return value;
                }
                catch { }
            }

            return null;
        }

        public static FieldInfo FindInstanceFieldByType<TOwner, TValue>() where TValue : class
        {
            foreach (FieldInfo field in typeof(TOwner).GetFields(AnyInstance))
            {
                if (field.FieldType == typeof(TValue)) return field;
            }

            return null;
        }

        public static PropertyInfo FindInstancePropertyByType<TOwner, TValue>() where TValue : class
        {
            foreach (PropertyInfo prop in typeof(TOwner).GetProperties(AnyInstance))
            {
                if (prop.PropertyType == typeof(TValue) && prop.GetIndexParameters().Length == 0) return prop;
            }

            return null;
        }

        public static int GetCrafterFuel(Crafter crafter)
        {
            if (crafter == null) return 0;

            FieldInfo fuelField = crafter.GetType().GetField("fuel", AnyInstance);
            if (fuelField != null && fuelField.FieldType == typeof(int))
            {
                try { return (int)fuelField.GetValue(crafter); } catch { }
            }

            foreach (PropertyInfo prop in crafter.GetType().GetProperties(AnyInstance))
            {
                if (prop.PropertyType != typeof(int) || prop.GetIndexParameters().Length != 0) continue;
                try { return (int)prop.GetValue(crafter, null); } catch { }
            }

            return 0;
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

    public class ResizeHandler : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform PanelRect;
        public Vector2 MinSize = new Vector2(300, 200);
        public Vector2 MaxSize = new Vector2(600, 800);

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

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.trstats.mod";
        public const string PLUGIN_NAME = "TR Stats";
        public const string PLUGIN_VERSION = "1.3.4";
    }
}
