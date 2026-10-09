using GorillaNetworking;
using Photon.Pun;
using Poison.Classes.Menu;
using Poison.Extensions;
using Poison.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;


using UnityEngine.InputSystem;
using static Poison.Menu.Main;
using static Poison.Utilities.AssetUtilities;

namespace Poison.Menu
{
    public partial class UI : MonoBehaviour
    {
        public static UI Instance;
        public static Texture2D watermarkImage;

        public static bool IsTyping => Hud.HasMouse ? Hud.Instance.IsTyping :
            Instance != null && Instance.isActiveAndEnabled && Instance.isOpen &&
            (Instance.textFocused || CurrentPrompt?.IsText == true);

        private static float ViewWidth => Screen.width;
        private static float ViewHeight => Screen.height;

        public static bool HasMouse
        {
            get
            {
                if (Hud.HasMouse) return true;
                if (Instance == null || !Instance.isActiveAndEnabled || !Instance.isOpen) return false;
                if (Mouse.current == null && !Instance.padCursor) return false;
                float scale = ScreenScale();
                Vector2 point = Instance.PointerPosition / scale;
                if (CurrentPrompt != null) return true;
                if (Instance.options.classicUI)
                    return Instance.draggingWindow || Instance.WindowRect(ViewWidth / scale, ViewHeight / scale).Contains(point);
                foreach (Panel panel in Instance.panels)
                    if (panel.open && !panel.closing && panel.rect.Contains(point)) return true;
                return false;
            }
        }

        private const float backgroundBaseAlpha = 1f;
        private const float panelBaseAlpha = 1f;
        private Color background = new Color(0.055f, 0.066f, 0.09f, backgroundBaseAlpha);
        private Color panel = new Color(0.086f, 0.1f, 0.14f, panelBaseAlpha);
        private Color border = new Color32(43, 47, 60, 255);
        private Color accent = new Color32(158, 139, 255, 255);
        private Color bright = new Color32(235, 237, 245, 255);
        private Color muted = new Color32(144, 151, 169, 255);
        private static readonly Regex tags = new Regex("<[^>]*>", RegexOptions.Compiled);

        internal Color BackgroundColor => background;
        internal Color PanelColor => panel;
        internal Color BorderColor => border;
        internal Color AccentColor => accent;
        internal Color BrightColor => bright;
        internal Color MutedColor => muted;
        internal float UiOpacity => options.uiOpacity;
        internal float UiRounding => options.rounding;
        internal float UiAccentAmount => options.accentAmount;
        internal float UiRowHeight => options.rowHeight;

        private const int PanelLauncher = 0;
        private const int PanelCategory = 1;
        private const int PanelFavorites = 2;
        private const int PanelActive = 3;
        private const int PanelSearch = 4;
        private const int PanelControls = 5;
        private const int PanelConsole = 6;
        private const int PanelSettings = 7;
        private const int PanelLayout = 8;
        private const int PanelInput = 9;

        private sealed class Entry
        {
            public ButtonInfo button;
            public string category;
            public string title;
            public string description;
            public string rawTitle;
            public string rawDescription;
        }

        private sealed class Panel
        {
            public int id;
            public string key;
            public string title;
            public int kind;
            public string category;
            public Rect rect;
            public float height;
            public float baseWidth;
            public float baseHeight;
            public bool open;
            public bool collapsed;
            public float collapse;
            public float appear;
            public bool closing;
            public bool hasTarget;
            public Rect target;
            public readonly Scroll scroll = new Scroll();
        }

        private readonly List<Panel> panels = new List<Panel>();
        private readonly Dictionary<string, Panel> panelMap = new Dictionary<string, Panel>();
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<string> categories = new List<string>();
        private readonly List<string> lines = new List<string>();
        private readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        private readonly Dictionary<ButtonInfo, float> switches = new Dictionary<ButtonInfo, float>();
        private readonly Queue<Action> actions = new Queue<Action>();
        private readonly string hidePath = Path.Combine(PluginInfo.BaseDirectory, "Poison_HideGUI.txt");

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;
        private GUIStyle wrapStyle;
        private GUIStyle rightStyle;
        private GUIStyle tabStyle;
        private GUIStyle buttonStyle;
        private GUIStyle numberStyle;
        private Font uiFont;
        private bool fontChecked;
        private Texture2D logoRecolor;
        private Texture2D logoRecolorSource;
        private float logoRecolorHue = -1f;
        private float logoRecolorAt;
        private Panel activePanel;
        private int panelId;
        private int cascade;
        private readonly Scroll promptScroll = new Scroll();

        private bool isOpen;
        private bool textFocused;
        private bool cursorHeld;
        private bool oldCursorVisible;
        private CursorLockMode oldCursorLock;
        private bool refresh = true;
        private bool focusSearch;
        private float fade;
        private float nextRefresh;
        private string search = "";
        private string command = "";
        private string roomCode = "";
        private string playerName = "";
        private string red = "0";
        private string green = "0";
        private string blue = "0";
        private string status = "";
        private float statusUntil;
        private string tip = "";
        private string lastTip = "";
        private float tipSince;
        private string promptInput = "";
        private PromptData lastPrompt;
        private string focusedInput;
        private string inputText = "";
        private Keyboard textKeyboard;
        private float backspaceNext;
        private bool inputSubmit;
        private string submitInput;
        private bool inputClicked;

        private float gridShownAt = -10f;
        private float gridFade;
        private bool layoutMode;
        private float searchIndexAt;
        private bool searchIndexing;
        private bool indexedResultsReady;
        private string indexedQuery = "";
        private readonly List<Entry> indexedResults = new List<Entry>();

        private void Awake()
        {
            Instance = this;
            LoadOptions();
            isOpen = !File.Exists(hidePath);
            Buttons.OnCategoryChanged += CategoryChanged;
            watermarkImage = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.icon.png");
        }

        private void Update()
        {
            ReadPad();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != textKeyboard)
            {
                if (textKeyboard != null) textKeyboard.onTextInput -= OnTextInput;
                textKeyboard = keyboard;
                if (textKeyboard != null) textKeyboard.onTextInput += OnTextInput;
            }

            if (!textFocused && (keyboard?.backslashKey.wasPressedThisFrame == true ||
                options.remoteMouse && keyboard?.homeKey.wasPressedThisFrame == true))
            {
                if (Hud.InUse && Hud.Instance != null)
                    Hud.Instance.SetOpen(!Hud.Instance.IsOpen);
                else
                    ToggleGUI();
            }
            if (isOpen && !options.classicUI && !textFocused && keyboard?.backquoteKey.wasPressedThisFrame == true)
                TogglePanel(PanelConsole, null);
            if (isOpen && keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.fKey.wasPressedThisFrame)
            {
                if (options.classicUI) tab = 0;
                else OpenPanel(PanelLauncher, null);
                focusSearch = true;
            }
            if (isOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !bindCancelled)
            {
                if (CurrentPrompt != null)
                {
                    PromptData prompt = CurrentPrompt;
                    if (prompt.DeclineText != null)
                        actions.Enqueue(() => { if (CurrentPrompt == prompt) Main.Toggle("Decline Prompt", true, true); });
                }
                else if (!string.IsNullOrEmpty(focusedInput) || textFocused) ClearInput();
                else if (Hud.InUse && Hud.Instance != null) Hud.Instance.SetOpen(false);
                else ToggleGUI();
            }
            UpdateTextInput(keyboard);
            StepSearchIndex();
            float speed = options.animations ? Ease(14) : 1;
            fade = Mathf.Lerp(fade, isOpen ? 1 : 0, speed);
            UpdateTheme();
            foreach (Panel panel in panels)
                panel.scroll.Step(Time.unscaledDeltaTime, options.smoothScroll, options.scrollSmoothing);
            cardScroll.Step(Time.unscaledDeltaTime, options.smoothScroll, options.scrollSmoothing);
            categoryScroll.Step(Time.unscaledDeltaTime, options.smoothScroll, options.scrollSmoothing);
            promptScroll.Step(Time.unscaledDeltaTime, options.smoothScroll, options.scrollSmoothing);
            if ((isOpen || options.arraylist) && (refresh || Time.unscaledTime >= nextRefresh)) Refresh();
            if (saveAt > 0 && Time.unscaledTime >= saveAt) SaveOptions();
            if (Time.unscaledTime >= cleanAt)
            {
                foreach (string key in motions.Where(pair => Time.unscaledTime - pair.Value.seen > 10).Select(pair => pair.Key).ToArray()) motions.Remove(key);
                cleanAt = Time.unscaledTime + 10;
            }
        }

        private void LateUpdate()
        {
            int count = actions.Count;
            for (int i = 0; i < count; i++)
            {
                try { actions.Dequeue()(); }
                catch (Exception error)
                {
                    ShowStatus(error.Message);
                    DebugPrint(error.ToString());
                    LogManager.LogError(error);
                }
                refresh = true;
            }
            if (isOpen && Application.isFocused && !Hud.HasMouse)
            {
                if (!cursorHeld)
                {
                    oldCursorVisible = Cursor.visible;
                    oldCursorLock = Cursor.lockState;
                    cursorHeld = true;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else ReleaseCursor();
        }

        private void OnDisable()
        {
            StopWii();
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            StopWii();
            if (saveAt > 0) SaveOptions();
            Buttons.OnCategoryChanged -= CategoryChanged;
            if (textKeyboard != null) textKeyboard.onTextInput -= OnTextInput;
            ReleaseCursor();
            foreach (Texture2D icon in icons.Values)
                if (icon != null) Destroy(icon);
            if (logoRecolor != null) Destroy(logoRecolor);
            if (monoFont != null) Destroy(monoFont);
            if (rainbow != null) Destroy(rainbow);
            if (Instance == this) Instance = null;
        }

        private void ReleaseCursor()
        {
            ResetPointer();
            draggingWindow = false;
            if (!cursorHeld) return;
            Cursor.lockState = oldCursorLock;
            Cursor.visible = oldCursorVisible;
            cursorHeld = false;
            textFocused = false;
            ClearInput();
        }

        private void ToggleGUI()
        {
            isOpen = !isOpen;
            refresh = true;
            if (isOpen) EnsurePanels();
            if (!isOpen) ReleaseCursor();
            try
            {
                if (isOpen && File.Exists(hidePath)) File.Delete(hidePath);
                else if (!isOpen)
                {
                    Directory.CreateDirectory(PluginInfo.BaseDirectory);
                    File.WriteAllText(hidePath, "hidden");
                }
            }
            catch (Exception error) { DebugPrint(error.Message); }
        }

        private void EnsurePanels()
        {
            if (panelMap.TryGetValue(PanelLauncher + ":", out Panel existing))
            {
                if (!existing.open && !existing.closing)
                {
                    existing.open = true;
                    existing.appear = 0f;
                }
                return;
            }
            if (options.panelLayout != null && options.panelLayout.Count > 0)
            {
                ApplySavedLayout();
                return;
            }
            Panel launcher = OpenPanel(PanelLauncher, null);
            launcher.rect = new Rect(24, 60, 224, 560);
            OpenPanel(PanelFavorites, null);
        }

        private static float DefaultWidth(int kind)
        {
            switch (kind)
            {
                case PanelLauncher: return 224;
                case PanelCategory:
                case PanelFavorites:
                case PanelActive: return 268;
                case PanelSearch: return 300;
                case PanelControls: return 560;
                case PanelConsole: return 560;
                case PanelSettings: return 620;
                case PanelInput: return 620;
                case PanelLayout: return 380;
                default: return 268;
            }
        }

        private static float DefaultHeight(int kind)
        {
            switch (kind)
            {
                case PanelLauncher: return 560;
                case PanelControls: return 300;
                case PanelConsole: return 340;
                case PanelSettings: return 520;
                case PanelInput: return 520;
                case PanelLayout: return 440;
                default: return 400;
            }
        }

        private static string DefaultTitle(int kind, string category)
        {
            switch (kind)
            {
                case PanelLauncher: return "Poison";
                case PanelCategory: return category;
                case PanelFavorites: return "Favorites";
                case PanelActive: return "Active";
                case PanelSearch: return "Search";
                case PanelControls: return "Controls";
                case PanelConsole: return "Console";
                case PanelSettings: return "Settings";
                case PanelLayout: return "Layout editor";
                case PanelInput: return "Controller input";
                default: return "Panel";
            }
        }

        private Panel OpenPanel(int kind, string category)
        {
            string key = kind + ":" + (category ?? "");
            if (panelMap.TryGetValue(key, out Panel existing))
            {
                existing.open = true;
                existing.closing = false;
                return existing;
            }

            Panel panel = new Panel
            {
                id = ++panelId,
                key = key,
                kind = kind,
                category = category,
                title = DefaultTitle(kind, category),
                height = DefaultHeight(kind),
                open = true
            };
            panel.baseWidth = DefaultWidth(kind);
            panel.baseHeight = DefaultHeight(kind);
            panel.rect = new Rect(260 + (cascade % 5) * 26, 54 + (cascade % 7) * 22, panel.baseWidth, panel.height);
            panel.appear = 0f;
            cascade++;
            if (kind == PanelLauncher) panel.rect.position = new Vector2(24, 60);
            else if (kind == PanelFavorites) panel.rect.position = new Vector2(Mathf.Max(260, ViewWidth / ScreenScale() - DefaultWidth(kind) - 24), 60);
            panels.Add(panel);
            panelMap[key] = panel;
            if (kind != PanelLauncher && (options.autoArrange || layoutManaged)) AutoSortPanels();
            return panel;
        }

        private void TogglePanel(int kind, string category)
        {
            string key = kind + ":" + (category ?? "");
            if (panelMap.TryGetValue(key, out Panel existing) && existing.open && !existing.closing)
            {
                existing.closing = true;
                ClearInput();
                return;
            }
            OpenPanel(kind, category);
        }

        private void ClosePanel(Panel panel)
        {
            panel.closing = true;
            ClearInput();
        }

        private void ToggleCollapse(Panel panel)
        {
            panel.collapsed = !panel.collapsed;
        }

        private void CategoryChanged()
        {
            refresh = true;
            if (options.classicUI)
            {
                tab = 0;
                category = Buttons.CurrentCategoryName;
                cardScroll.Reset();
            }
        }

        private static bool CanSee(string name) =>
            name != "Internal Mods" && (isAdmin || (!name.Contains("Admin") && name != "Mod Givers"));

        private void Refresh()
        {
            refresh = false;
            nextRefresh = Time.unscaledTime + 0.2f;
            categories.Clear();
            var seen = new HashSet<ButtonInfo>();
            ButtonInfo[][] groups = Buttons.buttons;
            string[] names = Buttons.categoryNames;
            if (groups == null || names == null) { entries.Clear(); return; }
            int entryCount = 0;
            for (int i = 0; i < Math.Min(groups.Length, names.Length); i++)
            {
                string name = names[i];
                if (!CanSee(name) || groups[i] == null) continue;
                categories.Add(name);
                foreach (ButtonInfo button in groups[i])
                {
                    if (button == null) continue;
#if LEGAL || LEGAL_DEBUG
                    if (!button.legal && !button.label) continue;
#endif
                    seen.Add(button);
                    Entry entry;
                    if (entryCount < entries.Count) entry = entries[entryCount];
                    else { entry = new Entry(); entries.Add(entry); }
                    entryCount++;
                    entry.button = button;
                    entry.category = name;
                    string title = button.overlapText ?? button.buttonText;
                    if (entry.rawTitle != title)
                    {
                        entry.rawTitle = title;
                        entry.title = Plain(title);
                    }
                    if (entry.rawDescription != button.toolTip)
                    {
                        entry.rawDescription = button.toolTip;
                        entry.description = Plain(button.toolTip);
                    }
                }
            }
            if (entryCount < entries.Count) entries.RemoveRange(entryCount, entries.Count - entryCount);
            if (switches.Count > entries.Count + 100)
            {
                foreach (ButtonInfo removed in switches.Keys.Where(key => !seen.Contains(key)).ToArray())
                    switches.Remove(removed);
            }
            UpdateArraylist();
        }

        private static bool Matches(string value, string query) => value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string Plain(string value) => string.IsNullOrEmpty(value) ? "" : tags.Replace(value, "");

        private List<Entry> FilterPanel(Panel panel, List<Entry> output)
        {
            output.Clear();
            string query = search.Trim();
            var shown = new HashSet<ButtonInfo>();
            foreach (Entry entry in entries)
            {
                ButtonInfo button = entry.button;
                switch (panel.kind)
                {
                    case PanelFavorites:
                        if (button.label || !favorites.Contains(button.buttonText)) continue;
                        break;
                    case PanelActive:
                        if (button.label || !button.enabled) continue;
                        if ((hideSettings && entry.category.Contains("Settings")) || (hideMacros && entry.category.Contains("Macro"))) continue;
                        break;
                    case PanelSearch:
                        if (query.Length == 0) continue;
                        if (indexedResultsReady && indexedQuery == query)
                        {
                            if (!indexedResults.Contains(entry)) continue;
                        }
                        else if (!Matches(entry.title, query) && !Matches(button.buttonText, query) &&
                            !Matches(entry.category, query) && !(button.aliases?.Any(alias => Matches(alias, query)) ?? false)) continue;
                        break;
                    default:
                        if (entry.category != panel.category) continue;
                        break;
                }
                if (shown.Add(button)) output.Add(entry);
            }
            if (options.sortModules) output.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));
            return output;
        }

        private Font UiFont()
        {
            if (fontChecked) return uiFont;
            fontChecked = true;
            try
            {
                // CreateDynamicFontFromOSFont logs a warning for every requested name that is not
                // installed, so only pass names that actually exist on this system.
                HashSet<string> installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                string[] candidates = { "Verdana", "Tahoma", "Segoe UI", "Arial", "DejaVu Sans", "Liberation Sans", "Noto Sans" };
                List<string> present = new List<string>();
                foreach (string name in candidates)
                    if (installed.Contains(name)) present.Add(name);
                if (present.Count > 0)
                {
                    Font f = Font.CreateDynamicFontFromOSFont(present.ToArray(), 15);
                    if (f != null) uiFont = f;
                }
            }
            catch { }
            return uiFont;
        }

        private void Setup()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), clipping = TextClipping.Clip, wordWrap = false };
            Font font = UiFont();
            if (font != null) textStyle.font = font;
            WhiteText(textStyle);
            smallStyle = new GUIStyle(textStyle) { fontSize = 11 };
            titleStyle = new GUIStyle(textStyle) { fontSize = 17, fontStyle = FontStyle.Bold };
            wrapStyle = new GUIStyle(smallStyle) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            rightStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
            tabStyle = new GUIStyle(textStyle) { fontSize = 13 };
            buttonStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter };
            WhiteText(buttonStyle);
            numberStyle = new GUIStyle(textStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(2, 2, 0, 0), clipping = TextClipping.Clip };
            numberStyle.normal.background = null;
            numberStyle.focused.background = null;
            numberStyle.hover.background = null;
            numberStyle.active.background = null;
            WhiteText(numberStyle);
        }

        private void OnGUI()
        {
            Event pad = DrawInputUIPrepare();
            if (isOpen) PluginManager.ExecuteOnGUI(pad);
            DrawInputUIDraw();
        }

        internal void DrawUI()
        {
            if (fade < 0.005f && !isOpen && !options.arraylist) return;
            Setup();
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            Color oldBackground = GUI.backgroundColor;
            Color oldContent = GUI.contentColor;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            try
            {
                GUI.depth = -100;
                GUI.contentColor = Color.white;
                GUI.color = Color.white;
                GUI.matrix = Matrix4x4.identity;
                if (!options.listOverMenu || !isOpen) DrawArraylist();
                if (fade < 0.005f && !isOpen) return;

                float scale = ScreenScale();
                float width = ViewWidth / scale;
                float height = ViewHeight / scale;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
                if (fade < 0.999f)
                {
                    float grow = Mathf.Lerp(0f, 1f, fade);
                    GUIUtility.ScaleAroundPivot(new Vector2(grow, grow), new Vector2(width * 0.5f, height * 0.5f));
                }
                GUI.color = new Color(1, 1, 1, fade);
                GUI.backgroundColor = Color.white;
                GUI.enabled = isOpen;
                tip = "";
                pointerInside = true;
                bool mouseDown = InputEvent.type == EventType.MouseDown;
                if (mouseDown) inputClicked = false;
                GUI.enabled = isOpen && CurrentPrompt == null;
                if (options.classicUI) DrawClassic(width, height);
                else DrawPanels(width, height);
                GUI.enabled = isOpen;

                if (CurrentPrompt != null) DrawPrompt();
                else DrawTip();
                if (Time.unscaledTime < statusUntil)
                {
                    float alpha = Animate("status", Mathf.Clamp01((statusUntil - Time.unscaledTime) * 3));
                    float toastWidth = Mathf.Min(600, smallStyle.CalcSize(new GUIContent(status)).x + 32);
                    float screenWidth = ViewWidth / scale;
                    float screenHeight = ViewHeight / scale;
                    Box(new Rect((screenWidth - toastWidth) / 2, screenHeight - 42, toastWidth, 28), Alpha(this.panel, alpha), 4);
                    Label(new Rect((screenWidth - toastWidth) / 2 + 16, screenHeight - 42, toastWidth - 32, 28), status, smallStyle, Alpha(bright, alpha));
                }

                if (options.listOverMenu && isOpen) DrawArraylist();
                DrawCursor();
                if (mouseDown && !inputClicked && !string.IsNullOrEmpty(focusedInput)) ClearInput();
                textFocused = isOpen && !string.IsNullOrEmpty(focusedInput);
                if (Event.current.type == EventType.Repaint) snapAnimations = false;
                inputSubmit = false;
                submitInput = null;
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.backgroundColor = oldBackground;
                GUI.contentColor = oldContent;
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
            }
        }

        private void DrawPanels(float width, float height)
        {
            if (isOpen) EnsurePanels();
            UpdatePanelInteraction(panels, width, height);
            DrawGrid(width, height);
            bool closedOne = false;
            for (int i = panels.Count - 1; i >= 0; i--)
            {
                Panel deck = panels[i];
                if (!deck.open && !deck.closing) continue;
                if (Event.current.type == EventType.Repaint)
                {
                    float target = deck.collapsed ? 1f : 0f;
                    deck.collapse = options.animations ? Mathf.Lerp(deck.collapse, target, Ease(18)) : target;
                    float appearTarget = deck.closing ? 0f : 1f;
                    deck.appear = options.animations ? Mathf.Lerp(deck.appear, appearTarget, Ease(22)) : appearTarget;
                    if (deck.hasTarget)
                    {
                        float move = options.animations ? Ease(14) : 1f;
                        deck.rect.x = Mathf.Lerp(deck.rect.x, deck.target.x, move);
                        deck.rect.y = Mathf.Lerp(deck.rect.y, deck.target.y, move);
                        deck.rect.width = Mathf.Lerp(deck.rect.width, deck.target.width, move);
                        deck.height = Mathf.Lerp(deck.height, deck.target.height, move);
                        if (Mathf.Abs(deck.rect.x - deck.target.x) < 0.5f && Mathf.Abs(deck.rect.y - deck.target.y) < 0.5f &&
                            Mathf.Abs(deck.rect.width - deck.target.width) < 0.5f && Mathf.Abs(deck.height - deck.target.height) < 0.5f)
                        {
                            deck.rect.x = deck.target.x;
                            deck.rect.y = deck.target.y;
                            deck.rect.width = deck.target.width;
                            deck.height = deck.target.height;
                            deck.hasTarget = false;
                        }
                    }
                }
                else if (!options.animations)
                {
                    deck.appear = deck.closing ? 0f : 1f;
                }
                if (deck.closing && deck.appear < 0.02f)
                {
                    deck.appear = 0f;
                    deck.open = false;
                    deck.closing = false;
                    closedOne = true;
                    continue;
                }
                if (!deck.open && !deck.closing) continue;
                deck.rect.height = Mathf.Lerp(deck.height, 26f, deck.collapse);
                deck.rect.x = Mathf.Clamp(deck.rect.x, 4f, Mathf.Max(4f, width - deck.rect.width - 4f));
                deck.rect.y = Mathf.Clamp(deck.rect.y, 4f, Mathf.Max(4f, height - deck.rect.height - 4f));
                DrawPanelBody(deck);
            }
            if (closedOne && (options.autoArrange || layoutManaged)) AutoSortPanels();
        }

        private float SnapSize => Mathf.Max(4f, options.snapSize);

        private float Snap(float value) => Mathf.Round(value / SnapSize) * SnapSize;

        private void SnapPanel(Panel panel)
        {
            if (!options.snapGrid) return;
            panel.rect.x = Mathf.Round(panel.rect.x / SnapSize) * SnapSize;
            panel.rect.y = Mathf.Round(panel.rect.y / SnapSize) * SnapSize;
        }

        private void DrawGrid(float width, float height)
        {
            bool active = draggingPanel != null || resizingPanel != null || layoutMode;
            if (!options.snapGrid && !layoutMode)
            {
                gridFade = 0f;
                return;
            }
            if (active) gridShownAt = Time.unscaledTime;
            float elapsed = Time.unscaledTime - gridShownAt;
            float target = active ? 1f : Mathf.Clamp01(1f - elapsed / 0.9f);
            gridFade = Mathf.Lerp(gridFade, target, options.animations ? Ease(16) : 1f);
            float opacity = gridFade * (0.06f + options.gridStrength * 0.22f);
            if (opacity <= 0.002f) return;

            float size = SnapSize;
            Color fine = Alpha(bright, opacity);
            Color major = Alpha(accent, opacity * 1.6f);
            for (float x = size; x < width; x += size)
            {
                int index = Mathf.RoundToInt(x / size);
                Box(new Rect(x, 0, 1, height), index % 4 == 0 ? major : fine, 0);
            }
            for (float y = size; y < height; y += size)
            {
                int index = Mathf.RoundToInt(y / size);
                Box(new Rect(0, y, width, 1), index % 4 == 0 ? major : fine, 0);
            }
        }

        private void BeginSearch()
        {
            bool wasOpen = panelMap.TryGetValue(PanelSearch + ":", out Panel open) && open.open;
            if (!wasOpen)
            {
                searchIndexing = true;
                searchIndexAt = Time.unscaledTime;
            }
            OpenPanel(PanelSearch, null);
            RunSearch();
        }

        private void RunSearch()
        {
            string query = search.Trim();
            indexedQuery = query;
            indexedResults.Clear();
            if (query.Length > 0)
            {
                foreach (Entry entry in entries)
                {
                    ButtonInfo button = entry.button;
                    if (!Matches(entry.title, query) && !Matches(button.buttonText, query) &&
                        !Matches(entry.category, query) && !(button.aliases?.Any(alias => Matches(alias, query)) ?? false)) continue;
                    indexedResults.Add(entry);
                }
                if (options.sortModules) indexedResults.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));
            }
            indexedResultsReady = true;
        }

        private void StepSearchIndex()
        {
            if (!searchIndexing) return;
            if (Time.unscaledTime - searchIndexAt >= 0.7f) searchIndexing = false;
        }

        private void ToggleLayout()
        {
            layoutMode = !layoutMode;
            if (layoutMode) OpenPanel(PanelLayout, null);
        }

        private Panel draggingPanel;
        private Panel resizingPanel;
        private Vector2 draggingOffset;
        private Vector2 resizeStart;
        private Vector2 resizeOrigin;

        private void UpdatePanelInteraction(List<Panel> list, float screenWidth, float screenHeight)
        {
            if (!GUI.enabled) return;
            Event current = InputEvent;
            Vector2 point = GuiPoint;

            if (current.type == EventType.MouseDown && current.button == 0)
                gridShownAt = Time.unscaledTime;

            if (resizingPanel != null)
            {
                if (!PointerHeld) { resizingPanel = null; return; }
                Vector2 delta = point - resizeStart;
                float minW = 150f, minH = 40f;
                resizingPanel.rect.width = Mathf.Max(minW, resizeOrigin.x + delta.x);
                float newHeight = Mathf.Max(minH, resizeOrigin.y + delta.y);
                if (options.snapGrid)
                {
                    resizingPanel.rect.width = Mathf.Max(minW, Mathf.Round(resizingPanel.rect.width / SnapSize) * SnapSize);
                    newHeight = Mathf.Max(minH, Mathf.Round(newHeight / SnapSize) * SnapSize);
                }
                resizingPanel.baseWidth = resizingPanel.rect.width;
                resizingPanel.baseHeight = newHeight;
                resizingPanel.height = newHeight;
                resizingPanel.hasTarget = false;
                gridShownAt = Time.unscaledTime;
                return;
            }

            if (draggingPanel != null)
            {
                if (!PointerHeld) { SnapPanel(draggingPanel); draggingPanel = null; return; }
                draggingPanel.rect.x = point.x - draggingOffset.x;
                draggingPanel.rect.y = point.y - draggingOffset.y;
                gridShownAt = Time.unscaledTime;
                if (options.snapGrid)
                {
                    draggingPanel.rect.x = Mathf.Round(draggingPanel.rect.x / SnapSize) * SnapSize;
                    draggingPanel.rect.y = Mathf.Round(draggingPanel.rect.y / SnapSize) * SnapSize;
                }
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                Panel deck = list[i];
                if (!deck.open || deck.closing) continue;
                if (!deck.rect.Contains(point)) continue;
                float localY = point.y - deck.rect.y;
                float localX = point.x - deck.rect.x;
                bool overHeaderButtons = localY <= 26 && localX >= deck.rect.width - 52;
                if (localY <= 26 && !overHeaderButtons)
                {
                    list.RemoveAt(i);
                    list.Add(deck);
                    draggingOffset = new Vector2(localX, localY);
                    draggingPanel = deck;
                    deck.hasTarget = false;
                    layoutManaged = false;
                    current.Use();
                }
                else if (options.showGrips && new Rect(deck.rect.xMax - 16, deck.rect.yMax - 16, 16, 16).Contains(point))
                {
                    list.RemoveAt(i);
                    list.Add(deck);
                    resizeStart = point;
                    resizeOrigin = new Vector2(deck.rect.width, deck.rect.height);
                    resizingPanel = deck;
                    deck.hasTarget = false;
                    layoutManaged = false;
                    current.Use();
                }
                break;
            }
        }

        private void DrawPanelBody(Panel deck)
        {
            Rect deckRect = deck.rect;
            float appear = Mathf.Clamp01(deck.appear);
            Matrix4x4 before = GUI.matrix;
            if (appear < 0.999f)
            {
                float scale = deck.closing ? Mathf.Clamp01(appear) : 0.85f + 0.15f * appear;
                GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), deckRect.center);
            }
            GUI.BeginGroup(deckRect);
            Color savedColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, savedColor.a * (deck.closing ? 1f : appear));
            float w = deckRect.width;
            float h = deckRect.height;

            if (options.panelShadows)
                Box(new Rect(3, 5, w, h), Alpha(Color.black, 0.35f), 6);
            Box(new Rect(0, 0, w, h), Alpha(border, options.uiOpacity), 6);
            Box(new Rect(1, 1, w - 2, h - 2), Alpha(background, options.uiOpacity), 5);
            if (deck.kind == PanelLauncher)
            {
                DrawBrand(new Rect(12, 6, 150, 24));
            }
            else
            {
                Label(new Rect(12, 0, w - 60, 26), deck.title, tabStyle, bright);
                Box(new Rect(12, 26, w - 24, 1), Alpha(border, 0.7f), 0);
                if (IconButton(new Rect(w - 44, 5, 16, 16), deck.collapsed ? "plus" : "minus", "Collapse")) ToggleCollapse(deck);
                if (IconButton(new Rect(w - 22, 5, 16, 16), "x", "Close")) ClosePanel(deck);
            }

            float full = deck.height;
            float bodyFade = Mathf.Clamp01(1f - deck.collapse);
            if (deck.kind == PanelLauncher) DrawLauncherBody(deck, w, h);
            else if (bodyFade > 0.01f)
            {
                Color body = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, body.a * bodyFade);
                switch (deck.kind)
                {
                    case PanelControls: DrawControlsBody(deck, w, full); break;
                    case PanelConsole: DrawConsoleBody(deck, w, full); break;
                    case PanelSettings: DrawSettingsBody(deck, w, full); break;
                    case PanelInput: DrawInputSettings(new Rect(8, 32, w - 16, full - 40), false); break;
                    case PanelLayout: DrawLayoutBody(deck, w, full); break;
                    default: DrawModulesBody(deck, w, full); break;
                }
                GUI.color = body;
            }

            if (bodyFade > 0.5f && options.showGrips)
            {
                bool gripHover = pointerInside && new Rect(w - 18, h - 18, 18, 18).Contains(GuiPoint);
                float glow = Animate("grip-" + deck.key, gripHover ? 1 : 0);
                Color grip = Alpha(Color.Lerp(muted, accent, glow), 0.35f + glow * 0.65f);
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c <= r; c++)
                        Box(new Rect(w - 8 + c * 3, h - 8 + (2 - r) * 3, 2, 2), grip, 1);
            }

            GUI.color = savedColor;
            GUI.EndGroup();
            GUI.matrix = before;
        }

        private void DrawBrand(Rect rect)
        {
            Texture2D logo = customWatermark ?? watermarkImage;
            if (!disableWatermark && logo != null)
            {
                Color.RGBToHSV(accent, out float _, out float sat, out float _);
                if (logoRecolor == null || logoRecolorSource != logo ||
                    (Time.unscaledTime - logoRecolorAt > 0.12f && Mathf.Abs(logoRecolorHue - sat) > 0.02f))
                {
                    Texture2D recolored = RecolorLogo(logo, accent);
                    if (recolored != null)
                    {
                        if (logoRecolor != null) Destroy(logoRecolor);
                        logoRecolor = recolored;
                        logoRecolorSource = logo;
                        logoRecolorHue = sat;
                        logoRecolorAt = Time.unscaledTime;
                    }
                }
                TintTexture(new Rect(rect.x, rect.y + 1, 22, 22), logoRecolor != null ? logoRecolor : logo,
                    logoRecolor != null ? Color.white : Color.Lerp(bright, accent, 0.6f));
            }
            Label(new Rect(rect.x + 30, rect.y - 1, 120, 20), "Poison", titleStyle, bright);
            Label(new Rect(rect.x + 30, rect.y + 16, 120, 14), "v" + PluginInfo.Version, smallStyle, muted);
        }

        private void DrawLauncherBody(Panel panel, float w, float h)
        {
            string value = Input(new Rect(8, 42, w - 16, 24), search, "search", "Search...");
            if (focusSearch) { focusedInput = "ui-search"; inputText = search; focusSearch = false; }
            if (value != search)
            {
                search = value;
                if (search.Trim().Length > 0) BeginSearch();
                else if (panelMap.TryGetValue(PanelSearch + ":", out Panel sp)) sp.open = false;
                refresh = true;
            }

            float footer = 56f;
            int rowCount = 3 + categories.Count;
            BeginScroll(new Rect(8, 72, w - 16, h - 72 - footer), panel.scroll, rowCount * 24f);
            LauncherRow(panel, "Search all", PanelSearch, null, 0);
            LauncherRow(panel, "Favorites", PanelFavorites, null, 1);
            LauncherRow(panel, "Active", PanelActive, null, 2);
            for (int i = 0; i < categories.Count; i++)
                LauncherRow(panel, categories[i], PanelCategory, categories[i], i + 3);
            EndScroll();

            if (TextButton(new Rect(8, h - 48, w - 16, 22), "WYVERN WARNING")) SetClassic(true);
            float bw = (w - 16 - 8) / 3f;
            if (TextButton(new Rect(8, h - 22, bw, 18), "Controls")) TogglePanel(PanelControls, null);
            if (TextButton(new Rect(12 + bw, h - 22, bw, 18), "Console")) TogglePanel(PanelConsole, null);
            if (TextButton(new Rect(16 + bw * 2, h - 22, bw, 18), "Settings")) TogglePanel(PanelSettings, null);
        }

        private void LauncherRow(Panel launcher, string label, int kind, string category, int index)
        {
            Rect row = new Rect(0, index * 24, launcher.rect.width - 30, 22);
            float hover = Hover("launch-" + kind + category, row);
            bool open = panelMap.TryGetValue(kind + ":" + (category ?? ""), out Panel existing) && existing.open;
            Box(row, Alpha(accent, (open ? 0.2f : 0f) + hover * 0.08f), 3);
            if (MenuButton(row, new GUIContent("", label), GUIStyle.none)) TogglePanel(kind, category);
            Label(new Rect(row.x + 12, row.y, row.width - 16, row.height), label, smallStyle, open ? bright : Color.Lerp(muted, bright, hover * 0.6f));
        }

        private readonly List<Entry> panelEntries = new List<Entry>();

        private void DrawModulesBody(Panel panel, float w, float h)
        {
            List<Entry> list = FilterPanel(panel, panelEntries);
            if (panel.kind == PanelSearch)
            {
                string value = Input(new Rect(8, 30, w - 16, 22), search, "search", "Search...");
                if (value != search) { search = value; BeginSearch(); }
                if (searchIndexing)
                {
                    Spinner(new Vector2(w / 2f, h / 2f), 16f, "Searching");
                    return;
                }
                Label(new Rect(10, 56, w - 20, 16), list.Count + " results", smallStyle, muted);
                float searchRows = options.rowHeight;
                BeginScroll(new Rect(8, 76, w - 16, h - 84), panel.scroll, list.Count * searchRows);
                for (int i = 0; i < list.Count; i++)
                {
                    float sy = i * searchRows;
                    if (sy + searchRows < panel.scroll.value || sy > panel.scroll.value + (h - 84)) continue;
                    DrawModuleRow(list[i], panel, new Rect(0, sy, w - 30, searchRows));
                }
                if (list.Count == 0) Label(new Rect(0, 86, w - 30, 24), "No results", buttonStyle, muted);
                EndScroll();
                return;
            }
            Label(new Rect(10, 30, w - 20, 16), panel.kind == PanelActive ? list.Count + " enabled" : list.Count + " modules", smallStyle, muted);
            float rowHeight = options.rowHeight;
            BeginScroll(new Rect(8, 50, w - 16, h - 58), panel.scroll, list.Count * rowHeight);
            for (int i = 0; i < list.Count; i++)
            {
                float y = i * rowHeight;
                if (y + rowHeight < panel.scroll.value || y > panel.scroll.value + (h - 58)) continue;
                DrawModuleRow(list[i], panel, new Rect(0, y, w - 30, rowHeight));
            }
            if (list.Count == 0)
                Label(new Rect(0, 60, w - 30, 24), search.Trim().Length > 0 ? "No results" : "Empty", buttonStyle, muted);
            EndScroll();
        }

        private void DrawModuleRow(Entry entry, Panel deck, Rect rect)
        {
            ButtonInfo button = entry.button;
            bool hover = pointerInside && rect.Contains(GuiPoint);
            bool blocked = button.detected && !allowDetected;
            if (!switches.TryGetValue(button, out float amount)) amount = button.enabled ? 1 : 0;
            if (Event.current.type == EventType.Repaint)
            {
                amount = Mathf.Lerp(amount, button.enabled ? 1 : 0, options.animations ? Ease(16) : 1);
                switches[button] = amount;
            }
            float lift = Hover("grow-" + deck.key + "-" + button.buttonText, rect);
            Box(rect, Alpha(accent, amount * 0.22f * options.accentAmount + lift * 0.08f), 3);
            if (hover) tip = blocked ? "Enable detected mods in the Detected Mods category first" : entry.title + ": " + entry.description;

            bool starred = favorites.Contains(button.buttonText);
            Label(new Rect(rect.x + 10, rect.y, rect.width - 42, rect.height), entry.title, smallStyle,
                blocked ? muted : button.enabled ? accent : Color.Lerp(bright, accent, lift * 0.5f));

            if (button.label) return;

            if (IconButton(new Rect(rect.xMax - 30, rect.y + 1, 20, 20), "star", "Favorite", starred ? accent : muted))
            {
                actions.Enqueue(() =>
                {
                    if (favorites.Contains(button.buttonText)) favorites.Remove(button.buttonText);
                    else favorites.Add(button.buttonText);
                    Preferences.Save();
                });
            }

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !blocked;
            Rect hit = new Rect(rect.x, rect.y, rect.width - 30, rect.height);
            Event current = InputEvent;
            bool leftClick = Pressed(hit);
            bool rightClick = Pressed(hit, 1);
            if (leftClick) { Activate(button, true); current.Use(); }
            else if (rightClick && button.incremental) { Activate(button, false); current.Use(); }
            GUI.enabled = enabled;
        }

        private void DrawLayoutBody(Panel deck, float w, float h)
        {
            Label(new Rect(10, 30, w - 20, 16), "Drag panels to arrange them, grid snaps to " + SnapSize + "px", smallStyle, muted);
            int visibleCount = panels.Count;
            BeginScroll(new Rect(8, 50, w - 16, h - 92), deck.scroll, visibleCount * 30f);
            for (int i = 0; i < visibleCount; i++)
            {
                Panel target = panels[i];
                Rect row = new Rect(0, i * 30, w - 36, 28);
                Box(row, Alpha(accent, target.open ? 0.1f : 0.03f), 3);
                string label = target.title + (target.category != null ? " (" + target.category + ")" : "");
                Label(new Rect(row.x + 8, row.y, row.width - 130, row.height), label, smallStyle, target.open ? bright : muted);
                Label(new Rect(row.x + row.width - 122, row.y, 58, row.height), Mathf.RoundToInt(target.rect.x) + "," + Mathf.RoundToInt(target.rect.y), smallStyle, muted);
                if (TextButton(new Rect(row.x + row.width - 60, row.y + 3, 56, 22), target.open ? "Hide" : "Show"))
                {
                    target.open = !target.open;
                    if (target.open) { panels.RemoveAt(i); panels.Add(target); }
                }
            }
            EndScroll();
            float bw = (w - 16 - 12) / 4f;
            if (TextButton(new Rect(8, h - 60, bw, 24), "Auto sort")) AutoSortPanels();
            if (TextButton(new Rect(12 + bw, h - 60, bw, 24), "Save")) SaveLayout();
            if (TextButton(new Rect(16 + bw * 2, h - 60, bw, 24), "Reset")) ResetLayout();
            if (TextButton(new Rect(20 + bw * 3, h - 60, bw, 24), layoutMode ? "Edit on" : "Edit off")) layoutMode = !layoutMode;
            if (TextButton(new Rect(8, h - 32, w - 16, 24), "Auto arrange on open: " + (options.autoArrange ? "on" : "off"))) { options.autoArrange = !options.autoArrange; Changed(); }
        }

        private bool layoutManaged;

        private void AutoSortPanels()
        {
            layoutManaged = true;
            float scale = ScreenScale();
            float sw = ViewWidth / scale;
            float sh = ViewHeight / scale;
            float margin = SnapSize;
            float gap = Mathf.Max(4f, SnapSize);
            var order = panels.Where(p => p.open && !p.closing)
                .OrderBy(p => p.kind == PanelLauncher ? 0 : 1)
                .ThenByDescending(p => p.height)
                .ToList();
            if (order.Count == 0) return;

            List<Rect> rects = null;
            for (float fit = 1f; fit >= 0.4f; fit -= 0.04f)
            {
                if (PackPanels(order, fit, sw, sh, margin, gap, out rects)) break;
            }
            if (rects == null) PackPanels(order, 0.4f, sw, sh, margin, gap, out rects);

            for (int i = 0; i < order.Count; i++)
            {
                order[i].target = rects[i];
                order[i].hasTarget = true;
            }
            ShowStatus("Panels sorted");
        }

        private bool PackPanels(List<Panel> order, float fit, float sw, float sh, float margin, float gap, out List<Rect> rects)
        {
            rects = new List<Rect>();
            float x = margin;
            float y = margin;
            float columnWidth = 0f;
            foreach (Panel panel in order)
            {
                float width = Snap(Mathf.Max(120f, panel.baseWidth * fit));
                float height = Snap(Mathf.Max(80f, panel.baseHeight * fit));
                if (y + height > sh - margin && y > margin)
                {
                    x += columnWidth + gap;
                    y = margin;
                    columnWidth = 0f;
                }
                if (x + width > sw - margin) { rects = null; return false; }
                rects.Add(new Rect(Snap(x), Snap(y), width, height));
                y += height + gap;
                columnWidth = Mathf.Max(columnWidth, width);
            }
            return true;
        }

        private void SaveLayout()
        {
            options.panelLayout = new List<PanelLayoutEntry>();
            foreach (Panel target in panels)
                options.panelLayout.Add(new PanelLayoutEntry { key = target.key, x = target.rect.x, y = target.rect.y, open = target.open });
            Changed();
            ShowStatus("Layout saved");
        }

        private void ResetLayout()
        {
            options.panelLayout = null;
            cascade = 0;
            foreach (Panel target in panels) target.open = false;
            panelMap.Clear();
            panels.Clear();
            EnsurePanels();
            ShowStatus("Layout reset");
        }

        private void ApplySavedLayout()
        {
            if (options.panelLayout == null) return;
            cascade = 0;
            foreach (PanelLayoutEntry saved in options.panelLayout)
            {
                string[] parts = saved.key.Split(':');
                if (parts.Length < 2 || !int.TryParse(parts[0], out int kind)) continue;
                string category = string.IsNullOrEmpty(parts[1]) ? null : parts[1];
                Panel deck = OpenPanel(kind, category);
                deck.rect.x = saved.x;
                deck.rect.y = saved.y;
                deck.open = saved.open;
            }
        }

        private void Activate(ButtonInfo button, bool increment)
        {
            actions.Enqueue(() =>
            {
                if (button.label || (button.detected && !allowDetected)) return;
                if (button.incremental) Main.ToggleIncremental(button.buttonText, increment, true, true);
                else Main.Toggle(button, true, true);
            });
        }

        private void DrawControlsBody(Panel deck, float w, float h)
        {
            float pw = (w - 24) / 2f;
            Box(new Rect(8, 30, pw, h - 38), panel, 4);
            Label(new Rect(20, 40, pw - 24, 20), "Room", smallStyle, accent);
            roomCode = Input(new Rect(20, 64, pw - 24, 30), roomCode, "room", "Room code");
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !string.IsNullOrWhiteSpace(roomCode);
            if (TextButton(new Rect(20, 100, (pw - 28) / 2, 28), "Join"))
            {
                string code = roomCode.Trim().ToUpperInvariant();
                actions.Enqueue(() =>
                {
                    if (PhotonNetworkController.Instance == null) throw new InvalidOperationException("Room controls are not ready.");
                    PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(code, JoinType.Solo);
                    ShowStatus("Joining " + code);
                });
            }
            if (TextButton(new Rect(24 + (pw - 28) / 2, 100, (pw - 28) / 2, 28), "Queue"))
            {
                string code = roomCode.Trim().ToUpperInvariant();
                actions.Enqueue(() => { Mods.Important.QueueRoom(code); ShowStatus("Queued " + code); });
            }
            GUI.enabled = enabled;
            if (TextButton(new Rect(20, 134, pw - 24, 28), "Disconnect"))
            {
                ButtonInfo button = Buttons.GetIndex("Disconnect");
                if (button != null) Activate(button, true);
            }

            Box(new Rect(16 + pw, 30, pw, h - 38), panel, 4);
            Label(new Rect(28 + pw, 40, pw - 24, 20), "Player", smallStyle, accent);
            playerName = Input(new Rect(28 + pw, 64, pw - 96, 30), playerName, "name", "Name");
            GUI.enabled = enabled && !string.IsNullOrWhiteSpace(playerName);
            if (TextButton(new Rect(w - 60, 64, 44, 30), "Set"))
            {
                string name = playerName.Trim();
                actions.Enqueue(() => { ChangeName(name); ShowStatus("Name updated"); });
            }
            GUI.enabled = enabled;
            Label(new Rect(28 + pw, 100, 20, 18), "R", smallStyle);
            Label(new Rect(28 + pw + 60, 100, 20, 18), "G", smallStyle);
            Label(new Rect(28 + pw + 120, 100, 20, 18), "B", smallStyle);
            red = Input(new Rect(28 + pw, 118, 52, 26), red, "red", "R");
            green = Input(new Rect(88 + pw, 118, 52, 26), green, "green", "G");
            blue = Input(new Rect(148 + pw, 118, 52, 26), blue, "blue", "B");
            byte r = 0, g = 0, b = 0;
            bool valid = byte.TryParse(red, out r) && byte.TryParse(green, out g) && byte.TryParse(blue, out b);
            GUI.enabled = enabled && valid;
            if (TextButton(new Rect(208 + pw, 118, 60, 26), "Apply"))
            {
                Color color = new Color32(r, g, b, 255);
                actions.Enqueue(() => { ChangeColor(color); ShowStatus("Color updated"); });
            }
            GUI.enabled = enabled;
            if (valid) Box(new Rect(274 + pw, 118, 24, 24), new Color32(r, g, b, 255), 3);
        }

        private void DrawConsoleBody(Panel deck, float w, float h)
        {
            string[] snapshot;
            lock (lines) snapshot = lines.ToArray();
            float contentHeight = 8;
            foreach (string line in snapshot) contentHeight += wrapStyle.CalcHeight(new GUIContent(line), w - 60) + 6;
            BeginScroll(new Rect(8, 30, w - 16, h - 82), deck.scroll, contentHeight);
            float y = 4;
            foreach (string line in snapshot)
            {
                float lineHeight = wrapStyle.CalcHeight(new GUIContent(line), w - 60);
                Label(new Rect(6, y, w - 60, lineHeight), line, wrapStyle);
                y += lineHeight + 6;
            }
            EndScroll();
            command = Input(new Rect(8, h - 44, w - 74, 36), command, "command", "Command");
            if (TextButton(new Rect(w - 62, h - 44, 54, 36), "Run") || (inputSubmit && submitInput == "ui-command"))
            {
                string value = command;
                command = "";
                actions.Enqueue(() => HandleDebugCommand(value));
            }
        }

        private void DrawTip()
        {
            if (tip != lastTip) { lastTip = tip; tipSince = Time.unscaledTime; }
            if (!options.tooltips || tip.Length < 40 || Time.unscaledTime - tipSince < 0.5f) return;
            float opacity = Mathf.Clamp01((Time.unscaledTime - tipSince - 0.5f) * 7);
            if (!options.animations) opacity = 1;
            Color before = GUI.color;
            GUI.color = new Color(1, 1, 1, before.a * opacity);
            float height = Mathf.Min(360, wrapStyle.CalcHeight(new GUIContent(tip), 366) + 24);
            Vector2 mouse = GuiPoint;
            float x = Mathf.Clamp(mouse.x + 16, 12, 658);
            float y = Mathf.Clamp(mouse.y + 20 + (1 - opacity) * 6, 12, 634 - height);
            Box(new Rect(x, y, 390, height), border, 7);
            Box(new Rect(x + 1, y + 1, 388, height - 2), panel, 6);
            Label(new Rect(x + 12, y + 12, 366, height - 24), tip, wrapStyle);
            GUI.color = before;
        }

        private void DrawPrompt()
        {
            PromptData prompt = CurrentPrompt;
            if (lastPrompt != prompt)
            {
                lastPrompt = prompt;
                promptInput = prompt.IsText ? keyboardInput ?? "" : "";
                promptScroll.Reset();
                ClearInput();
            }
            float scale = ScreenScale();
            float sx = (ViewWidth / scale - 560) / 2f;
            float sy = (ViewHeight / scale - 365) / 2f;
            Box(new Rect(sx - 260, sy - 165, 1080, 700), new Color(0, 0, 0, 0.6f), 0);
            Box(new Rect(sx, sy, 560, 365), border, 8);
            Box(new Rect(sx + 1, sy + 1, 558, 363), panel, 7);
            Label(new Rect(sx + 26, sy + 22, 490, 28), "Poison", titleStyle);
            string message = Plain(prompt.Message);
            float height = wrapStyle.CalcHeight(new GUIContent(message), 484);
            BeginScroll(new Rect(sx + 26, sy + 74, 508, 155), promptScroll, height);
            Label(new Rect(0, 0, 484, height), message, wrapStyle);
            EndScroll();
            if (prompt.IsText)
            {
                promptInput = Input(new Rect(sx + 26, sy + 242, 508, 34), keyboardInput ?? "", "prompt", "Enter text");
                keyboardInput = promptInput;
            }
            if (TextButton(new Rect(sx + 26, sy + 301, 248, 38), Plain(prompt.AcceptText ?? "OK")))
                actions.Enqueue(() => { if (CurrentPrompt == prompt) Main.Toggle("Accept Prompt", true, true); });
            if (prompt.DeclineText != null && TextButton(new Rect(sx + 286, sy + 301, 248, 38), Plain(prompt.DeclineText)))
                actions.Enqueue(() => { if (CurrentPrompt == prompt) Main.Toggle("Decline Prompt", true, true); });
            if (InputEvent.isMouse || InputEvent.type == EventType.ScrollWheel) InputEvent.Use();
        }

        private string Input(Rect rect, string value, string name, string placeholder)
        {
            string id = "ui-" + name;
            bool focused = focusedInput == id;
            float amount = Animate("input-" + name, focused ? 1 : 0);
            Box(rect, Color.Lerp(border, accent, amount), 4);
            Box(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), background, 3);

            if (Pressed(rect))
            {
                focusedInput = id;
                inputText = value;
                inputClicked = true;
                focused = true;
                InputEvent.Use();
            }

            Rect inner = new Rect(rect.x + 8, rect.y, rect.width - 16, rect.height);
            string text = focused ? inputText : value;
            if (text.Length == 0 && !focused) Label(inner, placeholder, smallStyle);
            else Label(inner, text, textStyle);
            if (focused) Caret(inner, text);
            return text;
        }

        private void Caret(Rect rect, string text)
        {
            if (Mathf.Repeat(Time.unscaledTime, 1f) >= 0.5f) return;
            float width = Mathf.Min(textStyle.CalcSize(new GUIContent(text)).x, rect.width - 2);
            Box(new Rect(rect.x + width + 1, rect.y + 4, 1, rect.height - 8), bright, 0);
        }

        private void ClearInput()
        {
            focusedInput = null;
            inputText = "";
        }

        private void UpdateTextInput(Keyboard keyboard)
        {
            if (!isOpen || keyboard == null || string.IsNullOrEmpty(focusedInput)) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                submitInput = focusedInput;
                inputSubmit = true;
                ClearInput();
                return;
            }
            if (keyboard.backspaceKey.isPressed && (keyboard.backspaceKey.wasPressedThisFrame || Time.unscaledTime >= backspaceNext))
            {
                backspaceNext = Time.unscaledTime + (keyboard.backspaceKey.wasPressedThisFrame ? 0.4f : 0.045f);
                if (inputText.Length > 0) inputText = inputText.Substring(0, inputText.Length - 1);
            }
        }

        private void OnTextInput(char character)
        {
            if (!isOpen || string.IsNullOrEmpty(focusedInput) || character < ' ' || character == (char)127) return;
            if (inputText.Length < 256) inputText += character;
        }

        private int padPressFrame = -1;
        private int padPressButton;
        private Vector2 padPressPoint;

        private bool Pressed(Rect rect, int button = 0)
        {
            Event current = InputEvent;
            bool hit = rect.Contains(GuiPoint);
            bool fired = current.type == EventType.MouseDown && current.button == button;
            if (!fired && padPressFrame == Time.frameCount && padPressButton == button) fired = true;
            return fired && PointerControl == 0 && GUI.enabled && pointerInside && hit;
        }

        private bool Clicked(Rect rect)
        {
            if (!guiPad) return Pressed(rect);
            if (!Pressed(rect)) return false;
            padPressFrame = -1;
            return true;
        }

        private bool TextButton(Rect rect, string text)
        {
            float hover = Hover("button-" + text + rect.x, rect);
            float pressed = Animate("press-" + text + rect.x, GUI.enabled && pointerInside && rect.Contains(GuiPoint) && PointerHeld ? 1 : 0, 24);
            Rect drawn = new Rect(rect.x + pressed, rect.y + pressed, rect.width - pressed * 2, rect.height - pressed * 2);
            Box(drawn, Color.Lerp(border, accent, hover * 0.4f), 4);
            Box(new Rect(drawn.x + 1, drawn.y + 1, drawn.width - 2, drawn.height - 2), Color.Lerp(panel, accent, hover * 0.12f), 3);
            bool clicked = Clicked(rect);
            Label(drawn, text, buttonStyle, Color.Lerp(bright, accent, hover * 0.35f));
            return clicked;
        }

        private bool IconButton(Rect rect, string name, string tooltip, Color? color = null)
        {
            float hover = Hover("icon-" + name + rect.x + rect.y, rect);
            float pressed = Animate("icon-press-" + name + rect.x + rect.y, GUI.enabled && pointerInside && rect.Contains(GuiPoint) && PointerHeld ? 1 : 0, 24);
            Rect drawn = new Rect(rect.x + pressed, rect.y + pressed, rect.width - pressed * 2, rect.height - pressed * 2);
            float amount = Mathf.Max(hover, pressed);
            if (amount > 0.01f) Box(drawn, Alpha(accent, amount * 0.18f), 3);
            if (pointerInside && rect.Contains(GuiPoint)) tip = tooltip;
            bool clicked = Clicked(rect);
            float size = 14 + hover * 2;
            Icon(name, new Rect(drawn.x + (drawn.width - size) / 2, drawn.y + (drawn.height - size) / 2, size, size), color ?? Color.Lerp(muted, accent, hover));
            return clicked;
        }

        private void Icon(string name, Rect rect, Color color)
        {
            if (!icons.TryGetValue(name, out Texture2D texture))
            {
                try
                {
                    using (Stream stream = typeof(UI).Assembly.GetManifestResourceStream(PluginInfo.ClientResourcePath + ".lucide-" + name + ".svg"))
                    {
                        if (stream == null) throw new FileNotFoundException("Missing icon: " + name);
                        using (var reader = new StreamReader(stream)) texture = Svg.Parse(reader.ReadToEnd());
                    }
                }
                catch (Exception error) { DebugPrint(error.Message); }
                icons[name] = texture;
            }
            if (texture != null) TintTexture(rect, texture, color);
        }

        private static Texture2D RecolorLogo(Texture2D source, Color accent)
        {
            try
            {
                Color[] pixels = source.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color pixel = pixels[i];
                    if (pixel.a <= 0.001f) continue;
                    Color.RGBToHSV(pixel, out float _, out float s, out float v);
                    Color target = s <= 0.08f ? Color.Lerp(Color.white, accent, 0.5f) : accent;
                    pixel.r *= target.r;
                    pixel.g *= target.g;
                    pixel.b *= target.b;
                    float lift = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(v * 1.3f));
                    pixels[i] = new Color(pixel.r * lift, pixel.g * lift, pixel.b * lift, pixel.a);
                }
                var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    name = "Logo Recolor",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                result.SetPixels(pixels);
                result.Apply(false, false);
                return result;
            }
            catch { return null; }
        }

        private static void TintTexture(Rect rect, Texture2D texture, Color color)
        {
            Color previous = GUI.color;
            GUI.color = previous * color;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        private void Box(Rect rect, Color color, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (rect.width <= 0f || rect.height <= 0f) return;
            float corner = radius * options.rounding;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, corner);
        }

        private void Spinner(Vector2 center, float radius, string label)
        {
            if (Event.current.type != EventType.Repaint) return;
            float spin = Time.unscaledTime * 240f;
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                float angle = (spin + t * 300f) * Mathf.Deg2Rad;
                float alpha = t * 0.9f + 0.1f;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Box(new Rect(point.x - 1.5f, point.y - 1.5f, 3, 3), Alpha(accent, alpha), 1.5f);
            }
            Label(new Rect(center.x - 80, center.y + radius + 10, 160, 18), label, buttonStyle, muted);
        }

        private void Slash(Rect rect, Color color, bool flip)
        {
            if (Event.current.type != EventType.Repaint || rect.height <= 0f) return;
            Color previous = GUI.color;
            GUI.color = previous * color;
            int steps = Mathf.Max(1, Mathf.CeilToInt(rect.height));
            float width = Mathf.Max(1f, rect.width);
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                float x = flip ? rect.x + t * (width - 1f) : rect.xMax - t * (width - 1f) - 1f;
                GUI.DrawTexture(new Rect(x, rect.y + i, 1f, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            }
            GUI.color = previous;
        }

        private static float ScreenScale() => Mathf.Max(0.1f, Mathf.Min(Instance?.options.scale ?? 1,
            Mathf.Min(ViewWidth / 1120f, ViewHeight / (Instance?.options.classicUI == true ? 552f : 726f))));

        private void Label(Rect rect, string text, GUIStyle style, Color? color = null)
        {
            Color previous = GUI.color;
            Color ink = color ?? (style == smallStyle || style == wrapStyle || style == rightStyle ? muted : bright);
            GUI.color = new Color(ink.r, ink.g, ink.b, previous.a * ink.a);
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private void ShowStatus(string value)
        {
            string text = Plain(value);
            status = text;
            statusUntil = Time.unscaledTime + 5;
            motions.Remove("status");
            NotificationManager.SendNotification("<color=grey>[</color><color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">Poison</color><color=grey>]</color> " + text);
        }

        public void DebugPrint(string text)
        {
            lock (lines)
            {
                lines.Add(Plain(text));
                if (lines.Count > 200) lines.RemoveAt(0);
            }
        }

        public void HandleDebugCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            string[] args = command.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string commandName = args[0].ToLowerInvariant();

            switch (commandName)
            {
                case "print":
                    {
                        DebugPrint(args.Skip(1).Join(" "));
                        break;
                    }
                case "admin":
                    {
                        string id = args.Length > 1 ? args[1] : PhotonNetwork.LocalPlayer?.UserId;
                        string name = args.Length > 2 ? args[2] : PhotonNetwork.LocalPlayer?.NickName;

                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        {
                            ServerData.LocalAdmins[id] = name;
                            DebugPrint($"Added ({id}, {name}) to local administrators");
                        }

                        break;
                    }
                case "beta":
                    {
                        PluginInfo.BetaBuild = args.Length > 1 && args[1].Equals("true", StringComparison.OrdinalIgnoreCase);
                        DebugPrint($"PluginInfo.BetaBuild is now {PluginInfo.BetaBuild}");
                        break;
                    }
                case "telemetry":
                    {
                        ServerData.DisableTelemetry = args.Length < 2 || args[1] == "false";
                        DebugPrint($"Telemetry is now {(ServerData.DisableTelemetry ? "disabled" : "enabled")}");
                        break;
                    }
                case "prompt":
                    {
                        MatchCollection matches = Regex.Matches(args.Skip(1).Join(" "), @"\[(.*?)\]");
                        string promptText = matches.Count > 0 ? matches[0].Groups[1].Value : args.Length > 1 ? args[1] : "Prompt text";
                        string acceptText = matches.Count > 1 ? matches[1].Groups[1].Value : args.Length > 2 ? args[2] : "Accept";
                        string declineText = matches.Count > 2 ? matches[2].Groups[1].Value : args.Length > 3 ? args[3] : "Decline";

                        Prompt(promptText, () => DebugPrint("Prompt accepted"), () => DebugPrint("Prompt declined"), acceptText, declineText);
                        DebugPrint($"Prompted user {promptText} {acceptText} {declineText}");

                        break;
                    }
                case "exit":
                case "quit":
                case "close":
                    {
                        Application.Quit();
                        break;
                    }
                default:
                    {
                        DebugPrint($"Unknown command: '{commandName}'");
                        break;
                    }
            }
        }
    }
}
