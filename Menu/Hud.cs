using Poison.Classes.Menu;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Poison.Menu
{
    public class Hud : MonoBehaviour
    {
        public static Hud Instance;
        public static bool InUse => active && Instance != null && Instance.isActiveAndEnabled;
        public static bool HasMouse => InUse && Instance.open && !XRSettings.isDeviceActive;

        static bool active;

        public bool IsOpen => open;
        public bool IsTyping => false;
        public bool RightHand = true;

        const float OrbRadius = 0.72f;
        const float WindowScale = 0.0014f;
        const float WindowWidth = 250f;
        const float TitleHeight = 26f;
        const float RowGap = 2f;
        const int MaxRows = 8;
        const float SideYaw = -20f * Mathf.Deg2Rad;
        const float SidePitch = 0f;
        const string SidebarKey = "\u0001sidebar";
        const string ResultsKey = "\u0001results";
        const string KeyboardKey = "\u0001keyboard";
        const string FontPath = "Environment Objects/LocalObjects_Prefab/TreeRoom/Text (14)";

        static TMP_FontAsset font;
        static bool probed;
        static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
        static readonly List<TMP_Text> texts = new List<TMP_Text>();

        class State
        {
            public bool collapsed;
            public bool openWindow;
            public float scroll;
            public float yaw;
            public float pitch;
            public bool placed;
        }

        static readonly Dictionary<string, State> states = new Dictionary<string, State>();

        static State GetState(string name)
        {
            if (!states.TryGetValue(name, out State state)) { state = new State(); states[name] = state; }
            return state;
        }

        class Window
        {
            public string title;
            public ButtonInfo[] group;
            public State state;
            public GameObject root;
            public RectTransform rect;
            public RectTransform titleRect;
            public RectTransform body;
            public RectTransform content;
            public RectTransform track;
            public Image thumb;
            public CanvasGroup fade;
            public bool sidebar;
            public bool dragging;
            public float born;
            public float delay;
            public float yaw, pitch;
            public float handYaw, handPitch;
            public float startYaw, startPitch;
            public float show;
            public float showTarget;
            public float collapse;
            public float collapseTarget;
            public float expanded;
            public float bodyFull;
            public float contentFull;
            public float rowStep;
            public float maxScroll;
            public float scroll;
            public float grab;
            public bool scrollDrag;
        }

        struct Item
        {
            public RectTransform rect;
            public RectTransform clip;
            public Image fill;
            public TMP_Text label;
            public Color baseColor;
            public Color hoverColor;
            public Action act;
            public bool hovered;
        }

        struct Title
        {
            public RectTransform rect;
            public Window window;
        }

        struct Tween
        {
            public float start;
            public float duration;
            public Action<float> step;
        }

        readonly List<Window> windows = new List<Window>();
        readonly List<Item> items = new List<Item>();
        readonly List<Title> titles = new List<Title>();
        readonly List<Tween> tweens = new List<Tween>();

        GameObject root;
        Vector3 orbCenter;
        Quaternion orbRotation;
        bool placed;
        bool visible;
        bool open;
        float openAmount;
        bool cursorHeld;
        bool cursorVisible;
        CursorLockMode cursorLock;
        Window sidebar;
        float fpsTime;
        float fps;
        int fpsCount;
        bool pcMode;
        float nextThemeCheck;
        Theme themeStamp;

        LineRenderer laser;
        GameObject pointer;
        Image pointerOuter, pointerInner, pointerPulse;
        float pulseTime = -10f;
        float triggerWas, gripWas;
        bool dragging;
        Window dragWindow;
        Window scrollWindow;
        Material overlayMaterial;

        string search = "";
        bool keyboardOpen;
        bool disconnectShown;
        Window keyboardWindow;
        Window resultsWindow;
        TMP_Text searchLabel;

        public static bool ShowFps = true;
        public static bool ShowClock = true;
        public static bool LaserOn = true;
        public static bool Draggable = true;

        void Awake()
        {
            Instance = this;
        }

        void OnDisable()
        {
            SetOpen(false);
            ReleaseCursor();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (overlayMaterial != null) Destroy(overlayMaterial);
            if (pointer != null) Destroy(pointer);
            if (laser != null) Destroy(laser.gameObject);
            if (root != null) Destroy(root);
            windows.Clear();
            items.Clear();
            titles.Clear();
            texts.Clear();
        }

        public static void SetEnabled(bool value)
        {
            bool wasOpen = Instance != null && Instance.open || Main.menu != null;
            active = value;
            if (Instance == null) return;
            if (!InUse)
            {
                Instance.SetOpen(false);
                if (wasOpen && Main.menu == null && !Main.Lockdown) Main.OpenMenu();
                return;
            }
            ClearMenu();
            Mods.Settings.DestroyKeyboard();
            Instance.SetOpen(wasOpen && !Main.Lockdown);
        }

        public static void ClearMenu()
        {
            if (Main.menu != null)
            {
                if (Main.promptVideoPlayer != null) Main.promptVideoPlayer.Stop();
                Main.menu.SetActive(false);
                Destroy(Main.menu);
                Main.menu = null;
            }
            if (Main.reference != null)
            {
                Main.reference.SetActive(false);
                Destroy(Main.reference);
                Main.reference = null;
            }
        }

        public void SetOpen(bool value)
        {
            if (value && (!InUse || Main.Lockdown || GorillaTagger.Instance == null)) return;
            if (value == open) return;
            open = value;
            if (value)
            {
                Build();
                placed = false;
            }
            else
            {
                ReleaseCursor();
            }
        }

        public void Search() => SetOpen(true);
        public void Edit() => SetOpen(true);
        public void Refresh() { }
        public void Recenter() => placed = false;

        void Build()
        {
            if (root != null) return;
            root = new GameObject("PoisonHud");
            root.transform.SetParent(transform, false);
            overlayMaterial = BuildOverlayMaterial();
            BuildWatermark();
            BuildLaser();
            BuildWindows();
            themeStamp = CurrentTheme();
            disconnectShown = DisconnectEnabled();
            ApplyOverlay();
            SetAllActive(false);
        }

        void Rebuild()
        {
            if (root == null) return;
            for (int i = 0; i < windows.Count; i++) if (windows[i].root != null) Destroy(windows[i].root);
            windows.Clear();
            items.Clear();
            titles.Clear();
            tweens.Clear();
            if (watermark != null) Destroy(watermark);
            BuildWatermark();
            BuildWindows();
            themeStamp = CurrentTheme();
            disconnectShown = DisconnectEnabled();
            ApplyOverlay();
            SetAllActive(visible && open);
        }

        void SetAllActive(bool on)
        {
            if (watermark != null) watermark.SetActive(on);
            if (laser != null) laser.enabled = false;
            if (pointer != null) pointer.SetActive(false);
            if (!on)
                for (int i = 0; i < windows.Count; i++) if (windows[i].root != null) windows[i].root.SetActive(false);
        }

        Material BuildOverlayMaterial()
        {
            Shader shader = Shader.Find("UI/Overlay")
                ?? Shader.Find("GUI/Text Shader")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default");
            if (shader == null) return null;
            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return material;
        }

        void ApplyOverlay()
        {
            if (root == null || overlayMaterial == null) return;
            SetOverlay(root.transform);
        }

        void SetOverlay(Transform target)
        {
            TMP_Text text = target.GetComponent<TMP_Text>();
            if (text == null)
            {
                Graphic graphic = target.GetComponent<Graphic>();
                if (graphic != null) graphic.material = overlayMaterial;
            }
            for (int i = 0; i < target.childCount; i++) SetOverlay(target.GetChild(i));
        }

        static bool CanSee(string name) =>
            name != "Internal Mods" && (Main.isAdmin || (!name.Contains("Admin") && name != "Mod Givers"));

        struct Leaf
        {
            public string title;
            public ButtonInfo[] group;
        }

        List<Leaf> Collect()
        {
            List<Leaf> list = new List<Leaf>();
            string[] names = Buttons.categoryNames;
            ButtonInfo[][] groups = Buttons.buttons;
            if (names == null || groups == null) return list;
            int count = Math.Min(names.Length, groups.Length);
            for (int i = 0; i < count; i++)
            {
                string name = names[i];
                if (string.IsNullOrEmpty(name) || name == "Main" || !CanSee(name)) continue;
                ButtonInfo[] group = groups[i];
                if (group == null || group.Length == 0) continue;
                list.Add(new Leaf { title = name, group = group });
            }
            return list;
        }

        void BuildWindows()
        {
            List<Leaf> leaves = Collect();
            if (leaves.Count == 0) return;

            bool anyOpen = false;
            for (int i = 0; i < leaves.Count; i++) if (GetState(leaves[i].title).openWindow) anyOpen = true;
            if (!anyOpen) GetState(leaves[0].title).openWindow = true;
            List<State> open = OpenList(leaves);
            for (int i = 0; i < open.Count; i++)
            {
                if (open[i].placed) continue;
                SlotFor(i, out float yaw, out float pitch);
                open[i].yaw = yaw;
                open[i].pitch = pitch;
                open[i].placed = true;
            }

            sidebar = BuildSidebar(leaves);
            sidebar.delay = 0f;
            sidebar.yaw = sidebar.state.yaw;
            sidebar.pitch = sidebar.state.pitch;
            windows.Add(sidebar);

            for (int i = 0; i < leaves.Count; i++)
            {
                Leaf leaf = leaves[i];
                State state = GetState(leaf.title);
                Window window = BuildWindow(leaf.title, leaf.group, state);
                window.delay = i * 0.03f;
                window.yaw = state.yaw;
                window.pitch = state.pitch;
                windows.Add(window);
            }

            keyboardWindow = BuildKeyboard();
            keyboardWindow.yaw = keyboardWindow.state.yaw;
            keyboardWindow.pitch = keyboardWindow.state.pitch;
            windows.Add(keyboardWindow);

            State resultsState = GetState(ResultsKey);
            if (!resultsState.placed)
            {
                resultsState.yaw = 10f * Mathf.Deg2Rad;
                resultsState.pitch = 0f;
                resultsState.placed = true;
            }
            resultsWindow = BuildWindow("Search", SearchResults().ToArray(), resultsState);
            resultsWindow.yaw = resultsState.yaw;
            resultsWindow.pitch = resultsState.pitch;
            windows.Add(resultsWindow);
        }

        List<State> OpenList(List<Leaf> leaves)
        {
            List<State> open = new List<State>();
            for (int i = 0; i < leaves.Count; i++)
            {
                State state = GetState(leaves[i].title);
                if (state.openWindow) open.Add(state);
            }
            return open;
        }

        static void SlotFor(int index, out float yaw, out float pitch)
        {
            const int perRow = 3;
            const float yawStep = 32f;
            const float pitchStep = 28f;
            const float baseYaw = 42f;
            int row = index / perRow;
            int col = index % perRow;
            float start = baseYaw - (perRow - 1) * 0.5f * yawStep;
            yaw = (start + col * yawStep) * Mathf.Deg2Rad;
            pitch = (-row * pitchStep) * Mathf.Deg2Rad;
        }

        void AssignSlot(string name)
        {
            List<Leaf> leaves = Collect();
            List<State> open = OpenList(leaves);
            for (int i = 0; i < open.Count; i++)
            {
                if (open[i] != GetState(name)) continue;
                SlotFor(i, out float yaw, out float pitch);
                open[i].yaw = yaw;
                open[i].pitch = pitch;
                open[i].placed = true;
                break;
            }
        }

        void AutoSort()
        {
            List<State> open = OpenList(Collect());
            for (int i = 0; i < open.Count; i++)
            {
                SlotFor(i, out float yaw, out float pitch);
                open[i].yaw = yaw;
                open[i].pitch = pitch;
                open[i].placed = true;
            }
        }

        void ToggleCategory(string name)
        {
            State state = GetState(name);
            state.openWindow = !state.openWindow;
            if (state.openWindow && !state.placed) AssignSlot(name);
            RebuildSidebar();
        }

        void RebuildSidebar()
        {
            if (sidebar == null) return;
            List<Leaf> leaves = Collect();
            Transform old = sidebar.root != null ? sidebar.root.transform : null;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                RectTransform rect = items[i].rect;
                if (rect == null || (old != null && rect.IsChildOf(old))) items.RemoveAt(i);
            }
            for (int i = titles.Count - 1; i >= 0; i--) if (titles[i].window == sidebar) titles.RemoveAt(i);
            if (sidebar.root != null) Destroy(sidebar.root);
            int index = windows.IndexOf(sidebar);
            Window next = BuildSidebar(leaves);
            next.show = sidebar.show;
            next.showTarget = sidebar.showTarget;
            next.yaw = next.state.yaw;
            next.pitch = next.state.pitch;
            if (index >= 0) windows[index] = next;
            else windows.Insert(0, next);
            sidebar = next;
            ApplyOverlay();
        }

        struct Theme
        {
            public Color background;
            public Color panel;
            public Color border;
            public Color accent;
            public Color bright;
            public Color muted;
            public float opacity;
            public float accentAmount;
            public float rounding;
            public float rowHeight;
        }

        Theme CurrentTheme()
        {
            UI ui = UI.Instance;
            Theme theme = new Theme
            {
                background = ui != null ? ui.BackgroundColor : new Color(0.055f, 0.066f, 0.09f),
                panel = ui != null ? ui.PanelColor : new Color(0.086f, 0.1f, 0.14f),
                border = ui != null ? ui.BorderColor : (Color)new Color32(43, 47, 60, 255),
                accent = ui != null ? ui.AccentColor : (Color)new Color32(158, 139, 255, 255),
                bright = ui != null ? ui.BrightColor : (Color)new Color32(235, 237, 245, 255),
                muted = ui != null ? ui.MutedColor : (Color)new Color32(144, 151, 169, 255),
                opacity = ui != null ? ui.UiOpacity : 1f,
                accentAmount = ui != null ? ui.UiAccentAmount : 1f,
                rounding = ui != null ? ui.UiRounding : 1f,
                rowHeight = ui != null ? ui.UiRowHeight : 24f,
            };
            theme.background.a = theme.opacity;
            theme.panel.a = theme.opacity;
            theme.border.a = theme.opacity;
            return theme;
        }

        static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        static float ThemeDiff(Theme a, Theme b) =>
            Mathf.Abs(a.background.r - b.background.r) + Mathf.Abs(a.background.g - b.background.g) + Mathf.Abs(a.background.b - b.background.b) +
            Mathf.Abs(a.panel.r - b.panel.r) + Mathf.Abs(a.panel.g - b.panel.g) + Mathf.Abs(a.panel.b - b.panel.b) +
            Mathf.Abs(a.border.r - b.border.r) + Mathf.Abs(a.border.g - b.border.g) + Mathf.Abs(a.border.b - b.border.b) +
            Mathf.Abs(a.accent.r - b.accent.r) + Mathf.Abs(a.accent.g - b.accent.g) + Mathf.Abs(a.accent.b - b.accent.b) +
            Mathf.Abs(a.bright.r - b.bright.r) + Mathf.Abs(a.bright.g - b.bright.g) + Mathf.Abs(a.bright.b - b.bright.b);

        int Radius(float baseRadius) => Mathf.Max(1, Mathf.RoundToInt(baseRadius * (UI.Instance != null ? UI.Instance.UiRounding : 1f)));

        static TMP_FontAsset Font()
        {
            if (font != null || probed) return font;
            probed = true;
            try
            {
                GameObject go = GameObject.Find(FontPath);
                if (go != null)
                {
                    TMP_Text text = go.GetComponent<TMP_Text>();
                    if (text != null && text.font != null) font = text.font;
                }
            }
            catch { }
            return font;
        }

        static Sprite Rounded(int radius)
        {
            radius = Mathf.Max(1, radius);
            if (sprites.TryGetValue(radius, out Sprite cached)) return cached;
            int size = radius * 2 + 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    int cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprites[radius] = sprite;
            return sprite;
        }

        static GameObject NewObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        Image Stretch(RectTransform parent, float inset, Color color, int radius)
        {
            GameObject go = NewObject("f", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = Rounded(radius);
            image.type = Image.Type.Sliced;
            return image;
        }

        Image Box(RectTransform parent, Vector2 position, Vector2 size, Color color, int radius)
        {
            GameObject go = NewObject("i", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = Rounded(radius);
            image.type = Image.Type.Sliced;
            return image;
        }

        Image Edge(RectTransform parent, float fromTop, float width, float height, Color color, int radius)
        {
            GameObject go = NewObject("edge", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f); rect.anchorMax = new Vector2(0.5f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -fromTop);
            rect.sizeDelta = new Vector2(width, height);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = Rounded(radius);
            image.type = Image.Type.Sliced;
            return image;
        }

        TMP_Text Label(RectTransform parent, string value, float size, TextAlignmentOptions align, Color color)
        {
            GameObject go = NewObject("t", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            TMP_FontAsset asset = Font();
            if (asset != null) text.font = asset;
            texts.Add(text);
            return text;
        }

        GameObject watermark;
        CanvasGroup watermarkGroup;
        TMP_Text watermarkTitle, watermarkSub, watermarkStat;

        void BuildWatermark()
        {
            Theme theme = CurrentTheme();
            watermark = new GameObject("Watermark", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            watermark.transform.SetParent(root.transform, false);
            Canvas canvas = watermark.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;
            watermarkGroup = watermark.GetComponent<CanvasGroup>();
            watermarkGroup.alpha = 0f;
            RectTransform rect = watermark.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(640f, 80f);
            watermark.transform.localScale = Vector3.one * WindowScale;

            watermarkTitle = WatermarkText(rect, "Poison", new Vector2(-250f, 6f), new Vector2(440f, 34f), 30, TextAlignmentOptions.Left, theme.bright);
            watermarkSub = WatermarkText(rect, "v" + PluginInfo.Version, new Vector2(-250f, -16f), new Vector2(440f, 14f), 11, TextAlignmentOptions.Left, theme.muted);
            watermarkStat = WatermarkText(rect, "", new Vector2(230f, 0f), new Vector2(220f, 34f), 16, TextAlignmentOptions.Right, theme.muted);
        }

        static TMP_Text WatermarkText(RectTransform parent, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions align, Color color)
        {
            GameObject go = NewObject("t", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = align == TextAlignmentOptions.Right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            TMP_FontAsset asset = Font();
            if (asset != null) text.font = asset;
            texts.Add(text);
            return text;
        }

        Window BuildShell(string header, bool sidebar, State state, int count, float rowHeight, Theme theme, out RectTransform content)
        {
            int rows = sidebar ? Mathf.Min(count, MaxRows) : Mathf.Min(count, MaxRows);
            float step = rowHeight + RowGap;
            float bodyHeight = rows > 0 ? rows * rowHeight + (rows - 1) * RowGap : 0f;
            float contentHeight = count > 0 ? count * rowHeight + (count - 1) * RowGap : 0f;
            float expanded = TitleHeight + 4f + bodyHeight + 6f;

            GameObject root = new GameObject("win", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(this.root.transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sidebar ? 12 : 10;
            CanvasGroup fade = root.GetComponent<CanvasGroup>();
            fade.alpha = 0f;
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(WindowWidth, expanded);
            root.transform.localScale = Vector3.one * WindowScale;

            Stretch(rect, 0f, Alpha(theme.border, theme.opacity), Radius(6f));
            Stretch(rect, 1f, Alpha(theme.background, theme.opacity), Radius(5f));

            Window window = new Window
            {
                title = header,
                state = state,
                root = root,
                rect = rect,
                fade = fade,
                sidebar = sidebar,
                expanded = expanded,
                bodyFull = bodyHeight,
                contentFull = contentHeight,
                rowStep = step,
                maxScroll = Mathf.Max(0f, contentHeight - bodyHeight),
                scroll = state.scroll,
                collapseTarget = state.collapsed ? 1f : 0f,
                collapse = state.collapsed ? 1f : 0f,
            };

            GameObject titleObject = NewObject("title", rect);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = new Vector2(1, 1); titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -1);
            titleRect.sizeDelta = new Vector2(-2f, TitleHeight);
            Image titleImage = titleObject.AddComponent<Image>();
            titleImage.sprite = Rounded(Radius(5f));
            titleImage.type = Image.Type.Sliced;
            titleImage.color = Alpha(theme.panel, theme.opacity);

            TMP_Text label = Label(titleRect, header, 13, TextAlignmentOptions.Left, theme.bright);
            label.rectTransform.offsetMin = new Vector2(12f, 0);
            label.rectTransform.offsetMax = new Vector2(sidebar ? -60f : -40f, 0);

            Edge(rect, TitleHeight, WindowWidth - 20f, 1f, Alpha(theme.border, theme.opacity * 0.7f), 0);

            GameObject bodyObject = NewObject("body", rect);
            RectTransform body = bodyObject.GetComponent<RectTransform>();
            body.anchorMin = new Vector2(0.5f, 1f); body.anchorMax = new Vector2(0.5f, 1f); body.pivot = new Vector2(0.5f, 1f);
            body.anchoredPosition = new Vector2(0, -(TitleHeight + 4f));
            body.sizeDelta = new Vector2(WindowWidth - 10f, bodyHeight);
            bodyObject.AddComponent<RectMask2D>();

            GameObject contentObject = NewObject("content", body);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 1f); contentRect.anchorMax = new Vector2(0.5f, 1f); contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = new Vector2(0, window.scroll);
            contentRect.sizeDelta = new Vector2(WindowWidth - 10f, contentHeight);

            if (window.maxScroll > 0f)
            {
                Image track = Box(body, new Vector2((WindowWidth - 10f) * 0.5f - 3f, 0f), new Vector2(3f, bodyHeight), Alpha(theme.border, theme.opacity * 0.5f), 2);
                float thumbHeight = Mathf.Max(18f, bodyHeight * (bodyHeight / contentHeight));
                window.track = track.rectTransform;
                window.thumb = Box(track.rectTransform, Vector2.zero, new Vector2(3f, thumbHeight), Alpha(theme.accent, 0.6f), 2);
            }

            window.titleRect = titleRect;
            window.body = body;
            window.content = contentRect;
            titles.Add(new Title { rect = titleRect, window = window });
            content = contentRect;
            return window;
        }

        Window BuildWindow(string title, ButtonInfo[] group, State state)
        {
            Theme theme = CurrentTheme();
            Window window = BuildShell(title, false, state, group != null ? group.Length : 0, theme.rowHeight, theme, out RectTransform content);
            float rowHeight = theme.rowHeight;

            float buttonSize = TitleHeight - 8f;
            float x = WindowWidth * 0.5f - 8f - buttonSize * 0.5f;
            TMP_Text icon = null;
            Image collapse = TitleSquare(window, x, buttonSize, theme, () =>
            {
                state.collapsed = !state.collapsed;
                window.collapseTarget = state.collapsed ? 1f : 0f;
                if (icon != null) icon.text = state.collapsed ? "+" : "-";
            });
            icon = Label(collapse.rectTransform, state.collapsed ? "+" : "-", 14, TextAlignmentOptions.Center, theme.bright);

            if (group != null)
            {
                for (int i = 0; i < group.Length; i++)
                    BuildRow(theme, group[i], content, -i * (rowHeight + RowGap), rowHeight, window.body);
            }
            return window;
        }

        Image TitleSquare(Window window, float x, float size, Theme theme, Action act)
        {
            RectTransform parent = window.titleRect;
            GameObject go = NewObject("tb", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0);
            rect.sizeDelta = new Vector2(size, size);
            Image image = go.AddComponent<Image>();
            image.sprite = Rounded(Radius(4f));
            image.type = Image.Type.Sliced;
            image.color = new Color(0, 0, 0, 0);
            items.Add(new Item
            {
                rect = rect,
                clip = window.titleRect,
                fill = image,
                baseColor = image.color,
                hoverColor = Alpha(theme.accent, 0.18f),
                act = act
            });
            return image;
        }

        Window BuildSidebar(List<Leaf> leaves)
        {
            Theme theme = CurrentTheme();
            Window window = BuildShell("Poison", true, GetState(SidebarKey), leaves.Count, theme.rowHeight, theme, out RectTransform content);
            window.state.yaw = SideYaw;
            window.state.pitch = SidePitch;
            window.state.placed = true;

            float buttonSize = TitleHeight - 8f;
            float x = WindowWidth * 0.5f - 8f - buttonSize * 0.5f;
            Image auto = TitleSquare(window, x, buttonSize, theme, AutoSort);
            Label(auto.rectTransform, "A", 13, TextAlignmentOptions.Center, theme.muted);

            float fieldTop = TitleHeight + 4f;
            float fieldHeight = 22f;
            Image fieldBorder = Edge(window.rect, fieldTop, WindowWidth - 20f, fieldHeight, Alpha(theme.border, theme.opacity), Radius(4f));
            Image fieldFill = Edge(window.rect, fieldTop + 1f, WindowWidth - 22f, fieldHeight - 2f, Alpha(theme.background, theme.opacity), Radius(3f));
            searchLabel = Label(fieldFill.rectTransform, "", 11, TextAlignmentOptions.Left, theme.muted);
            searchLabel.margin = new Vector4(8f, 0, 8f, 0);
            items.Add(new Item
            {
                rect = fieldFill.rectTransform,
                fill = fieldFill,
                baseColor = fieldFill.color,
                hoverColor = Alpha(theme.accent, 0.12f),
                act = () => { keyboardOpen = !keyboardOpen; }
            });

            window.body.anchoredPosition = new Vector2(0, -(fieldTop + fieldHeight + 4f));
            window.expanded += fieldHeight + 4f;
            window.rect.sizeDelta = new Vector2(WindowWidth, window.expanded);

            float rowHeight = theme.rowHeight;
            for (int i = 0; i < leaves.Count; i++)
                BuildSidebarRow(theme, leaves[i].title, content, -i * (rowHeight + RowGap), rowHeight, window.body);

            if (DisconnectEnabled())
            {
                float bottom = fieldTop + fieldHeight + 4f + window.bodyFull + 6f;
                float discHeight = 24f;
                Edge(window.rect, bottom, WindowWidth - 20f, discHeight, Alpha(theme.border, theme.opacity), Radius(4f));
                Image discFill = Edge(window.rect, bottom + 1f, WindowWidth - 22f, discHeight - 2f, Alpha(theme.panel, theme.opacity), Radius(3f));
                Label(discFill.rectTransform, "Disconnect", 12, TextAlignmentOptions.Center, theme.bright);
                items.Add(new Item
                {
                    rect = discFill.rectTransform,
                    fill = discFill,
                    baseColor = discFill.color,
                    hoverColor = Alpha(theme.accent, 0.25f),
                    act = Disconnect
                });
                window.expanded = bottom + discHeight + 6f;
                window.rect.sizeDelta = new Vector2(WindowWidth, window.expanded);
            }
            return window;
        }

        void BuildSidebarRow(Theme theme, string name, RectTransform content, float y, float rowHeight, RectTransform clip)
        {
            GameObject rowObject = NewObject("row", content);
            RectTransform rect = rowObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f); rect.anchorMax = new Vector2(0.5f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(WindowWidth - 10f, rowHeight);
            Image fill = rowObject.AddComponent<Image>();
            fill.sprite = Rounded(Radius(3f));
            fill.type = Image.Type.Sliced;

            bool on = GetState(name).openWindow;
            Color baseColor = on ? Alpha(theme.accent, 0.22f * theme.accentAmount) : new Color(0, 0, 0, 0);
            fill.color = baseColor;

            TMP_Text label = Label(rect, name, 11, TextAlignmentOptions.Left, on ? theme.accent : theme.bright);
            label.margin = new Vector4(10f, 0, 10f, 0);

            items.Add(new Item
            {
                rect = rect,
                clip = clip,
                fill = fill,
                label = label,
                baseColor = baseColor,
                hoverColor = on ? Alpha(theme.accent, 0.22f * theme.accentAmount + 0.08f) : Alpha(theme.accent, 0.08f),
                act = () => ToggleCategory(name)
            });
        }

        void BuildRow(Theme theme, ButtonInfo button, RectTransform content, float y, float rowHeight, RectTransform clip)
        {
            GameObject rowObject = NewObject("row", content);
            RectTransform rect = rowObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f); rect.anchorMax = new Vector2(0.5f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(WindowWidth - 10f, rowHeight);
            Image fill = rowObject.AddComponent<Image>();
            fill.color = new Color(0, 0, 0, 0);
            fill.sprite = Rounded(Radius(3f));
            fill.type = Image.Type.Sliced;

            bool blocked = button.detected && !Main.allowDetected;
            bool isLabel = button.label;
            bool isStep = button.incremental && !isLabel;
            bool isToggle = button.isTogglable && !isStep && !isLabel;

            if (isLabel)
            {
                Label(rect, button.buttonText, 11, TextAlignmentOptions.Left, theme.muted).margin = new Vector4(10f, 0, 10f, 0);
                return;
            }

            if (isToggle)
            {
                Color onColor = Alpha(theme.accent, 0.22f * theme.accentAmount);
                Color hoverOn = Alpha(theme.accent, 0.22f * theme.accentAmount + 0.08f);
                fill.color = button.enabled ? onColor : new Color(0, 0, 0, 0);

                TMP_Text label = Label(rect, Text(button), 11, TextAlignmentOptions.Left,
                    blocked ? theme.muted : button.enabled ? theme.accent : theme.bright);
                label.margin = new Vector4(10f, 0, 10f, 0);

                if (blocked) return;
                items.Add(new Item
                {
                    rect = rect,
                    clip = clip,
                    fill = fill,
                    label = label,
                    baseColor = fill.color,
                    hoverColor = button.enabled ? hoverOn : Alpha(theme.accent, 0.08f),
                    act = () =>
                    {
                        Main.Toggle(button, true, true);
                        bool on = button.enabled;
                        label.color = on ? theme.accent : theme.bright;
                        fill.color = on ? onColor : new Color(0, 0, 0, 0);
                        for (int i = 0; i < items.Count; i++)
                        {
                            Item item = items[i];
                            if (item.rect != rect) continue;
                            item.baseColor = fill.color;
                            item.hoverColor = on ? hoverOn : Alpha(theme.accent, 0.08f);
                            items[i] = item;
                            break;
                        }
                    }
                });
                return;
            }

            if (isStep)
            {
                TMP_Text label = Label(rect, Text(button), 11, TextAlignmentOptions.Left, blocked ? theme.muted : theme.bright);
                label.margin = new Vector4(10f, 0, 74f, 0);

                float width = 64f;
                Image back = Box(rect, new Vector2(rect.sizeDelta.x * 0.5f - width * 0.5f - 2f, 0), new Vector2(width, rowHeight - 4f), Alpha(theme.border, theme.opacity), Radius(4f));
                Image inner = Box(back.rectTransform, Vector2.zero, new Vector2(width - 2f, rowHeight - 6f), Alpha(theme.panel, theme.opacity), Radius(3f));

                Color ink = blocked ? theme.muted : theme.accent;
                float half = width * 0.5f;

                RectTransform left = NewObject("l", back.rectTransform).GetComponent<RectTransform>();
                left.anchorMin = left.anchorMax = left.pivot = new Vector2(0.5f, 0.5f);
                left.anchoredPosition = new Vector2(-half * 0.5f, 0);
                left.sizeDelta = new Vector2(half, rowHeight - 6f);
                Label(left, "<", 13, TextAlignmentOptions.Center, ink);

                RectTransform right = NewObject("r", back.rectTransform).GetComponent<RectTransform>();
                right.anchorMin = right.anchorMax = right.pivot = new Vector2(0.5f, 0.5f);
                right.anchoredPosition = new Vector2(half * 0.5f, 0);
                right.sizeDelta = new Vector2(half, rowHeight - 6f);
                Label(right, ">", 13, TextAlignmentOptions.Center, ink);

                if (blocked) return;
                items.Add(new Item
                {
                    rect = rect,
                    clip = clip,
                    fill = fill,
                    baseColor = new Color(0, 0, 0, 0),
                    hoverColor = Alpha(theme.accent, 0.08f),
                    act = () => { }
                });
                items.Add(new Item
                {
                    rect = left,
                    clip = clip,
                    fill = inner,
                    baseColor = inner.color,
                    hoverColor = Alpha(theme.accent, 0.18f),
                    act = () => Main.ToggleIncremental(button.buttonText, false, true, true)
                });
                items.Add(new Item
                {
                    rect = right,
                    clip = clip,
                    fill = inner,
                    baseColor = inner.color,
                    hoverColor = Alpha(theme.accent, 0.18f),
                    act = () => Main.ToggleIncremental(button.buttonText, true, true, true)
                });
                return;
            }

            TMP_Text buttonLabel = Label(rect, Text(button), 11, TextAlignmentOptions.Left, blocked ? theme.muted : theme.bright);
            buttonLabel.margin = new Vector4(10f, 0, 18f, 0);
            TMP_Text arrow = Label(rect, ">", 13, TextAlignmentOptions.Right, blocked ? theme.muted : theme.accent);
            arrow.margin = new Vector4(6f, 0, 10f, 0);

            if (blocked) return;
            items.Add(new Item
            {
                rect = rect,
                clip = clip,
                fill = fill,
                label = buttonLabel,
                baseColor = new Color(0, 0, 0, 0),
                hoverColor = Alpha(theme.accent, 0.08f),
                act = () => Main.Toggle(button, true, true)
            });
        }

        static string Text(ButtonInfo button) =>
            string.IsNullOrEmpty(button.overlapText) ? button.buttonText : button.overlapText;

        static string Match(string value, string query) =>
            value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? value : null;

        bool Matches(ButtonInfo button, string category, string query)
        {
            if (Match(Text(button), query) != null) return true;
            if (Match(button.buttonText, query) != null) return true;
            if (Match(category, query) != null) return true;
            if (button.aliases != null)
                for (int i = 0; i < button.aliases.Length; i++)
                    if (Match(button.aliases[i], query) != null) return true;
            return false;
        }

        List<ButtonInfo> SearchResults()
        {
            List<ButtonInfo> list = new List<ButtonInfo>();
            string query = search.Trim();
            if (query.Length == 0) return list;
            string[] names = Buttons.categoryNames;
            ButtonInfo[][] groups = Buttons.buttons;
            if (names == null || groups == null) return list;
            HashSet<ButtonInfo> seen = new HashSet<ButtonInfo>();
            int count = Math.Min(names.Length, groups.Length);
            for (int i = 0; i < count; i++)
            {
                if (!CanSee(names[i]) || groups[i] == null) continue;
                for (int j = 0; j < groups[i].Length; j++)
                {
                    ButtonInfo button = groups[i][j];
                    if (button == null || button.label || !seen.Add(button)) continue;
                    if (Matches(button, names[i], query)) list.Add(button);
                }
            }
            return list;
        }

        void Append(char character)
        {
            if (search.Length < 32) search += character;
            RefreshResults();
        }

        void Backspace()
        {
            if (search.Length > 0) search = search.Substring(0, search.Length - 1);
            RefreshResults();
        }

        void ClearSearch()
        {
            search = "";
            RefreshResults();
        }

        void RefreshResults()
        {
            if (resultsWindow != null)
            {
                Transform old = resultsWindow.root != null ? resultsWindow.root.transform : null;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    RectTransform rect = items[i].rect;
                    if (rect == null || (old != null && rect.IsChildOf(old))) items.RemoveAt(i);
                }
                for (int i = titles.Count - 1; i >= 0; i--) if (titles[i].window == resultsWindow) titles.RemoveAt(i);
                if (resultsWindow.root != null) Destroy(resultsWindow.root);
                windows.Remove(resultsWindow);
            }
            State state = GetState(ResultsKey);
            if (state.yaw == 0f && state.pitch == 0f)
            {
                state.yaw = 10f * Mathf.Deg2Rad;
                state.pitch = 0f;
                state.placed = true;
            }
            List<ButtonInfo> found = SearchResults();
            resultsWindow = BuildWindow("Search", found.ToArray(), state);
            resultsWindow.show = 1f;
            resultsWindow.showTarget = 1f;
            resultsWindow.yaw = state.yaw;
            resultsWindow.pitch = state.pitch;
            windows.Add(resultsWindow);
            ApplyOverlay();
        }

        Window BuildKeyboard()
        {
            Theme theme = CurrentTheme();
            State state = GetState(KeyboardKey);
            if (!state.placed)
            {
                state.yaw = 10f * Mathf.Deg2Rad;
                state.pitch = -34f * Mathf.Deg2Rad;
                state.placed = true;
            }
            Window window = BuildShell("Keyboard", false, state, 0, theme.rowHeight, theme, out RectTransform content);
            window.yaw = state.yaw;
            window.pitch = state.pitch;

            const float keyWidth = 22f;
            const float keyHeight = 22f;
            const float gap = 2f;
            string[] rows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    char key = rows[r][c];
                    AddKey(window, content, theme, key.ToString(), c * (keyWidth + gap), r * (keyHeight + gap), keyWidth, keyHeight, () => Append(key));
                }

            float specialY = rows.Length * (keyHeight + gap);
            AddKey(window, content, theme, "Space", 0f, specialY, 142f, keyHeight, () => Append(' '));
            AddKey(window, content, theme, "Del", 146f, specialY, 44f, keyHeight, Backspace);
            AddKey(window, content, theme, "Done", 192f, specialY, 46f, keyHeight, () => { keyboardOpen = false; });

            float height = specialY + keyHeight;
            window.body.sizeDelta = new Vector2(WindowWidth - 10f, height);
            window.bodyFull = height;
            window.contentFull = height;
            window.expanded = TitleHeight + 4f + height + 6f;
            window.rect.sizeDelta = new Vector2(WindowWidth, window.expanded);
            return window;
        }

        void AddKey(Window window, RectTransform content, Theme theme, string label, float x, float y, float width, float height, Action act)
        {
            GameObject go = NewObject("key", content);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            Image fill = go.AddComponent<Image>();
            fill.sprite = Rounded(Radius(3f));
            fill.type = Image.Type.Sliced;
            fill.color = Alpha(theme.panel, theme.opacity);
            Label(rect, label, 11, TextAlignmentOptions.Center, theme.bright);
            items.Add(new Item
            {
                rect = rect,
                clip = window.body,
                fill = fill,
                baseColor = fill.color,
                hoverColor = Alpha(theme.accent, 0.25f),
                act = act
            });
        }

        static bool DisconnectEnabled() => !Main.disableDisconnectButton;

        void Disconnect()
        {
            ButtonInfo button = Buttons.GetIndex("Disconnect");
            if (button == null) return;
            if (button.incremental) Main.ToggleIncremental(button.buttonText, true, true, true);
            else Main.Toggle(button, true, true);
        }

        void BuildLaser()
        {
            laser = NewLaser("PoisonLaser");
            pointer = NewPointer("PoisonPointer", out pointerOuter, out pointerInner, out pointerPulse);
            pointer.SetActive(false);
        }

        LineRenderer NewLaser(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.0065f;
            line.endWidth = 0.0012f;
            line.numCapVertices = 12;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Standard");
            line.material = new Material(shader);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        GameObject NewPointer(string name, out Image outer, out Image inner, out Image pulse)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(root.transform, false);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(32f, 32f);
            go.transform.localScale = Vector3.one * 0.0007f;
            outer = PointerPart(rect, "ring", new Vector2(18f, 18f), 9, Color.white);
            inner = PointerPart(rect, "dot", new Vector2(7f, 7f), 4, Color.white);
            pulse = PointerPart(rect, "pulse", new Vector2(18f, 18f), 9, new Color(1f, 1f, 1f, 0f));
            return go;
        }

        static Image PointerPart(RectTransform parent, string name, Vector2 size, int radius, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            Image image = go.AddComponent<Image>();
            image.sprite = Rounded(radius);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Vector3 Direction(float yaw, float pitch)
        {
            float cp = Mathf.Cos(pitch);
            return new Vector3(Mathf.Sin(yaw) * cp, Mathf.Sin(pitch), Mathf.Cos(yaw) * cp);
        }

        void PlaceWindow(Window window, float yaw, float pitch, float alpha)
        {
            Vector3 local = Direction(yaw, pitch);
            Vector3 world = orbRotation * local;
            window.root.transform.position = orbCenter + world * OrbRadius;
            window.root.transform.rotation = Quaternion.LookRotation(world, Vector3.up);
            window.fade.alpha = alpha;
        }

        void PlaceWatermark()
        {
            Vector3 world = orbRotation * new Vector3(0, Mathf.Sin(20f * Mathf.Deg2Rad), Mathf.Cos(20f * Mathf.Deg2Rad));
            watermark.transform.position = orbCenter + world * OrbRadius;
            watermark.transform.rotation = Quaternion.LookRotation(world, Vector3.up);
        }

        static Camera HeadCamera()
        {
            Camera camera = Camera.main;
            if (camera != null) return camera;
            try
            {
                GameObject go = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/MainCamera");
                if (go != null) return go.GetComponent<Camera>();
            }
            catch { }
            return null;
        }

        void PlaceOrb()
        {
            Camera camera = HeadCamera();
            if (camera == null) return;
            Vector3 forward = camera.transform.forward; forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = camera.transform.forward;
            forward.Normalize();
            orbCenter = camera.transform.position + Vector3.up * 0.02f;
            orbRotation = Quaternion.LookRotation(forward, Vector3.up);
            placed = true;
            SetAllActive(open);
        }

        struct Hit
        {
            public Window window;
            public Vector3 point;
            public float distance;
        }

        static bool HitsWindow(Window window, Vector3 origin, Vector3 direction, out Vector3 point, out float distance)
        {
            point = Vector3.zero; distance = float.MaxValue;
            if (window.root == null) return false;
            Vector3 normal = -window.root.transform.forward;
            Vector3 plane = window.root.transform.position;
            float denominator = Vector3.Dot(direction, normal);
            if (Mathf.Abs(denominator) < 1e-5f) return false;
            float t = Vector3.Dot(plane - origin, normal) / denominator;
            if (t < 0f || t > 50f) return false;
            point = origin + direction * t;
            distance = t;
            Vector3 local = window.rect.InverseTransformPoint(point);
            Rect area = window.rect.rect;
            if (local.x < area.xMin - 8f || local.x > area.xMax + 8f) return false;
            if (local.y < area.yMin - 8f || local.y > area.yMax + 8f) return false;
            return true;
        }

        bool RayHit(Vector3 origin, Vector3 direction, out Hit hit)
        {
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < windows.Count; i++)
            {
                Window window = windows[i];
                if (window.root == null || !window.root.activeInHierarchy) continue;
                if (HitsWindow(window, origin, direction, out Vector3 point, out float distance) && distance < best)
                {
                    hit.window = window; hit.point = point; hit.distance = distance;
                    best = distance; found = true;
                }
            }
            return found;
        }

        static bool Inside(RectTransform rect, Vector3 point)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            Vector3 local = rect.InverseTransformPoint(point);
            Rect area = rect.rect;
            return local.x > area.xMin && local.x < area.xMax && local.y > area.yMin && local.y < area.yMax;
        }

        int ItemAt(Vector3 point, Window window)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].rect == null) continue;
                if (!items[i].rect.IsChildOf(window.root.transform)) continue;
                if (!Inside(items[i].rect, point)) continue;
                if (items[i].clip != null && !Inside(items[i].clip, point)) continue;
                return i;
            }
            return -1;
        }

        Window TitleAt(Vector3 point, Window preferred)
        {
            if (preferred != null && preferred.root != null && preferred.root.activeInHierarchy)
                for (int i = 0; i < titles.Count; i++)
                    if (titles[i].window == preferred && Inside(titles[i].rect, point)) return titles[i].window;
            for (int i = 0; i < titles.Count; i++)
                if (titles[i].window != null && titles[i].window.root != null && titles[i].window.root.activeInHierarchy && Inside(titles[i].rect, point))
                    return titles[i].window;
            return null;
        }

        bool HandRay(bool right, out Vector3 origin, out Vector3 direction)
        {
            origin = default; direction = default;
            Transform hand = right ? GorillaTagger.Instance?.rightHandTransform : GorillaTagger.Instance?.leftHandTransform;
            if (hand == null) return false;
            origin = hand.position;
            direction = (hand.rotation * new Vector3(0f, -0.4f, 1f)).normalized;
            return true;
        }

        bool TriggerPressed()
        {
            ControllerInputPoller poll = ControllerInputPoller.instance;
            if (poll == null) return false;
            float value = RightHand ? poll.rightControllerIndexFloat : poll.leftControllerIndexFloat;
            bool held = value > 0.5f;
            bool previous = triggerWas > 0.5f;
            triggerWas = held ? 1f : 0f;
            return held && !previous;
        }

        bool GripHeld()
        {
            ControllerInputPoller poll = ControllerInputPoller.instance;
            if (poll == null) return false;
            float value = RightHand ? poll.rightControllerGripFloat : poll.leftControllerGripFloat;
            return value > 0.5f;
        }

        bool GripPressed()
        {
            bool held = GripHeld();
            bool previous = gripWas > 0.5f;
            gripWas = held ? 1f : 0f;
            return held && !previous;
        }

        void ScrollBy(Window window, float amount)
        {
            if (window == null || window.maxScroll <= 0f) return;
            window.state.scroll = Mathf.Clamp(window.state.scroll + amount, 0f, window.maxScroll);
        }

        void DragScroll(Window window, Vector3 origin, Vector3 direction)
        {
            if (window == null || window.track == null || window.maxScroll <= 0f) return;
            Vector3 normal = -window.root.transform.forward;
            float denominator = Vector3.Dot(direction, normal);
            if (Mathf.Abs(denominator) < 1e-5f) return;
            float t = Vector3.Dot(window.root.transform.position - origin, normal) / denominator;
            Vector3 point = origin + direction * t;
            float thumbHeight = window.thumb != null ? window.thumb.rectTransform.sizeDelta.y : 0f;
            float travel = Mathf.Max(1f, window.bodyFull - thumbHeight);
            float local = window.track.InverseTransformPoint(point).y;
            float fromTop = window.bodyFull * 0.5f - local;
            float desired = Mathf.Clamp(fromTop - window.grab, 0f, travel);
            window.state.scroll = desired / travel * window.maxScroll;
            window.scroll = window.state.scroll;
        }

        float StickY()
        {
            Vector2 left = Main.leftJoystick;
            Vector2 right = Main.rightJoystick;
            return Mathf.Abs(left.y) >= Mathf.Abs(right.y) ? left.y : right.y;
        }

        void HandleInput()
        {
            if (openAmount < 0.6f || !open) return;
            pcMode = !XRSettings.isDeviceActive;

            int hover = -1;
            Window hitWindow = null;
            Vector3 hitPoint = Vector3.zero;
            bool hit = false;
            bool press = false;
            bool grip = false;
            bool gripNow = false;
            float wheel = 0f;
            Vector3 origin = Vector3.zero, direction = Vector3.forward;
            bool ray = false;

            if (pcMode)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                Camera camera = HeadCamera();
                if (mouse != null && camera != null)
                {
                    Vector2 position = mouse.position.ReadValue();
                    Ray cast = camera.ScreenPointToRay(new Vector3(position.x, position.y, 0f));
                    origin = cast.origin; direction = cast.direction; ray = true;
                    if (RayHit(origin, direction, out Hit result))
                    {
                        hit = true; hitWindow = result.window; hitPoint = result.point;
                        hover = ItemAt(hitPoint, hitWindow);
                    }
                    press = mouse.leftButton.wasPressedThisFrame;
                    grip = mouse.leftButton.wasPressedThisFrame;
                    gripNow = mouse.leftButton.isPressed;
                    wheel = -mouse.scroll.ReadValue().y * 0.4f;
                }
            }
            else if (HandRay(RightHand, out origin, out direction))
            {
                ray = true;
                if (RayHit(origin, direction, out Hit result))
                {
                    hit = true; hitWindow = result.window; hitPoint = result.point;
                    hover = ItemAt(hitPoint, hitWindow);
                }
                if (TriggerPressed() && hit)
                {
                    pulseTime = Time.unscaledTime;
                    if (hover >= 0) press = true;
                }
                grip = GripPressed();
                gripNow = GripHeld();
                float stick = StickY();
                if (Mathf.Abs(stick) > 0.25f) wheel = -stick * Time.unscaledDeltaTime * 260f;
            }

            if (scrollWindow != null)
            {
                if (!gripNow) { scrollWindow.scrollDrag = false; scrollWindow = null; }
                else DragScroll(scrollWindow, origin, direction);
            }

            if (scrollWindow == null && grip && hitWindow != null && hitWindow.maxScroll > 0f && hitWindow.track != null && Inside(hitWindow.track, hitPoint))
            {
                scrollWindow = hitWindow;
                scrollWindow.scrollDrag = true;
                float thumbHeight = scrollWindow.thumb != null ? scrollWindow.thumb.rectTransform.sizeDelta.y : 0f;
                float travel = Mathf.Max(1f, scrollWindow.bodyFull - thumbHeight);
                float local = scrollWindow.track.InverseTransformPoint(hitPoint).y;
                float fromTop = scrollWindow.bodyFull * 0.5f - local;
                float ratio = scrollWindow.scroll / scrollWindow.maxScroll;
                bool onThumb = scrollWindow.thumb != null && Inside(scrollWindow.thumb.rectTransform, hitPoint);
                scrollWindow.grab = onThumb ? fromTop - ratio * travel : thumbHeight * 0.5f;
                DragScroll(scrollWindow, origin, direction);
            }

            if (scrollWindow == null && wheel != 0f && hitWindow != null) ScrollBy(hitWindow, wheel);

            if (LaserOn) DrawLaser(ray, origin, direction, hit, hitPoint, hover >= 0);
            else
            {
                laser.enabled = false;
                pointer.SetActive(false);
            }

            if (scrollWindow != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    Item item = items[i];
                    if (item.rect == null || !item.hovered) continue;
                    item.hovered = false;
                    if (item.fill != null)
                    {
                        Image fill = item.fill;
                        Color to = item.baseColor;
                        Animate(0.10f, t => { if (fill != null) fill.color = Color.Lerp(fill.color, to, t); });
                    }
                    items[i] = item;
                }
                return;
            }

            if (press && hover >= 0) { DoPress(hover); return; }

            if (Draggable && !dragging)
            {
                if (grip) StartDrag(direction, hit, hitPoint, hitWindow);
            }
            if (dragging && dragWindow != null)
            {
                Window window = dragWindow;
                if (!gripNow || window.root == null)
                {
                    dragging = false;
                    window.dragging = false;
                    if (window.root != null && window.state != null) { window.state.yaw = window.yaw; window.state.pitch = window.pitch; window.state.placed = true; }
                    dragWindow = null;
                }
                else
                {
                    Vector3 pointer = PointerDirection(direction);
                    if (pointer.sqrMagnitude > 1e-4f)
                    {
                        Vector3 local = Quaternion.Inverse(orbRotation) * pointer.normalized;
                        float pitch = Mathf.Asin(Mathf.Clamp(local.y, -0.95f, 0.95f));
                        float yaw = Mathf.Atan2(local.x, local.z);
                        float deltaYaw = Mathf.DeltaAngle(window.handYaw * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        float deltaPitch = pitch - window.handPitch;
                        float targetYaw = window.startYaw + deltaYaw;
                        float targetPitch = Mathf.Clamp(window.startPitch + deltaPitch, -50f * Mathf.Deg2Rad, 50f * Mathf.Deg2Rad);
                        window.yaw = Mathf.LerpAngle(window.yaw * Mathf.Rad2Deg, targetYaw * Mathf.Rad2Deg, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f)) * Mathf.Deg2Rad;
                        window.pitch = Mathf.Lerp(window.pitch, targetPitch, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f));
                    }
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item.rect == null) continue;
                bool nowHover = i == hover;
                if (nowHover == item.hovered) continue;
                item.hovered = nowHover;
                if (item.fill != null)
                {
                    Color from = item.fill.color;
                    Color to = nowHover ? item.hoverColor : item.baseColor;
                    Image fill = item.fill;
                    Animate(0.10f, t => { if (fill != null) fill.color = Color.Lerp(from, to, t); });
                }
                items[i] = item;
            }
        }

        Vector3 PointerDirection(Vector3 direction)
        {
            if (pcMode) return direction;
            Transform hand = RightHand ? GorillaTagger.Instance?.rightHandTransform : GorillaTagger.Instance?.leftHandTransform;
            if (hand == null) return Vector3.zero;
            return hand.position - orbCenter;
        }

        void StartDrag(Vector3 direction, bool hit, Vector3 point, Window hitWindow)
        {
            Window target = null;
            if (hit && hitWindow != null) target = TitleAt(point, hitWindow);
            if (target == null) return;
            dragging = true;
            dragWindow = target;
            target.dragging = true;
            Vector3 pointer = PointerDirection(direction);
            if (pointer.sqrMagnitude > 1e-4f)
            {
                Vector3 local = Quaternion.Inverse(orbRotation) * pointer.normalized;
                target.handPitch = Mathf.Asin(Mathf.Clamp(local.y, -0.95f, 0.95f));
                target.handYaw = Mathf.Atan2(local.x, local.z);
                target.startYaw = target.yaw;
                target.startPitch = target.pitch;
            }
        }

        void DoPress(int index)
        {
            if (index < 0 || index >= items.Count) return;
            Item item = items[index];
            if (item.act == null) return;
            RectTransform rect = item.rect;
            Animate(0.14f, t =>
            {
                if (rect == null) return;
                float scale = 1f - 0.05f * Mathf.Sin(t * Mathf.PI);
                rect.localScale = new Vector3(scale, scale, 1f);
            });
            item.act.Invoke();
        }

        void DrawLaser(bool ray, Vector3 origin, Vector3 direction, bool hit, Vector3 point, bool overItem)
        {
            if (!ray || !hit)
            {
                laser.enabled = false;
                pointer.SetActive(false);
                return;
            }

            laser.enabled = true;
            laser.SetPosition(0, origin);
            laser.SetPosition(1, point);

            Theme theme = CurrentTheme();
            Color color = overItem ? theme.accent : theme.bright;
            float amount = Mathf.Clamp01((Time.unscaledTime - pulseTime) / 0.18f);
            float burst = 1f - amount;
            laser.startColor = new Color(color.r, color.g, color.b, 0.95f);
            laser.endColor = new Color(color.r, color.g, color.b, 0.06f);
            laser.startWidth = (overItem ? 0.0068f : 0.0058f) + burst * 0.0014f;
            laser.endWidth = overItem ? 0.0018f : 0.0012f;

            pointer.SetActive(true);
            pointer.transform.position = point + (-direction) * 0.001f;
            pointer.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            pointer.transform.localScale = Vector3.one * (overItem ? 0.00084f : 0.00070f);

            pointerOuter.color = new Color(color.r, color.g, color.b, 0.95f);
            pointerInner.color = new Color(color.r, color.g, color.b, 1f);
            float size = Mathf.Lerp(18f, 30f, amount);
            pointerPulse.rectTransform.sizeDelta = new Vector2(size, size);
            pointerPulse.color = new Color(color.r, color.g, color.b, (1f - amount) * 0.6f);
        }

        void Animate(float duration, Action<float> step) => tweens.Add(new Tween { start = Time.unscaledTime, duration = duration, step = step });

        void StepTweens()
        {
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                Tween tween = tweens[i];
                float now = Time.unscaledTime;
                if (now < tween.start) continue;
                float amount = (now - tween.start) / tween.duration;
                if (amount >= 1f) { tween.step(1f); tweens.RemoveAt(i); }
                else tween.step(Mathf.Clamp01(amount));
            }
        }

        void ApplyFont()
        {
            TMP_FontAsset asset = Font();
            if (asset == null) return;
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                TMP_Text text = texts[i];
                if (text == null) { texts.RemoveAt(i); continue; }
                if (text.font != asset) text.font = asset;
            }
        }

        void UpdateCursor()
        {
            if (!open || XRSettings.isDeviceActive || !Application.isFocused)
            {
                ReleaseCursor();
                return;
            }
            if (!cursorHeld)
            {
                cursorVisible = Cursor.visible;
                cursorLock = Cursor.lockState;
                cursorHeld = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ReleaseCursor()
        {
            if (!cursorHeld) return;
            Cursor.visible = cursorVisible;
            Cursor.lockState = cursorLock;
            cursorHeld = false;
        }

        void Update()
        {
            if (!InUse)
            {
                if (visible) { visible = false; SetAllActive(false); }
                ReleaseCursor();
                return;
            }
            if (root == null) return;

            fpsTime += Time.unscaledDeltaTime;
            fpsCount++;
            if (fpsTime >= 0.5f) { fps = fpsCount / fpsTime; fpsTime = 0f; fpsCount = 0; }

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.jKey.wasPressedThisFrame)
                Recenter();

            openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, Time.unscaledDeltaTime * (open ? 7f : 9f));
            float amount = Mathf.Clamp01(openAmount);
            float eased = amount < 0.5f ? 2f * amount * amount : 1f - Mathf.Pow(-2f * amount + 2f, 2f) * 0.5f;

            bool nowVisible = openAmount > 0.001f;
            if (nowVisible != visible)
            {
                visible = nowVisible;
                SetAllActive(visible);
            }
            if (!visible)
            {
                ReleaseCursor();
                return;
            }

            if (!placed && open) PlaceOrb();

            if (open && Time.unscaledTime >= nextThemeCheck)
            {
                nextThemeCheck = Time.unscaledTime + 0.5f;
                if (ThemeDiff(CurrentTheme(), themeStamp) > 0.02f) Rebuild();
            }

            bool nowDisconnect = DisconnectEnabled();
            if (nowDisconnect != disconnectShown && sidebar != null) { disconnectShown = nowDisconnect; RebuildSidebar(); }

            if (searchLabel != null)
            {
                bool empty = string.IsNullOrEmpty(search);
                searchLabel.text = empty ? "Search..." : search;
                searchLabel.color = empty ? themeStamp.muted : themeStamp.bright;
            }

            UpdateCursor();

            float now = Time.unscaledTime;
            float delta = Time.unscaledDeltaTime;
            for (int i = 0; i < windows.Count; i++)
            {
                Window window = windows[i];
                if (window.root == null) continue;

                bool want;
                if (window == keyboardWindow) want = open && keyboardOpen;
                else if (window == resultsWindow) want = open && search.Trim().Length > 0;
                else want = open && (window.sidebar || window.state.openWindow);
                window.showTarget = want ? 1f : 0f;
                if (window.showTarget > 0f && !window.root.activeSelf)
                {
                    window.root.SetActive(true);
                    window.born = now;
                    window.yaw = window.state.yaw;
                    window.pitch = window.state.pitch;
                }
                window.show = Mathf.MoveTowards(window.show, window.showTarget, delta * (window.showTarget > 0f ? 7f : 11f));
                if (window.show <= 0.001f)
                {
                    if (window.root.activeSelf) window.root.SetActive(false);
                    continue;
                }

                if (!window.dragging)
                {
                    window.yaw = Mathf.LerpAngle(window.yaw * Mathf.Rad2Deg, window.state.yaw * Mathf.Rad2Deg, 1f - Mathf.Exp(-delta * 18f)) * Mathf.Deg2Rad;
                    window.pitch = Mathf.Lerp(window.pitch, window.state.pitch, 1f - Mathf.Exp(-delta * 18f));
                }

                if (window.sidebar) window.collapseTarget = 0f;
                window.collapse = Mathf.MoveTowards(window.collapse, window.collapseTarget, delta * 10f);
                float expanded = window.expanded;
                float collapsed = TitleHeight + 4f;
                window.rect.sizeDelta = new Vector2(WindowWidth, Mathf.Lerp(expanded, collapsed, window.collapse));
                window.body.sizeDelta = new Vector2(WindowWidth - 10f, Mathf.Lerp(window.bodyFull, 0f, window.collapse));

                window.state.scroll = Mathf.Clamp(window.state.scroll, 0f, window.maxScroll);
                window.scroll = Mathf.Lerp(window.scroll, window.state.scroll, 1f - Mathf.Exp(-delta * 16f));
                window.content.anchoredPosition = new Vector2(0f, window.scroll);

                if (window.thumb != null && window.maxScroll > 0f)
                {
                    float travel = Mathf.Max(0f, window.bodyFull - window.thumb.rectTransform.sizeDelta.y);
                    float ratio = window.scroll / window.maxScroll;
                    window.thumb.rectTransform.anchoredPosition = new Vector2(0f, (0.5f - ratio) * travel);
                }

                float age = now - window.born - window.delay;
                float progress = Mathf.Clamp01(age / 0.36f);
                float appear = age < 0f ? 0f : 1f - Mathf.Pow(1f - progress, 3f);
                float close = Mathf.Lerp(0.82f, 1f, eased);
                float alpha = appear * window.show * eased;
                float scale = (0.78f + 0.22f * appear + Mathf.Sin(progress * Mathf.PI) * 0.05f * (1f - progress)) * close * window.show;
                window.root.transform.localScale = Vector3.one * WindowScale * Mathf.Max(0.01f, scale);
                PlaceWindow(window, window.yaw, window.pitch, alpha);
            }

            PlaceWatermark();
            if (watermarkGroup != null) watermarkGroup.alpha = eased;

            StepTweens();
            ApplyFont();
            if (open && openAmount > 0.6f) HandleInput();

            if (watermarkStat != null)
            {
                string status = "";
                if (ShowFps) status += Mathf.RoundToInt(fps) + " FPS";
                if (ShowClock) { if (status.Length > 0) status += "  |  "; status += DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture); }
                watermarkStat.text = status;
            }
        }
    }
}
