using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TRShared;

namespace TRAutoloaderPlugin
{
    [BepInPlugin("com.lolaiur.trautoloader", "TR Autoloader", "2.0.0")]
    [BepInProcess("TravellersRest.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static string LogPath;

        public static ConfigEntry<KeyCode> ToggleUIKey;
        public static ConfigEntry<bool> AutoLoadEnabled;
        public static ConfigEntry<float> AutoloaderIntervalSeconds;
        public static ConfigEntry<string> FoodLoaderGuid;
        public static ConfigEntry<string> DrinkLoaderGuid;
        public static ConfigEntry<bool> VerboseDebug;

        void Awake()
        {
            Instance = this;

            string logDir = Path.Combine(Paths.GameRootPath, "ModLogs");
            Directory.CreateDirectory(logDir);
            LogPath = Path.Combine(logDir, "autoload_debug.txt");
            // Append (do not wipe) so test data survives game restarts for diagnosis.
            try { File.Delete(LogPath); } catch { }
            File.WriteAllText(LogPath, "TR Autoloader 2.0.0\n");
            Logger.LogInfo("TR Autoloader 2.0.0");

            ToggleUIKey = Config.Bind("UI", "ToggleKey", KeyCode.F5,
                "Key to toggle the autoloader UI");

            AutoLoadEnabled = Config.Bind("Autoloaders", "Enabled", true,
                "Master switch for auto food/drink loading (use the panel toggle to change it at runtime)");

            AutoloaderIntervalSeconds = Config.Bind("Autoloaders", "IntervalSeconds", 6f,
                new ConfigDescription("Seconds between autoloader refill checks",
                    new AcceptableValueRange<float>(2f, 30f)));

            FoodLoaderGuid = Config.Bind("Autoloaders", "FoodLoaderGuid", string.Empty,
                "Persistent placeable GUID for the assigned food/barback loader container");

            DrinkLoaderGuid = Config.Bind("Autoloaders", "DrinkLoaderGuid", string.Empty,
                "Persistent placeable GUID for the assigned drink/keg loader container");

            VerboseDebug = Config.Bind("Debug", "VerboseDrinkLog", false,
                "Log per-target drink loading detail. Useful for diagnosis, adds some lag. Restart after changing.");

            BarAutoloaderService.NotifySceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            var old = FindObjectOfType<AutoloaderManager>();
            if (old) Destroy(old.gameObject);

            GameObject go = new GameObject("TRAutoloader_Manager");
            GameObject.DontDestroyOnLoad(go);
            go.AddComponent<AutoloaderManager>();

            var loadMsg = new GameObject("LoadMsg").AddComponent<LoadStatusUI>();
            loadMsg.ModName = "TR Autoloader";
            DontDestroyOnLoad(loadMsg.gameObject);

            File.AppendAllText(LogPath, "Manager created\n");

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try {
                File.AppendAllText(LogPath, "Scene loaded: " + scene.name + "\n");
                BarAutoloaderService.NotifySceneLoaded(scene.name);
                var mgr = FindObjectOfType<AutoloaderManager>();
                if (mgr == null) {
                    GameObject go = new GameObject("TRAutoloader_Manager");
                    GameObject.DontDestroyOnLoad(go);
                    go.AddComponent<AutoloaderManager>();
                } else {
                    mgr.ForceRecreate();
                }
            } catch (Exception ex) {
                File.AppendAllText(LogPath, "SceneLoad Err: " + ex.Message + "\n");
            }
        }
    }

    public class LoadStatusUI : MonoBehaviour
    {
        public string ModName = "";
        private float alpha = 1f;
        void OnGUI()
        {
            if (alpha <= 0) { Destroy(this.gameObject); return; }
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20; style.fontStyle = FontStyle.Bold;

            GUI.color = new Color(0, 0, 0, alpha);
            GUI.Label(new Rect(21, 21, 400, 50), ModName + " Loaded!", style);
            GUI.color = new Color(0.2f, 1f, 0.2f, alpha);
            GUI.Label(new Rect(20, 20, 400, 50), ModName + " Loaded!", style);

            alpha -= Time.deltaTime / 5f;
        }
    }

    public class AutoloaderManager : MonoBehaviour
    {
        private GameObject _uiObj;
        private RectTransform _panelRT;
        private GameObject _contentObj;
        private bool _showUI = true;
        private float _expandedHeight = 460f;
        private float _collapsedHeight = 35f;

        private Text _infoText;
        private Toggle _enabledToggle;
        private Coroutine _loopCoroutine;
        private float _infoUpdateInterval = 0.5f;

        void Start()
        {
            try {
                File.AppendAllText(Plugin.LogPath, "AutoloaderManager Start\n");
                CreateUI();
                EnsureGameLoop();
            } catch (Exception ex) {
                File.AppendAllText(Plugin.LogPath, "Start error: " + ex.Message + "\n");
            }
        }

        void OnEnable() { EnsureGameLoop(); }

        void OnDisable()
        {
            if (_loopCoroutine != null) {
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
            if (_loopCoroutine == null) {
                _loopCoroutine = StartCoroutine(GameLoop());
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(Plugin.ToggleUIKey.Value)) {
                _showUI = !_showUI;
                if (_uiObj != null) _uiObj.SetActive(_showUI);
            }
        }

        IEnumerator GameLoop()
        {
            while (true) {
                if (_uiObj == null) CreateUI();
                BarAutoloaderService.TryRun();
                if (_showUI && _uiObj != null && _uiObj.activeInHierarchy) {
                    UpdateInfoText();
                }
                yield return new WaitForSecondsRealtime(_infoUpdateInterval);
            }
        }

        void UpdateInfoText()
        {
            if (_infoText == null) return;
            try {
                string info = "Loading: " + (Plugin.AutoLoadEnabled.Value ? "<color=#66ff66>ON</color>" : "<color=#ff6666>OFF</color>");
                info += "\nInterval: " + Plugin.AutoloaderIntervalSeconds.Value.ToString("F1") + "s";
                info += "\nFood Loader: " + BarAutoloaderService.GetFoodLoaderSummary();
                info += "\nDrink Loader: " + BarAutoloaderService.GetDrinkLoaderSummary();
                info += "\nLast: " + BarAutoloaderService.GetLastStatusSummary();
                info += BarAutoloaderService.GetRecentActionsText();
                _infoText.text = info;
            } catch { }
        }

        void CreateUI()
        {
            try {
                if (_uiObj != null) return;

                if (FindObjectOfType<EventSystem>() == null) {
                    GameObject es = new GameObject("TRAutoloader_EventSystem");
                    es.AddComponent<EventSystem>();
                    es.AddComponent<StandaloneInputModule>();
                    GameObject.DontDestroyOnLoad(es);
                }

                _uiObj = new GameObject("TRAutoloaderCanvas");
                GameObject.DontDestroyOnLoad(_uiObj);

                Canvas c = _uiObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 103;

                CanvasScaler cs = _uiObj.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);

                _uiObj.AddComponent<GraphicRaycaster>();

                // Panel
                GameObject panel = new GameObject("TRAutoloaderPanel");
                panel.transform.SetParent(_uiObj.transform, false);

                Image border = panel.AddComponent<Image>();
                border.color = new Color(0.5f, 0.3f, 0.1f);
                border.raycastTarget = false;

                _panelRT = panel.GetComponent<RectTransform>();
                if (_panelRT == null) _panelRT = panel.AddComponent<RectTransform>();

                _panelRT.anchorMin = new Vector2(1, 0);
                _panelRT.anchorMax = new Vector2(1, 0);
                _panelRT.pivot = new Vector2(1, 0);
                _panelRT.anchoredPosition = new Vector2(-20, 20);
                _panelRT.sizeDelta = new Vector2(340, _expandedHeight);

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

                GameObject hTitle = new GameObject("Title");
                hTitle.transform.SetParent(header.transform, false);
                Text ht = hTitle.AddComponent<Text>();
                ht.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                ht.text = "TR AUTOLOADER 2.0.0 (F5)";
                ht.alignment = TextAnchor.MiddleCenter;
                ht.color = new Color(1f, 0.8f, 0.4f);
                ht.fontSize = 14;
                ht.raycastTarget = false;
                RectTransform htRT = hTitle.GetComponent<RectTransform>();
                if (htRT == null) htRT = hTitle.AddComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero;
                htRT.anchorMax = Vector2.one;
                htRT.sizeDelta = Vector2.zero;

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

                // Content — anchored below the header so it never overlaps (and never blocks) the
                // draggable title bar.
                _contentObj = new GameObject("Content");
                _contentObj.transform.SetParent(bg.transform, false);
                RectTransform contentRT = _contentObj.AddComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0, 0);
                contentRT.anchorMax = new Vector2(1, 1);
                contentRT.pivot = new Vector2(0.5f, 0.5f);
                contentRT.offsetMin = new Vector2(5, 5);
                contentRT.offsetMax = new Vector2(-5, -34);

                VerticalLayoutGroup vlg = _contentObj.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.spacing = 4;
                vlg.childControlHeight = true; vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

                ch.ContentObj = _contentObj;

                float yPos = 0;
                yPos = CreateToggleRow(_contentObj.transform, "Auto-Loading Enabled", Plugin.AutoLoadEnabled.Value, yPos, out _enabledToggle);
                _enabledToggle.onValueChanged.AddListener(delegate { Plugin.AutoLoadEnabled.Value = _enabledToggle.isOn; });
                yPos = CreateSectionHeader(_contentObj.transform, "LOADERS", yPos);
                yPos = CreateButton(_contentObj.transform, "Assign Nearby Food Loader", yPos, delegate { AssignFoodLoader(); });
                yPos = CreateButton(_contentObj.transform, "Assign Nearby Drink Loader", yPos, delegate { AssignDrinkLoader(); });
                yPos = CreateButton(_contentObj.transform, "Clear Food Loader", yPos, delegate { ClearFoodLoader(); });
                yPos = CreateButton(_contentObj.transform, "Clear Drink Loader", yPos, delegate { ClearDrinkLoader(); });

                yPos -= 5;
                GameObject infoObj = new GameObject("InfoText");
                infoObj.transform.SetParent(_contentObj.transform, false);
                LayoutElement infoLayout = infoObj.AddComponent<LayoutElement>();
                infoLayout.minHeight = 160;
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
            return yPos - 22;
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

        float CreateToggleRow(Transform parent, string label, bool defaultVal, float yPos, out Toggle toggle)
        {
            GameObject rowObj = new GameObject(label + "_Row");
            rowObj.transform.SetParent(parent, false);
            LayoutElement le = rowObj.AddComponent<LayoutElement>();
            le.minHeight = 26;

            GameObject toggleBg = new GameObject("Background");
            toggleBg.transform.SetParent(rowObj.transform, false);
            Image bgImg = toggleBg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.15f, 0.1f);
            RectTransform bgRT = toggleBg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.5f);
            bgRT.anchorMax = new Vector2(0, 0.5f);
            bgRT.pivot = new Vector2(0, 0.5f);
            bgRT.anchoredPosition = new Vector2(8, 0);
            bgRT.sizeDelta = new Vector2(22, 22);

            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggleBg.transform, false);
            Image checkImg = checkmark.AddComponent<Image>();
            checkImg.color = new Color(0.4f, 1f, 0.4f);
            RectTransform checkRT = checkmark.GetComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.1f, 0.1f);
            checkRT.anchorMax = new Vector2(0.9f, 0.9f);
            checkRT.sizeDelta = Vector2.zero;

            toggle = rowObj.AddComponent<Toggle>();
            toggle.isOn = defaultVal;
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;

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
            labelRT.offsetMin = new Vector2(38, 0);
            labelRT.offsetMax = new Vector2(-8, 0);

            return yPos - 30;
        }

        void AssignFoodLoader()
        {
            BarAutoloaderService.AssignNearestFoodLoader();
            UpdateInfoText();
        }

        void AssignDrinkLoader()
        {
            BarAutoloaderService.AssignNearestDrinkLoader();
            UpdateInfoText();
        }

        void ClearFoodLoader()
        {
            BarAutoloaderService.ClearFoodLoader();
            UpdateInfoText();
        }

        void ClearDrinkLoader()
        {
            BarAutoloaderService.ClearDrinkLoader();
            UpdateInfoText();
        }
    }

    internal static class AutoloaderReflection
    {
        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static T FindSingleton<T>() where T : class
        {
            Type type = typeof(T);

            foreach (PropertyInfo prop in type.GetProperties(AnyStatic)) {
                if (prop.PropertyType != type || prop.GetIndexParameters().Length != 0) continue;
                try {
                    T value = prop.GetValue(null, null) as T;
                    if (value != null) return value;
                }
                catch { }
            }

            foreach (FieldInfo field in type.GetFields(AnyStatic)) {
                if (field.FieldType != type) continue;
                try {
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

            foreach (PropertyInfo prop in instance.GetType().GetProperties(AnyInstance)) {
                if (prop.PropertyType != typeof(T) || prop.GetIndexParameters().Length != 0) continue;
                try {
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

            foreach (FieldInfo field in instance.GetType().GetFields(AnyInstance)) {
                if (field.FieldType != typeof(T)) continue;
                try {
                    T value = field.GetValue(instance) as T;
                    if (value != null) return value;
                }
                catch { }
            }

            return null;
        }

        public static FieldInfo FindInstanceFieldByType<TOwner, TValue>() where TValue : class
        {
            foreach (FieldInfo field in typeof(TOwner).GetFields(AnyInstance)) {
                if (field.FieldType == typeof(TValue)) return field;
            }

            return null;
        }

        public static PropertyInfo FindInstancePropertyByType<TOwner, TValue>() where TValue : class
        {
            foreach (PropertyInfo prop in typeof(TOwner).GetProperties(AnyInstance)) {
                if (prop.PropertyType == typeof(TValue) && prop.GetIndexParameters().Length == 0) return prop;
            }

            return null;
        }
    }

    public static class BarAutoloaderService
    {
        private const float MaxAssignDistance = 4.5f;
        private const float GameplayWarmupSeconds = 10f;
        private const float AssignmentDelaySeconds = 1.5f;
        private const float DispenserRescanSeconds = 8f;
        private const float DrinkRejectCooldownSeconds = 120f;
        private const int MaxDrinkTargetsPerTick = 12;
        private const int MaxFoodMovesPerTick = 8;
        private const int MaxDrinkUnitsPerTick = 20;
        private static bool VerboseDrinkDebug;

        private static float _nextRunTime;
        private static float _nextDispenserScanTime;
        private static float _nextFoodIdleLogTime;
        private static float _nextDrinkIdleLogTime;
        private static float _nextNoSourceLogTime;
        private static string _lastStatus = "Idle";
        private static float _lastStatusTime = -999f;
        private const int ActionFeedCapacity = 6;
        private static readonly List<string> _actionFeed = new List<string>(ActionFeedCapacity);
        private static ItemContainer _cachedFoodLoader;
        private static ItemContainer _cachedDrinkLoader;
        private static DrinkDispenser[] _cachedDispensers = new DrinkDispenser[0];
        private static BanquetBarrel[] _cachedBanquetBarrels = new BanquetBarrel[0];
        private static TavernZonesManager _cachedZoneManager;
        private static readonly HashSet<long> _drinkCompatibilityCache = new HashSet<long>();
        private static readonly Dictionary<long, float> _drinkRejectCooldowns = new Dictionary<long, float>();
        private static readonly Dictionary<string, MethodInfo> _itemCloneMethodCache = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> _itemFactoryMethodCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, FieldInfo> _priceFieldCache = new Dictionary<Type, FieldInfo>();
        private static readonly FieldInfo ItemContainerPlaceableField = typeof(ItemContainer).GetField("_placeable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ItemInstanceItemField = typeof(ItemInstance).GetField("item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ItemIdField = typeof(Item).GetField("id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ContainerUIPlaceableField = AutoloaderReflection.FindInstanceFieldByType<ContainerUI, Placeable>();
        private static readonly PropertyInfo ContainerUICurrentContainerProperty = AutoloaderReflection.FindInstancePropertyByType<ContainerUI, Container>();
        private static readonly MethodInfo PlaceableUniqueIdGetter = typeof(Placeable).GetMethod("get_uniqueId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo ItemContainerPlaceableProperty = AutoloaderReflection.FindInstancePropertyByType<ItemContainer, Placeable>();

        public static void NotifySceneLoaded(string sceneName)
        {
            _cachedFoodLoader = null;
            _cachedDrinkLoader = null;
            _cachedDispensers = new DrinkDispenser[0];
            _cachedBanquetBarrels = new BanquetBarrel[0];
            _cachedZoneManager = null;
            _nextDispenserScanTime = 0f;
            _nextFoodIdleLogTime = 0f;
            _nextDrinkIdleLogTime = 0f;
            _drinkCompatibilityCache.Clear();
            _drinkRejectCooldowns.Clear();
            _nextRunTime = string.Equals(sceneName, "Gameplay", StringComparison.Ordinal)
                ? Time.unscaledTime + GameplayWarmupSeconds
                : 0f;
        }

        public static void AssignNearestFoodLoader()
        {
            AssignNearestLoader(Plugin.FoodLoaderGuid, "Food");
        }

        public static void AssignNearestDrinkLoader()
        {
            AssignNearestLoader(Plugin.DrinkLoaderGuid, "Drink");
        }

        public static void ClearFoodLoader()
        {
            Plugin.FoodLoaderGuid.Value = string.Empty;
            _cachedFoodLoader = null;
            SetStatus("Food loader cleared.");
        }

        public static void ClearDrinkLoader()
        {
            Plugin.DrinkLoaderGuid.Value = string.Empty;
            _cachedDrinkLoader = null;
            SetStatus("Drink loader cleared.");
        }

        public static string GetFoodLoaderSummary()
        {
            return GetLoaderSummary(Plugin.FoodLoaderGuid.Value, ref _cachedFoodLoader);
        }

        public static string GetDrinkLoaderSummary()
        {
            return GetLoaderSummary(Plugin.DrinkLoaderGuid.Value, ref _cachedDrinkLoader);
        }

        public static string GetLastStatusSummary()
        {
            if (string.IsNullOrEmpty(_lastStatus)) return "<color=#bbbbbb>Idle</color>";
            if (_lastStatusTime < 0f) return _lastStatus;
            return string.Format("{0} <size=10>({1:0}s ago)</size>", _lastStatus, Mathf.Max(0f, Time.unscaledTime - _lastStatusTime));
        }

        public static void TryRun()
        {
            try {
                if (Plugin.AutoLoadEnabled != null && !Plugin.AutoLoadEnabled.Value) return;
                // In multiplayer, only the host (master client) runs the loader so transfers happen
                // once and propagate to the other player, instead of both clients duplicating them.
                if (OnlineManager.PlayingOnline() && !OnlineManager.IsMasterClient()) return;

                VerboseDrinkDebug = Plugin.VerboseDebug != null && Plugin.VerboseDebug.Value;

                float interval = Plugin.AutoloaderIntervalSeconds != null ? Mathf.Max(1f, Plugin.AutoloaderIntervalSeconds.Value) : 6f;
                if (Time.unscaledTime < _nextRunTime) return;
                _nextRunTime = Time.unscaledTime + interval;

                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gameplay") return;

                int foodMoved = RunFoodLoader();
                int drinkMoved = RunDrinkLoader();
                if (foodMoved > 0 || drinkMoved > 0) {
                    SetStatus(string.Format("Loaded {0} food and {1} drink unit{2}.",
                        foodMoved,
                        drinkMoved,
                        drinkMoved == 1 ? string.Empty : "s"));
                }
            }
            catch (Exception ex) {
                Log("Autoloader tick error: " + ex);
            }
        }

        private static void AssignNearestLoader(ConfigEntry<string> entry, string label)
        {
            ItemContainer activeContainer = TryGetOpenContainer();
            if (IsAssignableContainer(activeContainer)) {
                string key = BuildContainerKey(activeContainer);
                entry.Value = key;
                CacheLoaderReference(entry, activeContainer);
                ScheduleNextRun(AssignmentDelaySeconds);
                SetStatus(string.Format("{0} loader assigned to open {1}.", label, DescribeContainer(activeContainer)));
                return;
            }

            PlayerController player = FindLocalPlayer();
            if (player == null) {
                SetStatus(label + " loader assignment failed: no local player found.");
                return;
            }

            ItemContainer bestContainer = null;
            float bestDistance = float.MaxValue;

            foreach (ItemContainer container in UnityEngine.Object.FindObjectsOfType<ItemContainer>()) {
                if (!IsAssignableContainer(container)) continue;

                float distance = Vector2.Distance(player.transform.position, container.transform.position);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    bestContainer = container;
                }
            }

            if (bestContainer == null || bestDistance > MaxAssignDistance) {
                SetStatus(string.Format("No nearby assignable container found within {0:0.0}m.", MaxAssignDistance));
                return;
            }

            entry.Value = BuildContainerKey(bestContainer);
            CacheLoaderReference(entry, bestContainer);
            ScheduleNextRun(AssignmentDelaySeconds);
            SetStatus(string.Format("{0} loader assigned to {1}.", label, DescribeContainer(bestContainer)));
        }

        private static int RunFoodLoader()
        {
            ItemContainer loader = ResolveLoaderByKey(Plugin.FoodLoaderGuid.Value, ref _cachedFoodLoader);
            // The food loader stocks the shared bar menu, so its room does not matter — only that
            // the container is live. (Drink loaders are still zone-checked via IsLoaderActive.)
            if (loader == null || !loader.isActiveAndEnabled) return 0;

            BarMenuInventory barInventory = BarMenuInventory.GetInstance();
            if (barInventory == null || barInventory.slots == null) {
                LogFoodIdle("Food tick skipped: bar menu inventory unavailable.");
                return 0;
            }

            Dictionary<int, int> currentCounts = GetFoodCounts(barInventory);
            HashSet<int> preferredIds = new HashSet<int>();
            foreach (KeyValuePair<int, int> pair in currentCounts) preferredIds.Add(pair.Key);

            if (preferredIds.Count == 0) {
                Slot fallback = FindBestFoodSourceSlot(loader, null, null, barInventory);
                int fallbackId = GetItemId(fallback != null ? fallback.itemInstance : null);
                if (fallbackId <= 0) {
                    LogFoodIdle("Food tick skipped: no valid food source in loader. Sources=" + DescribeContainerSources(loader) + ".");
                    return 0;
                }
                preferredIds.Add(fallbackId);
                currentCounts[fallbackId] = 0;
            }

            int moved = 0;
            bool foundSource = false;
            bool transferFailed = false;
            while (moved < MaxFoodMovesPerTick) {
                Slot sourceSlot = FindBestFoodSourceSlot(loader, preferredIds, currentCounts, barInventory);
                if (sourceSlot == null) {
                    sourceSlot = FindBestFoodSourceSlot(loader, null, null, barInventory);
                    if (sourceSlot == null) break;
                }
                foundSource = true;

                int itemId = GetItemId(sourceSlot.itemInstance);
                if (itemId <= 0) break;

                if (!TryMoveOneItem(loader, barInventory, sourceSlot)) {
                    transferFailed = true;
                    break;
                }

                moved++;
                int currentCount;
                currentCounts.TryGetValue(itemId, out currentCount);
                currentCounts[itemId] = currentCount + 1;
            }

            // Smart-fill: stock any empty bar-menu slots with a priority-chosen food (special event
            // item first, then highest revenue), one different food per slot for diversity.
            if (moved < MaxFoodMovesPerTick) {
                bool halloweenActive = IsHalloweenActive();
                HashSet<int> placedIds = new HashSet<int>();
                while (moved < MaxFoodMovesPerTick) {
                    Slot source = PickBestSourceSlot(loader, false, halloweenActive, placedIds);
                    if (source == null) break;
                    if (!TryMoveOneItem(loader, barInventory, source)) break;
                    placedIds.Add(GetItemId(source.itemInstance));
                    moved++;
                }
            }

            if (moved == 0) {
                if (transferFailed) {
                    LogFoodIdle("Food tick idle: matching food source was found, but transfer failed.");
                }
                else if (!foundSource) {
                    LogFoodIdle("Food tick idle: no valid loader food could fit the bar menu. Menu=" + DescribeFoodCounts(currentCounts) + ". Sources=" + DescribeContainerSources(loader) + ".");
                }
                else {
                    LogFoodIdle("Food tick idle: no compatible bar menu slot needed loader food. Menu=" + DescribeFoodCounts(currentCounts) + ". Sources=" + DescribeContainerSources(loader) + ".");
                }
            }

            return moved;
        }

        private static int RunDrinkLoader()
        {
            string loaderKey = Plugin.DrinkLoaderGuid.Value;
            if (string.IsNullOrEmpty(loaderKey)) {
                LogDrinkDebug("Drink tick skipped: no drink loader assigned.");
                return 0;
            }

            ItemContainer loader = ResolveLoaderByKey(loaderKey, ref _cachedDrinkLoader);
            if (loader == null) {
                LogDrinkDebug("Drink tick skipped: loader unresolved for key " + loaderKey + ".");
                return 0;
            }

            if (!IsLoaderActive(loader)) {
                LogDrinkDebug("Drink tick skipped: loader inactive or outside dining room: " + DescribeContainer(loader));
                return 0;
            }

            DrinkDispenser[] dispensers = GetCachedDispensers();
            BanquetBarrel[] barrels = GetCachedBanquetBarrels();
            if ((dispensers == null || dispensers.Length == 0) && (barrels == null || barrels.Length == 0)) {
                LogDrinkDebug("Drink tick skipped: no drink dispensers or bar barrels found.");
                return 0;
            }

            Dictionary<int, int> activeDrinkCounts = GetActiveDrinkCounts(dispensers, barrels);
            int sourceSlots = CountSourceSlots(loader);
            int usableTargets = 0;
            if (VerboseDrinkDebug) {
                LogDrinkDebug(string.Format("Drink tick start. Loader={0}. SourceSlots={1}. Sources={2}. Dispensers={3}. Barrels={4}. ActiveTypes={5}.",
                    DescribeContainer(loader),
                    sourceSlots,
                    DescribeContainerSources(loader),
                    dispensers != null ? dispensers.Length : 0,
                    barrels != null ? barrels.Length : 0,
                    activeDrinkCounts.Count));
            }
            int unitsMoved = 0;
            int targetsAttempted = 0;

            if (dispensers != null) {
                foreach (DrinkDispenser dispenser in dispensers) {
                    if (unitsMoved >= MaxDrinkUnitsPerTick) break;
                    if (targetsAttempted >= MaxDrinkTargetsPerTick) break;
                    if (!IsDrinkTargetUsable(dispenser)) {
                        if (VerboseDrinkDebug) {
                            TavernZone sz = GetTavernZone(dispenser.transform.position);
                            Slot sds = GetDispenserSlot(dispenser);
                            LogDrinkDebug(string.Format("Skipping dispenser at {0}: isBeerTap={1} zone={2} targetSlot={3}",
                                FormatPosition(dispenser.transform.position),
                                dispenser.isBeerTap,
                                sz != null ? sz.zoneType.ToString() : "none",
                                sds == null ? "null" : DescribeSlotStack(sds)));
                        }
                        continue;
                    }

                    usableTargets++;
                    targetsAttempted++;
                    unitsMoved += FillDrinkTarget(
                        loader,
                        dispenser,
                        GetDispenserSlot(dispenser),
                        activeDrinkCounts,
                        MaxDrinkUnitsPerTick - unitsMoved,
                        dispenser.isBeerTap ? "tap" : "service barrel");
                }
            }

            if (barrels != null) {
                foreach (BanquetBarrel barrel in barrels) {
                    if (unitsMoved >= MaxDrinkUnitsPerTick) break;
                    if (targetsAttempted >= MaxDrinkTargetsPerTick) break;
                    if (!IsDrinkTargetUsable(barrel)) {
                        if (VerboseDrinkDebug) LogDrinkDebug("Skipping bar barrel outside dining room at " + FormatPosition(barrel.transform.position) + ".");
                        continue;
                    }

                    usableTargets++;
                    targetsAttempted++;
                    int movedNow = FillDrinkTarget(loader, barrel, barrel.slots[0], activeDrinkCounts, MaxDrinkUnitsPerTick - unitsMoved, "bar barrel slot 0");
                    unitsMoved += movedNow;
                    if (movedNow <= 0 && barrel.slots.Length > 1 && unitsMoved < MaxDrinkUnitsPerTick) {
                        unitsMoved += FillDrinkTarget(loader, barrel, barrel.slots[1], activeDrinkCounts, MaxDrinkUnitsPerTick - unitsMoved, "bar barrel slot 1");
                    }
                }
            }

            // Smart-fill: fill still-empty dispenser/keg slots with a priority-chosen drink
            // (special event item first, then highest revenue), one different drink per dispenser
            // for diversity. Uses AddItemInstance (not direct slot transfer) so the game properly
            // registers the item — direct slot transfer caused duplication where served drinks were
            // not consumed, giving players extra drinks.
            if (unitsMoved < MaxDrinkUnitsPerTick && dispensers != null) {
                bool halloweenActive = IsHalloweenActive();
                HashSet<int> placedIds = new HashSet<int>();
                foreach (DrinkDispenser dispenser in dispensers) {
                    if (unitsMoved >= MaxDrinkUnitsPerTick) break;
                    if (!IsDrinkTargetUsable(dispenser)) continue;
                    Slot slot = GetDispenserSlot(dispenser);
                    if (slot == null || slot.itemInstance != null) continue; // only empty target slots
                    Slot source = PickBestSourceSlot(loader, true, halloweenActive, placedIds);
                    if (source == null) break;
                    if (TryMoveOneItem(loader, dispenser, source)) {
                        placedIds.Add(GetItemId(source.itemInstance));
                        unitsMoved++;
                    }
                    // Don't break on failure — a different source may work for the next dispenser.
                }
            }

            if (VerboseDrinkDebug) {
                LogDrinkDebug(string.Format("Drink tick end. UsableTargets={0}. UnitsMoved={1}.",
                    usableTargets,
                    unitsMoved));
            }
            if (unitsMoved == 0) {
                LogDrinkIdle(string.Format("Drink tick idle: targets={0}, attempted={1}, sources={2}, sourceList={3}.",
                    usableTargets,
                    targetsAttempted,
                    sourceSlots,
                    DescribeContainerSources(loader)));
            }
            return unitsMoved;
        }

        private static int FillDrinkTarget(ItemContainer loader, Container target, Slot drinkSlot, Dictionary<int, int> activeDrinkCounts, int maxUnits, string targetLabel)
        {
            if (VerboseDrinkDebug)
            {
                LogDrinkDebug(string.Format("FillDrinkTarget {0} {1} drinkSlot={2} maxUnits={3}",
                    targetLabel,
                    FormatPosition(target != null ? target.transform.position : Vector3.zero),
                    drinkSlot == null ? "null" : DescribeSlotStack(drinkSlot),
                    maxUnits));
            }
            if (loader == null || target == null || drinkSlot == null || maxUnits <= 0) return 0;

            ItemInstance currentDrink = drinkSlot.itemInstance;
            int currentId = GetItemId(currentDrink);
            int currentAmount = GetDrinkAmount(drinkSlot);
            int currentMax = GetDrinkTargetMax(target, currentDrink);
            bool wasActive = currentDrink != null && currentAmount > 0;
            // Only top off a dispenser that already holds a drink. We never pour a different drink
            // into a dispenser, and we no longer guess a drink for an empty one (that was filling
            // empty taps with whatever else was active, e.g. beer into a wine tap). Leave a drink in
            // a dispenser and the autoloader keeps it stocked from the loader.
            bool shouldTopOff = currentDrink != null && (currentAmount <= 0 || (currentMax > 0 && currentAmount < currentMax));
            if (!shouldTopOff) {
                if (VerboseDrinkDebug) {
                    LogDrinkDebug(string.Format("{0} {1} already full with {2} ({3}/{4}).",
                        targetLabel,
                        FormatPosition(target.transform.position),
                        DescribeSlotStack(drinkSlot),
                        currentAmount,
                        currentMax));
                }
                return 0;
            }

            bool directSlotTransfer = UsesDirectDrinkSlotTransfer(target);
            Slot sourceSlot = FindBestDrinkSourceSlot(loader, target, currentId, wasActive, activeDrinkCounts, !directSlotTransfer);
            if (sourceSlot == null) {
                LogDrinkNoSource(target, currentDrink, loader);
                if (VerboseDrinkDebug) {
                    LogDrinkDebug(string.Format("No source drink found for {0} {1}. PreferredId={2}. Sources={3}.",
                        targetLabel,
                        FormatPosition(target.transform.position),
                        currentId,
                        DescribeContainerSources(loader)));
                }
                return 0;
            }

            int candidateId = GetItemId(sourceSlot.itemInstance);
            if (candidateId <= 0) return 0;

            if (drinkSlot.itemInstance != null && currentAmount <= 0) {
                Slot removedEmpty = target.RemoveItemInstance(drinkSlot.itemInstance, true);
                if (removedEmpty == null) {
                    if (VerboseDrinkDebug) LogDrinkDebug("Failed to clear empty item from " + targetLabel + " at " + FormatPosition(target.transform.position) + ".");
                    return 0;
                }
            }

            int targetMax = GetDrinkTargetMax(target, sourceSlot.itemInstance);
            if (targetMax <= 0) return 0;

            int amountNeeded = Mathf.Min(targetMax - GetDrinkAmount(drinkSlot), maxUnits);
            if (amountNeeded <= 0) return 0;

            int movedNow = TryMoveItemUnits(loader, target, drinkSlot, sourceSlot, amountNeeded, directSlotTransfer);
            if (movedNow <= 0) return 0;

            if (!wasActive) {
                int currentCount;
                activeDrinkCounts.TryGetValue(candidateId, out currentCount);
                activeDrinkCounts[candidateId] = currentCount + 1;
            }

            if (VerboseDrinkDebug) {
                LogDrinkDebug(string.Format("Loaded {0} {1} with {2}; units moved={3}.",
                    targetLabel,
                    FormatPosition(target.transform.position),
                    DescribeItemInstance(sourceSlot.itemInstance),
                    movedNow));
            }

            // Direct slot writes (drink dispensers/kegs) bypass AddItemInstance, so sync the slot
            // explicitly so the other player sees the fill in multiplayer.
            if (movedNow > 0) SyncSlot(drinkSlot);

            return movedNow;
        }

        private static bool UsesDirectDrinkSlotTransfer(Container target)
        {
            // All drink dispensers (taps and kegs) and bar barrels use direct slot transfer. The
            // normal AddItemInstance path is rejected by the dispenser's item filters for cloned
            // items (it returns null even when CanFitItems says there is room), so topping off by
            // incrementing the existing slot stack is the reliable path and matches how the game
            // itself fills dispensers.
            return target is DrinkDispenser || target is BanquetBarrel;
        }

        // Beer taps pour from slots[0]; service barrels (kegs) keep their drink in slots[1]. The game
        // itself fills those exact slots (DrinkDispenser references slots[0] for taps, slots[1] for
        // service barrels), so we must target the matching slot or the keg never fills.
        private static Slot GetDispenserSlot(DrinkDispenser dispenser)
        {
            if (dispenser == null || dispenser.slots == null || dispenser.slots.Length == 0) return null;
            if (dispenser.isBeerTap) return dispenser.slots[0];
            return dispenser.slots.Length > 1 ? dispenser.slots[1] : dispenser.slots[0];
        }

        private static Slot FindBestFoodSourceSlot(ItemContainer loader, HashSet<int> preferredIds, Dictionary<int, int> currentCounts, Container target)
        {
            if (loader == null || loader.slots == null) return null;

            Slot bestSlot = null;
            int bestCount = int.MaxValue;
            int bestValue = int.MinValue;

            foreach (Slot slot in loader.slots) {
                FoodInstance food = slot != null ? slot.itemInstance as FoodInstance : null;
                if (food == null || slot.Stack <= 0) continue;

                int itemId = GetItemId(food);
                if (itemId <= 0) continue;
                if (preferredIds != null && preferredIds.Count > 0 && !preferredIds.Contains(itemId)) continue;
                if (target != null && !CanContainerFitItem(target, food)) continue;

                int currentCount = 0;
                if (currentCounts != null) currentCounts.TryGetValue(itemId, out currentCount);

                int value = GetInstanceValue(food);
                if (bestSlot == null || currentCount < bestCount || (currentCount == bestCount && value > bestValue)) {
                    bestSlot = slot;
                    bestCount = currentCount;
                    bestValue = value;
                }
            }

            return bestSlot;
        }

        private static Slot FindBestDrinkSourceSlot(ItemContainer loader, Container target, int preferredId, bool exactOnly, Dictionary<int, int> activeDrinkCounts, bool requireCompatibility)
        {
            if (loader == null || target == null) return null;

            if (preferredId > 0) {
                HashSet<int> preferredIds = new HashSet<int>();
                preferredIds.Add(preferredId);
                Slot exactMatch = FindBestCompatibleDrinkSourceSlot(loader, target, preferredIds, activeDrinkCounts, requireCompatibility);
                if (exactMatch != null) return exactMatch;
            }

            if (exactOnly) return null;

            if (activeDrinkCounts != null && activeDrinkCounts.Count > 0) {
                HashSet<int> activeIds = new HashSet<int>();
                foreach (KeyValuePair<int, int> pair in activeDrinkCounts) activeIds.Add(pair.Key);

                Slot activeMatch = FindBestCompatibleDrinkSourceSlot(loader, target, activeIds, activeDrinkCounts, requireCompatibility);
                if (activeMatch != null) return activeMatch;
            }

            return FindBestCompatibleDrinkSourceSlot(loader, target, null, null, requireCompatibility);
        }

        private static Slot FindBestCompatibleDrinkSourceSlot(ItemContainer loader, Container target, HashSet<int> allowedIds, Dictionary<int, int> currentCounts, bool requireCompatibility)
        {
            if (loader == null || target == null || loader.slots == null) return null;

            Slot bestSlot = null;
            int bestCount = int.MaxValue;
            int bestValue = int.MinValue;

            foreach (Slot slot in loader.slots) {
                if (slot == null || slot.itemInstance == null || slot.Stack <= 0) continue;
                if (!IsLooseDrinkInstance(slot.itemInstance)) continue;

                int itemId = GetItemId(slot.itemInstance);
                if (itemId <= 0) continue;
                if (allowedIds != null && allowedIds.Count > 0 && !allowedIds.Contains(itemId)) continue;
                if (requireCompatibility && !CanDispenseItem(target, slot.itemInstance)) continue;

                int currentCount = 0;
                if (currentCounts != null) currentCounts.TryGetValue(itemId, out currentCount);

                int value = GetInstanceValue(slot.itemInstance);
                if (bestSlot == null || currentCount < bestCount || (currentCount == bestCount && value > bestValue)) {
                    bestSlot = slot;
                    bestCount = currentCount;
                    bestValue = value;
                }
            }

            return bestSlot;
        }

        private static int TryMoveItemUnits(Container source, Container target, Slot targetSlot, Slot sourceSlot, int amount, bool directSlotTransfer)
        {
            int moved = 0;
            if (amount <= 0) return moved;

            while (moved < amount) {
                if (sourceSlot == null || sourceSlot.itemInstance == null || sourceSlot.Stack <= 0) break;
                if (targetSlot != null) {
                    ItemInstance targetInstance = targetSlot != null && targetSlot.itemInstance != null ? targetSlot.itemInstance : sourceSlot.itemInstance;
                    int currentAmount = GetDrinkAmount(targetSlot);
                    int targetMax = GetDrinkTargetMax(target, targetInstance);
                    if (targetMax > 0 && currentAmount >= targetMax) break;
                }
                if (directSlotTransfer) {
                    if (!TryMoveOneItemToSlot(source, target, targetSlot, sourceSlot)) break;
                }
                else if (!TryMoveOneItem(source, target, sourceSlot)) break;

                moved++;
            }

            return moved;
        }

        private static bool TryMoveOneItemToSlot(Container source, Container target, Slot targetSlot, Slot sourceSlot)
        {
            if (source == null || targetSlot == null || sourceSlot == null || sourceSlot.itemInstance == null || sourceSlot.Stack <= 0) return false;

            ItemInstance clone = CloneItemInstance(sourceSlot.itemInstance);
            if (clone == null) {
                Log("Transfer failed: could not clone " + DescribeItemInstance(sourceSlot.itemInstance) + ".");
                return false;
            }

            int sourceItemId = GetItemId(sourceSlot.itemInstance);
            int targetItemId = GetItemId(targetSlot.itemInstance);
            if (targetItemId > 0 && sourceItemId > 0 && targetItemId != sourceItemId) return false;

            ItemInstance previousInstance = targetSlot.itemInstance;
            int previousStack = targetSlot.Stack;
            try {
                if (previousInstance == null || previousStack <= 0) {
                    targetSlot.itemInstance = clone;
                    clone.currentSlot = targetSlot;
                    targetSlot.Stack = 1;
                }
                else {
                    targetSlot.Stack = previousStack + 1;
                }

                targetSlot.OnItemAdded();
                targetSlot.OnItemInstanceChange();
                targetSlot.OnItemAddedWithItem(targetSlot.itemInstance);
                targetSlot.isDirty = true;
            }
            catch {
                RestoreDirectDrinkSlot(targetSlot, previousInstance, previousStack);
                return false;
            }

            Slot removedSlot = source.RemoveItemInstance(sourceSlot.itemInstance, true);
            if (removedSlot != null) {
                NotifyDrinkTargetChanged(target);
                return true;
            }

            RestoreDirectDrinkSlot(targetSlot, previousInstance, previousStack);
            Log("Autoloader rollback: source removal failed for " + GetItemLabel(sourceSlot.itemInstance));
            return false;
        }

        private static void RestoreDirectDrinkSlot(Slot slot, ItemInstance previousInstance, int previousStack)
        {
            if (slot == null) return;

            try {
                slot.itemInstance = previousInstance;
                if (previousInstance != null) previousInstance.currentSlot = slot;
                slot.Stack = previousStack;
                slot.OnItemInstanceChange();
                slot.isDirty = true;
            }
            catch { }
        }

        private static void NotifyDrinkTargetChanged(Container target)
        {
            DrinkDispenser dispenser = target as DrinkDispenser;
            if (dispenser != null) {
                try { DrinkDispenser.DrinkDispenserContainerUpdated(dispenser); } catch { }
            }
        }

        private static bool TryMoveOneItem(Container source, Container target, Slot sourceSlot)
        {
            if (source == null || target == null || sourceSlot == null || sourceSlot.itemInstance == null || sourceSlot.Stack <= 0) return false;

            ItemInstance clone = CloneItemInstance(sourceSlot.itemInstance);
            if (clone == null) {
                Log("Transfer failed: could not clone " + DescribeItemInstance(sourceSlot.itemInstance) + ".");
                return false;
            }
            if (!target.CanFitItems(clone, 1)) {
                Log("Transfer failed: target cannot fit " + DescribeItemInstance(clone) + " into " + target.GetType().Name + ".");
                return false;
            }

            bool isDrinkTarget = target is DrinkDispenser || target is BanquetBarrel;
            Slot addedSlot = target.AddItemInstance(1, clone, true, true);
            if (addedSlot == null) {
                if (isDrinkTarget) {
                    Slot targetSlot = target.slots != null && target.slots.Length > 0 ? target.slots[0] : null;
                    if (targetSlot != null && targetSlot.itemInstance != null) {
                        int targetItemId = GetItemId(targetSlot.itemInstance);
                        int cloneItemId = GetItemId(clone);
                        int currentAmount = GetDrinkAmount(targetSlot);
                        if (targetItemId > 0 && targetItemId == cloneItemId && currentAmount > 0) {
                            return false;
                        }
                    }
                }

                Log("Transfer failed: AddItemInstance returned null for " + DescribeItemInstance(clone) + " into " + target.GetType().Name + ".");
                return false;
            }

            Slot removedSlot = source.RemoveItemInstance(sourceSlot.itemInstance, true);
            if (removedSlot != null) return true;

            target.RemoveItemInstance(clone, true);
            Log("Autoloader rollback: source removal failed for " + GetItemLabel(sourceSlot.itemInstance));
            return false;
        }

        private static void ScheduleNextRun(float delaySeconds)
        {
            _nextRunTime = Time.unscaledTime + Mathf.Max(0.25f, delaySeconds);
        }

        private static DrinkDispenser[] GetCachedDispensers()
        {
            if (_cachedDispensers == null || _cachedDispensers.Length == 0 || Time.unscaledTime >= _nextDispenserScanTime) {
                _cachedDispensers = UnityEngine.Object.FindObjectsOfType<DrinkDispenser>();
                _cachedBanquetBarrels = UnityEngine.Object.FindObjectsOfType<BanquetBarrel>();
                _nextDispenserScanTime = Time.unscaledTime + DispenserRescanSeconds;
            }

            return _cachedDispensers;
        }

        private static BanquetBarrel[] GetCachedBanquetBarrels()
        {
            if (_cachedBanquetBarrels == null || _cachedBanquetBarrels.Length == 0 || Time.unscaledTime >= _nextDispenserScanTime) {
                _cachedDispensers = UnityEngine.Object.FindObjectsOfType<DrinkDispenser>();
                _cachedBanquetBarrels = UnityEngine.Object.FindObjectsOfType<BanquetBarrel>();
                _nextDispenserScanTime = Time.unscaledTime + DispenserRescanSeconds;
            }

            return _cachedBanquetBarrels;
        }

        // Push a slot's state to the other player in multiplayer (used after direct slot writes that
        // bypass Container.AddItemInstance, which would otherwise sync itself).
        private static void SyncSlot(Slot slot)
        {
            if (slot == null || !OnlineManager.PlayingOnline()) return;
            try { OnlineSlotsManager.instance.SendSlot(slot); } catch { }
        }

        private static long GetDrinkCacheKey(Container target, ItemInstance instance)
        {
            if (target == null || instance == null) return 0L;

            int itemId = GetItemId(instance);
            if (itemId <= 0) return 0L;

            return ((long)target.GetInstanceID() << 32) ^ (uint)itemId;
        }

        private static Dictionary<int, int> GetFoodCounts(Container container)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            if (container == null || container.slots == null) return counts;

            foreach (Slot slot in container.slots) {
                FoodInstance food = slot != null ? slot.itemInstance as FoodInstance : null;
                if (food == null || slot.Stack <= 0) continue;

                int itemId = GetItemId(food);
                if (itemId <= 0) continue;

                int currentCount;
                counts.TryGetValue(itemId, out currentCount);
                counts[itemId] = currentCount + slot.Stack;
            }

            return counts;
        }

        private static Dictionary<int, int> GetActiveDrinkCounts(DrinkDispenser[] dispensers, BanquetBarrel[] barrels)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();

            if (dispensers != null) {
                foreach (DrinkDispenser dispenser in dispensers) {
                    if (!IsDrinkTargetUsable(dispenser)) continue;
                    AddActiveDrinkCount(counts, dispenser.slots[0]);
                }
            }

            if (barrels != null) {
                foreach (BanquetBarrel barrel in barrels) {
                    if (!IsDrinkTargetUsable(barrel)) continue;
                    AddActiveDrinkCount(counts, barrel.slots[0]);
                    if (barrel.slots.Length > 1) AddActiveDrinkCount(counts, barrel.slots[1]);
                }
            }

            return counts;
        }

        private static void AddActiveDrinkCount(Dictionary<int, int> counts, Slot slot)
        {
            if (counts == null || slot == null || slot.itemInstance == null) return;
            if (GetDrinkAmount(slot) <= 0) return;

            int itemId = GetItemId(slot.itemInstance);
            if (itemId <= 0) return;

            int currentCount;
            counts.TryGetValue(itemId, out currentCount);
            counts[itemId] = currentCount + 1;
        }

        private static int CountSourceSlots(ItemContainer loader)
        {
            int count = 0;
            if (loader == null || loader.slots == null) return count;

            foreach (Slot slot in loader.slots) {
                if (slot != null && slot.itemInstance != null && slot.Stack > 0) count++;
            }

            return count;
        }

        private static ItemContainer ResolveLoaderByKey(string key, ref ItemContainer cachedContainer)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (MatchesContainerKey(cachedContainer, key)) return cachedContainer;

            ItemContainer openContainer = TryGetOpenContainer();
            if (MatchesContainerKey(openContainer, key)) {
                cachedContainer = openContainer;
                return openContainer;
            }

            foreach (ItemContainer container in UnityEngine.Object.FindObjectsOfType<ItemContainer>()) {
                if (MatchesContainerKey(container, key)) {
                    cachedContainer = container;
                    return container;
                }
            }

            return null;
        }

        private static bool IsLoaderActive(ItemContainer container)
        {
            if (container == null || !container.isActiveAndEnabled) return false;

            TavernZone zone = GetTavernZone(container.transform.position);
            return zone != null && zone.zoneType == ZoneType.DiningRoom;
        }

        private static bool IsDrinkTargetUsable(Container target)
        {
            // Any active dispenser counts. Earlier versions required a dining-room zone, then any
            // tavern zone, but wine kegs are routinely placed in unzoned bar space (zone=none), so
            // the zone gate just blocked them. The "only top off dispensers that already hold a
            // drink" rule is what prevents unwanted fills, not the zone.
            return target != null && target.isActiveAndEnabled && target.slots != null && target.slots.Length > 0;
        }

        // Cached TavernZonesManager singleton — resolving it via reflection on every zone check was the
        // single biggest per-tick cost (called once per dispenser/barrel, multiple times per tick).
        private static TavernZonesManager GetZoneManager()
        {
            TavernZonesManager manager = _cachedZoneManager;
            if (manager != null) return manager;

            manager = AutoloaderReflection.FindSingleton<TavernZonesManager>();
            _cachedZoneManager = manager;
            return manager;
        }

        private static TavernZone GetTavernZone(Placeable placeable)
        {
            if (placeable == null) return null;

            try {
                TavernZone zone = placeable.GetCurrentTavernZone();
                if (zone != null) return zone;
            }
            catch { }

            try {
                TavernZonesManager zoneManager = GetZoneManager();
                if (zoneManager != null) return zoneManager.GetTavernZone(placeable.transform.position);
            }
            catch { }

            return null;
        }

        private static TavernZone GetTavernZone(Vector3 position)
        {
            try {
                TavernZonesManager zoneManager = GetZoneManager();
                if (zoneManager != null) return zoneManager.GetTavernZone(position);
            }
            catch { }

            return null;
        }

        private static PlayerController FindLocalPlayer()
        {
            for (int i = 1; i <= 4; i++) {
                try {
                    PlayerController player = PlayerController.GetPlayer(i);
                    if (player != null) return player;
                }
                catch { }
            }

            return null;
        }

        private static ItemInstance CloneItemInstance(ItemInstance source)
        {
            Item item = GetInstanceItem(source);
            if (source == null || item == null) return null;

            try {
                MethodInfo cloneMethod = GetItemCloneMethod(item.GetType(), source.GetType());
                ItemInstance clone = cloneMethod != null ? cloneMethod.Invoke(item, new object[] { source }) as ItemInstance : null;
                if (clone != null) return clone;
            }
            catch { }

            try {
                MethodInfo factoryMethod = GetItemFactoryMethod(item.GetType());
                ItemInstance created = factoryMethod != null ? factoryMethod.Invoke(item, null) as ItemInstance : null;
                if (created != null) return created;
            }
            catch { }

            return null;
        }

        private static MethodInfo GetItemCloneMethod(Type itemType, Type sourceType)
        {
            if (itemType == null || sourceType == null) return null;

            string key = itemType.FullName + "|" + sourceType.FullName;
            MethodInfo cached;
            if (_itemCloneMethodCache.TryGetValue(key, out cached)) return cached;

            MethodInfo fallback = null;
            foreach (MethodInfo method in itemType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                if (!typeof(ItemInstance).IsAssignableFrom(method.ReturnType)) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                if (!parameters[0].ParameterType.IsAssignableFrom(sourceType)) continue;

                if (method.DeclaringType == itemType) {
                    _itemCloneMethodCache[key] = method;
                    return method;
                }

                if (fallback == null) fallback = method;
            }

            _itemCloneMethodCache[key] = fallback;
            return fallback;
        }

        private static MethodInfo GetItemFactoryMethod(Type itemType)
        {
            if (itemType == null) return null;

            MethodInfo cached;
            if (_itemFactoryMethodCache.TryGetValue(itemType, out cached)) return cached;

            MethodInfo fallback = null;
            foreach (MethodInfo method in itemType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                if (!typeof(ItemInstance).IsAssignableFrom(method.ReturnType)) continue;
                if (method.GetParameters().Length != 0) continue;

                if (method.DeclaringType == itemType) {
                    _itemFactoryMethodCache[itemType] = method;
                    return method;
                }

                if (fallback == null) fallback = method;
            }

            _itemFactoryMethodCache[itemType] = fallback;
            return fallback;
        }

        private static string GetLoaderSummary(string key, ref ItemContainer cachedContainer)
        {
            if (string.IsNullOrEmpty(key)) return "<color=#bbbbbb>unassigned</color>";

            ItemContainer container = ResolveLoaderByKey(key, ref cachedContainer);
            if (container == null) return "<color=#ffcc66>missing</color>";

            TavernZone zone = GetTavernZone(container.transform.position);
            string zoneName = zone != null ? zone.zoneType.ToString() : "Outside";
            string state = zone != null && zone.zoneType == ZoneType.DiningRoom
                ? "<color=#66ff66>ready</color>"
                : "<color=#ffcc66>inactive</color>";

            return string.Format("{0} <size=10>({1}, {2:0.0}/{3:0.0})</size>\n<size=9>Drinks: {4}</size>",
                state,
                zoneName,
                container.transform.position.x,
                container.transform.position.y,
                DescribeSourceTypes(container));
        }

        // Distinct drink names in a loader (no counts, no duplicates), so the panel can show at a
        // glance which drinks the assigned loader actually holds.
        private static string DescribeSourceTypes(ItemContainer loader)
        {
            if (loader == null || loader.slots == null) return "empty";

            HashSet<string> seen = new HashSet<string>();
            List<string> ordered = new List<string>();
            foreach (Slot slot in loader.slots)
            {
                if (slot == null || slot.itemInstance == null || slot.Stack <= 0) continue;
                string label = GetItemLabel(slot.itemInstance);
                if (seen.Add(label)) ordered.Add(label);
            }

            return ordered.Count == 0 ? "empty" : string.Join(", ", ordered.ToArray());
        }

        private static string DescribeContainer(ItemContainer container)
        {
            if (container == null) return "missing container";

            TavernZone zone = GetTavernZone(container.transform.position);
            string zoneName = zone != null ? zone.zoneType.ToString() : "Outside";
            string label = container.bigContainer ? "big container" : "container";
            string key = BuildContainerKey(container);

            return string.Format("{0} [{1}] {2:0.0}/{3:0.0} <size=10>{4}</size>",
                label,
                zoneName,
                container.transform.position.x,
                container.transform.position.y,
                key);
        }

        private static int GetItemId(ItemInstance instance)
        {
            Item item = GetInstanceItem(instance);
            return GetItemId(item);
        }

        private static int GetItemId(Item item)
        {
            try {
                if (item == null || ItemIdField == null) return -1;
                return (int)ItemIdField.GetValue(item);
            }
            catch {
                return -1;
            }
        }

        private static float _nextHalloweenCheckTime;
        private static bool _cachedHalloweenActive;
        private static bool IsHalloweenActive()
        {
            if (Time.unscaledTime < _nextHalloweenCheckTime) return _cachedHalloweenActive;
            _nextHalloweenCheckTime = Time.unscaledTime + 10f;
            _cachedHalloweenActive = UnityEngine.Object.FindObjectOfType<HalloweenEvent>() != null;
            return _cachedHalloweenActive;
        }

        private static bool IsSpecialItem(ItemInstance instance)
        {
            Food food = GetInstanceItem(instance) as Food;
            return food != null && food.halloweenFood;
        }

        // Pick the best source slot from the loader to fill an EMPTY target, by priority: event-special
        // items first (halloween food while halloween is active), then highest revenue. `drinks` selects
        // loose-drink sources; otherwise food (FoodInstance) sources.
        private static Slot PickBestSourceSlot(ItemContainer loader, bool drinks, bool halloweenActive, HashSet<int> excludedIds)
        {
            if (loader == null || loader.slots == null) return null;

            Slot best = null;
            long bestScore = long.MinValue;

            foreach (Slot slot in loader.slots)
            {
                if (slot == null || slot.itemInstance == null || slot.Stack <= 0) continue;
                if (drinks ? !IsLooseDrinkInstance(slot.itemInstance) : !(slot.itemInstance is FoodInstance)) continue;

                // Diversity: skip items already placed in this smart-fill pass so each container
                // gets a different drink/food.
                int itemId = GetItemId(slot.itemInstance);
                if (excludedIds != null && itemId > 0 && excludedIds.Contains(itemId)) continue;

                bool special = halloweenActive && IsSpecialItem(slot.itemInstance);
                int revenue = GetInstanceValue(slot.itemInstance);
                long score = (special ? 1000000L : 0L) + revenue;
                if (best == null || score > bestScore)
                {
                    best = slot;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int GetInstanceValue(ItemInstance instance)
        {
            if (instance == null) return 0;

            try {
                FieldInfo priceField = GetCachedPriceField(instance.GetType());
                if (priceField != null) {
                    int total = GetBoxedPriceValue(priceField.GetValue(instance));
                    if (total > 0) return total;
                }
            }
            catch { }

            try {
                Item item = GetInstanceItem(instance);
                if (item != null) {
                    int sellValue = GetPriceValue(item.sellPrice);
                    if (sellValue > 0) return sellValue;
                    return GetPriceValue(item.price);
                }
            }
            catch { }

            return 0;
        }

        // Price field lookup is reflected by obfuscated name per instance type; cache it per type so we
        // don't walk the type hierarchy for every candidate item on every tick.
        private static FieldInfo GetCachedPriceField(Type type)
        {
            FieldInfo field;
            if (_priceFieldCache.TryGetValue(type, out field)) return field;

            field = FindFieldOnType(type, "OFNEDBBCDNO");
            _priceFieldCache[type] = field;
            return field;
        }

        private static int GetBoxedPriceValue(object boxedPrice)
        {
            if (boxedPrice == null) return 0;

            try {
                return GetPriceValue((Price)boxedPrice);
            }
            catch {
                return 0;
            }
        }

        private static int GetPriceValue(Price price)
        {
            return (price.gold * 10000) + (price.silver * 100) + price.copper;
        }

        private static string DescribeContainerSources(ItemContainer loader)
        {
            if (loader == null || loader.slots == null) return "none";

            List<string> parts = new List<string>();
            int total = 0;

            foreach (Slot slot in loader.slots) {
                if (slot == null || slot.itemInstance == null || slot.Stack <= 0) continue;

                total++;
                if (parts.Count < 5) {
                    parts.Add(GetItemLabel(slot.itemInstance) + " x" + slot.Stack);
                }
            }

            if (total == 0) return "none";
            if (total > parts.Count) parts.Add("+" + (total - parts.Count) + " more");
            return string.Join(", ", parts.ToArray());
        }

        private static string DescribeItemInstance(ItemInstance instance)
        {
            if (instance == null) return "null";

            string label = GetItemLabel(instance);
            string typeName = instance.GetType().Name;
            string detail = string.Empty;

            OldKegInstance keg = instance as OldKegInstance;
            if (keg != null) detail = " beers=" + keg.beersLeft;
            else if (instance is FoodInstance) detail = " food";

            return string.Format("{0} [{1}]{2}", label, typeName, detail);
        }

        private static string DescribeSlotStack(Slot slot)
        {
            if (slot == null || slot.itemInstance == null) return "empty";
            return string.Format("{0} stack={1}", DescribeItemInstance(slot.itemInstance), slot.Stack);
        }

        private static int GetDrinkAmount(Slot slot)
        {
            if (slot == null || slot.itemInstance == null) return 0;

            OldKegInstance keg = slot.itemInstance as OldKegInstance;
            if (keg != null) return Mathf.Max(0, keg.beersLeft);

            return Mathf.Max(0, slot.Stack);
        }

        private static int GetDrinkTargetMax(Container target, ItemInstance instance)
        {
            if (target == null || instance == null) return 0;

            // Deliberately not using the learned "observed cap" here. That cache locked in a wrong
            // value (e.g. 10) after a single failed add and then treated the dispenser as permanently
            // full, so a partially-filled tap (like cider at 10 of 20) never got topped off. The
            // compatibility cooldown already prevents re-trying a genuinely full dispenser, so we
            // rely on the slot/item max values instead.
            int slotMax = 0;
            try {
                Slot slot = target.slots != null && target.slots.Length > 0 ? target.slots[0] : null;
                if (slot != null) slotMax = Mathf.Max(0, slot.maxStack);
            }
            catch { }

            int itemMax = 0;
            try {
                itemMax = Mathf.Max(0, target.GetMaxStack(instance));
            }
            catch { }

            int instanceMax = 0;
            try {
                Item item = GetInstanceItem(instance);
                if (item != null) instanceMax = Mathf.Max(0, item.amountStack);
            }
            catch { }

            // Prefer the container's own max (Container.GetMaxStack returns the dispenser's maxStack,
            // e.g. 30 for bar dispensers). The per-slot maxStack can be smaller and was wrongly
            // capping some drinks (cider at 10). AddItemInstance and CanFitItems use the container
            // maxStack, so this matches what the dispenser actually accepts.
            if (itemMax > 0) return itemMax;
            if (slotMax > 0) return slotMax;
            if (instanceMax > 0) return instanceMax;

            return 0;
        }

        private static bool CanDispenseItem(Container target, ItemInstance instance)
        {
            if (target == null || instance == null) return false;

            long key = GetDrinkCacheKey(target, instance);
            if (key == 0L) return false;

            float retryTime;
            if (_drinkRejectCooldowns.TryGetValue(key, out retryTime) && Time.unscaledTime < retryTime) return false;

            // Compatibility between an item type and a target type is stable for the object's lifetime,
            // so a positive result is cached to skip the clone on subsequent probes.
            if (_drinkCompatibilityCache.Contains(key)) return true;

            ItemInstance clone = CloneItemInstance(instance);
            if (clone == null) return false;

            try {
                bool result = target.CanFitItems(clone, 1);
                if (result) _drinkCompatibilityCache.Add(key);
                else _drinkRejectCooldowns[key] = Time.unscaledTime + DrinkRejectCooldownSeconds;
                return result;
            }
            catch {
                _drinkRejectCooldowns[key] = Time.unscaledTime + DrinkRejectCooldownSeconds;
                return false;
            }
        }

        private static bool CanContainerFitItem(Container target, ItemInstance instance)
        {
            if (target == null || instance == null) return false;

            // No compatibility cache here. A container like the bar menu changes capacity as it
            // fills and empties, so a cached "fits" result goes stale and the loader would keep
            // picking an item that no longer fits (it spammed "cannot fit Pescado Asado" every tick).
            ItemInstance clone = CloneItemInstance(instance);
            if (clone == null) return false;

            try {
                return target.CanFitItems(clone, 1);
            }
            catch {
                return false;
            }
        }

        private static bool IsLooseDrinkInstance(ItemInstance instance)
        {
            if (instance == null) return false;
            if (!(instance is FoodInstance)) return false;
            if (instance is OldKegInstance) return false;
            return true;
        }

        private static void LogFoodIdle(string message)
        {
            if (Time.unscaledTime < _nextFoodIdleLogTime) return;
            _nextFoodIdleLogTime = Time.unscaledTime + 30f;
            Log("[FoodDebug] " + message);
        }

        private static void LogDrinkIdle(string message)
        {
            if (Time.unscaledTime < _nextDrinkIdleLogTime) return;
            _nextDrinkIdleLogTime = Time.unscaledTime + 30f;
            Log("[DrinkDebug] " + message);
        }

        // Always-on (throttled) diagnostic: when a drink target finds no source, dump the drink it
        // wanted plus every loader slot and its item type, so it is clear whether the wanted drink is
        // present and whether it is being excluded (for example wine held as kegs, not loose bottles).
        private static void LogDrinkNoSource(Container target, ItemInstance wanted, ItemContainer loader)
        {
            if (Time.unscaledTime < _nextNoSourceLogTime) return;
            _nextNoSourceLogTime = Time.unscaledTime + 30f;
            Log(string.Format("No source for {0} {1}: wants {2} (id {3}). Loader: {4}",
                target.GetType().Name,
                FormatPosition(target.transform.position),
                GetItemLabel(wanted),
                GetItemId(wanted),
                DescribeAllSources(loader)));
        }

        private static string DescribeAllSources(ItemContainer loader)
        {
            if (loader == null || loader.slots == null) return "none";

            List<string> parts = new List<string>();
            foreach (Slot slot in loader.slots)
            {
                if (slot == null || slot.itemInstance == null || slot.Stack <= 0) continue;
                ItemInstance inst = slot.itemInstance;
                parts.Add(string.Format("{0} x{1} ({2}, id {3}{4})",
                    GetItemLabel(inst),
                    slot.Stack,
                    inst.GetType().Name,
                    GetItemId(inst),
                    IsLooseDrinkInstance(inst) ? ", ok" : ", EXCLUDED"));
            }

            return parts.Count == 0 ? "none" : string.Join(", ", parts.ToArray());
        }

        private static string DescribeFoodCounts(Dictionary<int, int> counts)
        {
            if (counts == null || counts.Count == 0) return "none";

            List<string> parts = new List<string>();
            foreach (KeyValuePair<int, int> pair in counts) {
                if (parts.Count >= 6) {
                    parts.Add("+" + (counts.Count - parts.Count) + " more");
                    break;
                }

                parts.Add(pair.Key + " x" + pair.Value);
            }

            return string.Join(", ", parts.ToArray());
        }

        private static string FormatPosition(Vector3 position)
        {
            return string.Format("{0:0.0}/{1:0.0}", position.x, position.y);
        }

        private static string GetItemLabel(ItemInstance instance)
        {
            Item item = GetInstanceItem(instance);
            if (instance == null || item == null) return "Unknown";

            try {
                if (!string.IsNullOrEmpty(item.nameId)) {
                    string localized = LocalisationSystem.Get(item.nameId);
                    if (!string.IsNullOrEmpty(localized)) return localized;
                }
            }
            catch { }

            return item.name;
        }

        private static Item GetInstanceItem(ItemInstance instance)
        {
            try {
                if (instance == null || ItemInstanceItemField == null) return null;
                return ItemInstanceItemField.GetValue(instance) as Item;
            }
            catch {
                return null;
            }
        }

        private static Placeable GetContainerPlaceable(ItemContainer container)
        {
            try {
                if (container == null) return null;
                if (ItemContainerPlaceableProperty != null) {
                    Placeable propertyPlaceable = ItemContainerPlaceableProperty.GetValue(container, null) as Placeable;
                    if (propertyPlaceable != null) return propertyPlaceable;
                }
                if (ItemContainerPlaceableField != null) {
                    Placeable fieldPlaceable = ItemContainerPlaceableField.GetValue(container) as Placeable;
                    if (fieldPlaceable != null) return fieldPlaceable;
                }

                return AutoloaderReflection.GetInstanceFieldValueByType<Placeable>(container);
            }
            catch {
                return null;
            }
        }

        private static ItemContainer TryGetOpenContainer()
        {
            for (int i = 1; i <= 4; i++) {
                ItemContainer container = TryGetOpenContainer(BigContainerUI.Get(i));
                if (container != null) return container;

                container = TryGetOpenContainer(SmallContainerUI.Get(i));
                if (container != null) return container;
            }

            return null;
        }

        private static ItemContainer TryGetOpenContainer(ContainerUI ui)
        {
            try {
                if (ui == null || !ui.isActiveAndEnabled || !ui.gameObject.activeInHierarchy) return null;

                Container current = AutoloaderReflection.GetInstancePropertyValueByType<Container>(ui);
                if (current == null && ContainerUICurrentContainerProperty != null) {
                    current = ContainerUICurrentContainerProperty.GetValue(ui, null) as Container;
                }
                return current as ItemContainer;
            }
            catch {
                return null;
            }
        }

        private static bool IsAssignableContainer(ItemContainer container)
        {
            if (container == null || !container.isActiveAndEnabled) return false;
            return !string.IsNullOrEmpty(BuildContainerKey(container));
        }

        private static bool MatchesContainerKey(ItemContainer container, string key)
        {
            if (container == null || string.IsNullOrEmpty(key)) return false;
            return string.Equals(BuildContainerKey(container), key, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildContainerKey(ItemContainer container)
        {
            Placeable placeable = GetContainerPlaceable(container);
            if (placeable == null) placeable = TryGetContainerUIPlaceable(container);
            if (placeable != null && !string.IsNullOrEmpty(placeable.guidString)) {
                return "guid:" + placeable.guidString;
            }

            if (placeable != null) {
                int uniqueId = GetPlaceableUniqueId(placeable);
                if (uniqueId > 0) {
                    return "id:" + uniqueId.ToString();
                }
            }

            return "pos:" + string.Format("{0:F2}:{1:F2}:{2:F2}",
                container.transform.position.x,
                container.transform.position.y,
                container.transform.position.z);
        }

        private static Placeable TryGetContainerUIPlaceable(ItemContainer container)
        {
            for (int i = 1; i <= 4; i++) {
                Placeable placeable = TryGetContainerUIPlaceable(BigContainerUI.Get(i), container);
                if (placeable != null) return placeable;

                placeable = TryGetContainerUIPlaceable(SmallContainerUI.Get(i), container);
                if (placeable != null) return placeable;
            }

            return null;
        }

        private static Placeable TryGetContainerUIPlaceable(ContainerUI ui, ItemContainer container)
        {
            try {
                if (ui == null) return null;
                Container current = AutoloaderReflection.GetInstancePropertyValueByType<Container>(ui);
                if (current == null && ContainerUICurrentContainerProperty != null) {
                    current = ContainerUICurrentContainerProperty.GetValue(ui, null) as Container;
                }
                if (!object.ReferenceEquals(current, container)) return null;
                Placeable placeable = null;
                if (ContainerUIPlaceableField != null) {
                    placeable = ContainerUIPlaceableField.GetValue(ui) as Placeable;
                }
                return placeable ?? AutoloaderReflection.GetInstanceFieldValueByType<Placeable>(ui);
            }
            catch {
                return null;
            }
        }

        private static int GetPlaceableUniqueId(Placeable placeable)
        {
            try {
                if (placeable == null || PlaceableUniqueIdGetter == null) return 0;
                return (int)PlaceableUniqueIdGetter.Invoke(placeable, null);
            }
            catch {
                return 0;
            }
        }

        private static void CacheLoaderReference(ConfigEntry<string> entry, ItemContainer container)
        {
            if (entry == Plugin.FoodLoaderGuid) {
                _cachedFoodLoader = container;
            }
            else if (entry == Plugin.DrinkLoaderGuid) {
                _cachedDrinkLoader = container;
            }
        }

        private static FieldInfo FindFieldOnType(Type type, string name)
        {
            while (type != null) {
                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field;
                type = type.BaseType;
            }

            return null;
        }

        private static void SetStatus(string message)
        {
            _lastStatus = message;
            _lastStatusTime = Time.unscaledTime;
            RecordAction(message);
        }

        // Pushes an action onto the rolling feed shown in the panel (most recent first) and also
        // writes it to the log file. Used for loads, assignments, and clears.
        private static void RecordAction(string message)
        {
            _actionFeed.Insert(0, message);
            while (_actionFeed.Count > ActionFeedCapacity) _actionFeed.RemoveAt(_actionFeed.Count - 1);
            Log(message);
        }

        public static string GetRecentActionsText()
        {
            if (_actionFeed.Count == 0) return "";
            return "\n--- recent ---\n" + string.Join("\n", _actionFeed.ToArray());
        }

        private static void Log(string message)
        {
            try {
                File.AppendAllText(Plugin.LogPath, "[Autoloader] " + message + "\n");
            }
            catch { }
        }

        private static void LogDrinkDebug(string message)
        {
            if (!VerboseDrinkDebug) return;
            Log("[DrinkDebug] " + message);
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.lolaiur.trautoloader";
        public const string PLUGIN_NAME = "TR Autoloader";
        public const string PLUGIN_VERSION = "2.0.0";
    }
}


