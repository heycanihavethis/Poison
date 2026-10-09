using System;
using System.Collections.Generic;
using Poison.Classes.Menu;
using UnityEngine;
using static Poison.Menu.Main;

namespace Poison.Menu
{
    public partial class UI
    {
        private static readonly string[] tabNames = { "Modules", "Config", "Main Settings", "Quality Settings", "Input" };
        private static readonly Color textColor = new Color32(224, 224, 224, 255);
        private static readonly Color dimColor = new Color32(146, 146, 146, 255);
        private static readonly Color cardColor = new Color32(13, 13, 13, 255);
        private static readonly Color lineColor = new Color32(43, 43, 43, 255);
        private const float SidebarWidth = 140;
        private const float ContentLeft = 168;
        private const float TabStep = 134;
        private readonly Scroll cardScroll = new Scroll();
        private readonly Scroll categoryScroll = new Scroll();
        private readonly List<Entry> cards = new List<Entry>();
        private readonly HashSet<ButtonInfo> seenCards = new HashSet<ButtonInfo>();
        private Font monoFont;
        private Texture2D rainbow;
        private GUIStyle labelStyle;
        private GUIStyle headingStyle;
        private GUIStyle versionStyle;
        private GUIStyle centerStyle;
        private GUIStyle descriptionStyle;
        private GUIStyle categoryStyle;
        private readonly GUIContent labelContent = new GUIContent();
        private int tab;
        private string category;
        private string query = "";
        private bool draggingWindow;
        private Vector2 dragOffset;

        private void SetClassic(bool enabled)
        {
            if (options.classicUI == enabled) return;
            options.classicUI = enabled;
            ClearInput();
            textFocused = false;
            focusSearch = false;
            activePanel = null;
            draggingWindow = false;
            PointerControl = 0;
            GUIUtility.keyboardControl = 0;
            refresh = true;
            if (!enabled) EnsurePanels();
            SaveOptions();
        }

        private void SetupClassic()
        {
            if (labelStyle != null) return;
            try
            {
                HashSet<string> installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                string[] candidates = { "Consolas", "Lucida Console", "Courier New", "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono", "Monospace" };
                List<string> present = new List<string>();
                foreach (string name in candidates)
                    if (installed.Contains(name)) present.Add(name);
                if (present.Count > 0)
                    monoFont = Font.CreateDynamicFontFromOSFont(present.ToArray(), 12);
            }
            catch { }
            labelStyle = new GUIStyle(textStyle) { fontSize = 12, font = monoFont != null ? monoFont : textStyle.font };
            headingStyle = new GUIStyle(labelStyle) { fontSize = 19, alignment = TextAnchor.UpperCenter };
            versionStyle = new GUIStyle(headingStyle) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            centerStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
            descriptionStyle = new GUIStyle(labelStyle) { wordWrap = false, alignment = TextAnchor.UpperLeft };
            categoryStyle = new GUIStyle(labelStyle) { fontSize = 11, wordWrap = false, alignment = TextAnchor.MiddleLeft };
        }

        private bool Button(Rect rect, string caption, bool flat = false)
        {
            bool hovered = GUI.enabled && pointerInside && rect.Contains(GuiPoint);
            Color fill = !flat && hovered ? (Color)new Color32(31, 31, 31, 255) : cardColor;
            Fill(rect, fill, 2);
            bool clicked = MenuButton(rect, GUIContent.none, GUIStyle.none);
            FitLabel(rect, caption, centerStyle, GUI.enabled ? textColor : dimColor);
            return clicked;
        }

        private static void Fill(Rect rect, Color color, float radius)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        private void FitLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(text) || rect.width <= 2 || rect.height <= 2) return;
            rect = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            int originalSize = style.fontSize;
            Matrix4x4 originalMatrix = GUI.matrix;
            labelContent.text = text;
            try
            {
                Vector2 size = style.CalcSize(labelContent);
                if (size.x > rect.width || size.y > rect.height)
                {
                    float fit = Mathf.Min(rect.width / Mathf.Max(1, size.x), rect.height / Mathf.Max(1, size.y));
                    style.fontSize = Mathf.Max(1, Mathf.FloorToInt(originalSize * fit));
                    size = style.CalcSize(labelContent);
                    while (style.fontSize > 1 && (size.x > rect.width || size.y > rect.height))
                    {
                        style.fontSize--;
                        size = style.CalcSize(labelContent);
                    }
                    if (size.x > rect.width || size.y > rect.height)
                    {
                        fit = Mathf.Min(rect.width / Mathf.Max(1, size.x), rect.height / Mathf.Max(1, size.y));
                        GUIUtility.ScaleAroundPivot(new Vector2(fit, fit), rect.position);
                        rect.width /= fit;
                        rect.height /= fit;
                    }
                }
                Label(rect, text, style, color);
            }
            finally
            {
                style.fontSize = originalSize;
                GUI.matrix = originalMatrix;
            }
        }

        private void DrawClassic(float width, float height)
        {
            SetupClassic();
            Rect window = WindowRect(width, height);
            DragWindow(ref window, width, height);
            Fill(window, Color.black, 2);

            bool inside = pointerInside;
            pointerInside = window.Contains(GuiPoint);
            GUI.BeginGroup(window);
            try
            {
                DrawMenu(window.width, window.height);
                DrawRainbow(window.width);
            }
            finally
            {
                GUI.EndGroup();
                pointerInside = inside;
            }
        }

        private void DrawRainbow(float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (rainbow == null)
            {
                const int resolution = 256;
                rainbow = new Texture2D(resolution, 1, TextureFormat.RGBA32, false)
                {
                    name = "Rainbow",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color[resolution];
                for (int i = 0; i < resolution; i++)
                    pixels[i] = Color.HSVToRGB(i / (float)resolution, 0.8f, 0.95f);
                rainbow.SetPixels(pixels);
                rainbow.Apply(false, true);
            }
            float phase = options.animations ? Mathf.Repeat(Time.unscaledTime * 0.32f, 1f) : 0f;
            GUI.DrawTextureWithTexCoords(new Rect(2, 0, width - 4, 0.5f), rainbow, new Rect(phase, 0, 1, 1));
        }

        private Rect WindowRect(float width, float height)
        {
            float w = Mathf.Min(1060, width - 32);
            float h = Mathf.Min(526, height - 32);
            return new Rect(16 + (width - w - 32) * options.classicWindowX,
                16 + (height - h - 32) * options.classicWindowY, w, h);
        }

        private void DragWindow(ref Rect window, float width, float height)
        {
            int id = GUIUtility.GetControlID("classic-window-drag".GetHashCode(), FocusType.Passive);
            Event current = InputEvent;
            EventType type = InputType(id);
            if (!GUI.enabled || CurrentPrompt != null || !Application.isFocused)
            {
                if (PointerControl == id) PointerControl = 0;
                draggingWindow = false;
                return;
            }
            float tabsEnd = ContentLeft + tabNames.Length * TabStep;
            bool overDragArea = new Rect(window.x, window.y, SidebarWidth, 78).Contains(GuiPoint) ||
                new Rect(window.x + tabsEnd, window.y, window.width - tabsEnd, 40).Contains(GuiPoint);
            if (type == EventType.MouseDown && current.button == 0 && overDragArea)
            {
                PointerControl = id;
                draggingWindow = true;
                dragOffset = GuiPoint - window.position;
                ClearInput();
                current.Use();
            }
            if (PointerControl != id) { draggingWindow = false; return; }
            if (!draggingWindow) { PointerControl = 0; return; }
            if (type == EventType.MouseDrag)
            {
                Vector2 position = GuiPoint - dragOffset;
                float travelX = Mathf.Max(0, width - window.width - 32);
                float travelY = Mathf.Max(0, height - window.height - 32);
                options.classicWindowX = travelX > 0 ? Mathf.Clamp01((position.x - 16) / travelX) : 0.5f;
                options.classicWindowY = travelY > 0 ? Mathf.Clamp01((position.y - 16) / travelY) : 0.5f;
                window = WindowRect(width, height);
                Changed();
                current.Use();
            }
            if (type == EventType.MouseUp || type == EventType.Ignore)
            {
                PointerControl = 0;
                draggingWindow = false;
                if (type == EventType.MouseUp) current.Use();
            }
        }

        private void DrawMenu(float width, float height)
        {
            SetupClassic();
            const float rail = SidebarWidth;
            const float left = ContentLeft;
            Fill(new Rect(0, 0, width, height), Color.black, 2);
            Fill(new Rect(0, 0, rail, height), cardColor, 2);
            FitLabel(new Rect(8, 14, rail - 16, 26), "Poison", headingStyle, textColor);
            FitLabel(new Rect(8, 40, rail - 16, 22), "v" + PluginInfo.Version, versionStyle, textColor);

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && CurrentPrompt == null;
            for (int i = 0; i < tabNames.Length; i++)
            {
                if (!Button(new Rect(left + i * TabStep, 5, 126, 28), tabNames[i], flat: true)) continue;
                tab = i;
                cardScroll.Reset();
                ClearInput();
            }

            if (Button(new Rect(10, height - 76, rail - 20, 28), "Original UI"))
                SetClassic(false);
            if (Button(new Rect(10, height - 42, rail - 20, 26), "Hide [\\]")) ToggleGUI();

            if (tab == 0)
            {
                DrawCategories(rail, height);
                DrawCards(new Rect(left, 54, width - left - 24, height - 68));
            }
            else if (tab == 1) DrawConfig(left, width, height);
            else if (tab == 2) DrawCards(new Rect(left, 54, width - left - 24, height - 68));
            else if (tab == 3) DrawQuality(left, width);
            else DrawInputSettings(new Rect(left, 54, width - left - 24, height - 68), true);
            GUI.enabled = enabled;
        }

        private void DrawCategories(float rail, float height)
        {
            if (focusSearch)
            {
                category = null;
                focusedInput = "ui-classic-search";
                inputText = query;
                focusSearch = false;
            }
            string value = SearchField(new Rect(10, height - 114, rail - 20, 26));
            if (value != query) { query = value; cardScroll.Reset(); }
            const float rowStep = 22;
            float listHeight = height - 214;
            BeginScroll(new Rect(8, 86, rail - 12, listHeight), categoryScroll, (categories.Count + 1) * rowStep, dimColor);
            if (CategoryRow(new Rect(0, 0, rail - 30, 20), "All modules", category == null))
            {
                category = null;
                cardScroll.Reset();
                ClearInput();
            }
            for (int i = 0; i < categories.Count; i++)
            {
                Rect rect = new Rect(0, (i + 1) * rowStep, rail - 30, 20);
                if (rect.yMax < categoryScroll.value || rect.y > categoryScroll.value + listHeight) continue;
                if (!CategoryRow(rect, categories[i], category == categories[i])) continue;
                category = categories[i];
                cardScroll.Reset();
                ClearInput();
            }
            EndScroll();
        }

        private bool CategoryRow(Rect rect, string caption, bool selected)
        {
            bool hovered = GUI.enabled && pointerInside && rect.Contains(GuiPoint);
            if (selected)
                FitLabel(new Rect(rect.x, rect.y, 10, rect.height), ">", categoryStyle, textColor);
            bool clicked = MenuButton(rect, GUIContent.none, GUIStyle.none);
            float labelWidth = rect.width - 12;
            FitLabel(new Rect(rect.x + 10, rect.y, labelWidth, rect.height), caption,
                categoryStyle, GUI.enabled && (selected || hovered) ? textColor : dimColor);
            if (hovered) tip = caption;
            return clicked;
        }

        private string SearchField(Rect rect)
        {
            const string id = "ui-classic-search";
            bool focused = focusedInput == id;
            if (Pressed(rect))
            {
                focusedInput = id;
                inputText = query;
                inputClicked = true;
                focused = true;
                InputEvent.Use();
            }
            Fill(rect, lineColor, 2);
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), cardColor, 1);
            string value = focused ? inputText : query;
            string display = value.Length == 0 && !focused ? "Search..." : value;
            if (focused && Mathf.Repeat(Time.unscaledTime, 1) < 0.5f) display += "|";
            FitLabel(new Rect(rect.x + 5, rect.y, rect.width - 10, rect.height), display, labelStyle, textColor);
            return value;
        }

        private void DrawCards(Rect area)
        {
            cards.Clear();
            seenCards.Clear();
            string filter = query.Trim();
            foreach (Entry entry in entries)
            {
                if (entry.button.label) continue;
                bool settings = Matches(entry.category, "Settings");
                if (tab == 2)
                {
                    if (!settings) continue;
                }
                else if (tab == 1)
                {
                    string name = entry.button.buttonText;
                    if (name != "Save Preferences" && name != "Load Preferences" && name != "Backup Preferences" && name != "Disable Autosave") continue;
                }
                else
                {
                    if (category != null ? entry.category != category : settings || entry.category == "Main") continue;
                    if (filter.Length > 0 && !Matches(entry.title, filter) && !Matches(entry.button.buttonText, filter) &&
                        !Matches(entry.description, filter) && !Matches(entry.category, filter)) continue;
                }
                if (seenCards.Add(entry.button)) cards.Add(entry);
            }
            cards.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));

            const int columns = 4;
            const float gap = 18;
            const float cardHeight = 74;
            const float rowHeight = cardHeight + 6;
            float cardWidth = (area.width - 14 - gap * (columns - 1)) / columns;
            int rows = (cards.Count + columns - 1) / columns;
            BeginScroll(area, cardScroll, rows * rowHeight, dimColor);
            for (int i = 0; i < cards.Count; i++)
            {
                float y = (i / columns) * rowHeight;
                if (y + cardHeight < cardScroll.value || y > cardScroll.value + area.height) continue;
                DrawCard(cards[i], new Rect((i % columns) * (cardWidth + gap), y, cardWidth, cardHeight));
            }
            if (cards.Count == 0)
                FitLabel(new Rect(0, 20, area.width - 20, 30), "No modules found.", labelStyle, dimColor);
            EndScroll();
        }

        private void DrawCard(Entry entry, Rect rect)
        {
            var button = entry.button;
            Fill(rect, cardColor, 2);
            FitLabel(new Rect(rect.x + 5, rect.y + 3, rect.width - 10, 20), entry.title, labelStyle, textColor);
            Box(new Rect(rect.x + 4, rect.y + 25, rect.width - 8, 1), lineColor, 0);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && (!button.detected || allowDetected);
            if (button.incremental)
            {
                float half = (rect.width - 12) / 2;
                if (Button(new Rect(rect.x + 4, rect.y + 29, half, 22), "<")) Activate(button, false);
                if (Button(new Rect(rect.x + 8 + half, rect.y + 29, half, 22), ">")) Activate(button, true);
            }
            else if (button.isTogglable)
            {
                if (Checkbox(new Rect(rect.x + 4, rect.y + 29, rect.width - 8, 22),
                    "Enabled", button.enabled)) Activate(button, true);
            }
            else if (Button(new Rect(rect.x + 4, rect.y + 29, rect.width - 8, 22), "Run")) Activate(button, true);
            GUI.enabled = enabled;
            if (options.descriptions)
                FitLabel(new Rect(rect.x + 5, rect.y + 55, rect.width - 10, 17), entry.description, descriptionStyle, textColor);
            if (pointerInside && rect.Contains(GuiPoint))
                tip = entry.title + "\n" + entry.description;
        }

        private void DrawConfig(float left, float width, float height)
        {
            if (Button(new Rect(left, 54, 210, 30), "Save interface settings"))
            {
                SaveOptions();
                ShowStatus("Interface settings saved.");
            }
            if (Button(new Rect(left + 222, 54, 210, 30), "Reload interface settings"))
            {
                LoadOptions();
                ClearInput();
            }
            FitLabel(new Rect(left, 98, width - left - 24, 24), "Game preferences", labelStyle, dimColor);
            DrawCards(new Rect(left, 136, width - left - 24, height - 150));
        }

        private void DrawQuality(float left, float width)
        {
            float col = Mathf.Min(420, width - left - 24);
            Option(new Rect(left, 54, col, 32), "Smooth scrolling", ref options.smoothScroll);
            Option(new Rect(left, 94, col, 32), "Animations", ref options.animations);
            Option(new Rect(left, 134, col, 32), "Descriptions", ref options.descriptions);
            Option(new Rect(left, 174, col, 32), "Tooltips", ref options.tooltips);
            Option(new Rect(left, 214, col, 32), "Arraylist", ref options.arraylist);
            Option(new Rect(left, 254, col, 32), "Arraylist over menu", ref options.listOverMenu);
            FitLabel(new Rect(left, 304, col, 24), "UI scale: " + options.scale.ToString("P0"), labelStyle, textColor);
            if (Button(new Rect(left, 340, 100, 28), "-")) { options.scale = Mathf.Max(0.75f, options.scale - 0.05f); Changed(); }
            if (Button(new Rect(left + 112, 340, 100, 28), "+")) { options.scale = Mathf.Min(1.3f, options.scale + 0.05f); Changed(); }
        }

        private void Option(Rect rect, string caption, ref bool value)
        {
            if (!Checkbox(rect, caption, value)) return;
            value = !value;
            Changed();
        }

        private bool Checkbox(Rect rect, string caption, bool value)
        {
            bool clicked = MenuButton(rect, GUIContent.none, GUIStyle.none);
            Rect square = new Rect(rect.x + 3, rect.center.y - 6, 12, 12);
            Color fill = value ? (GUI.enabled ? textColor : dimColor) : lineColor;
            Fill(square, fill, 2);
            if (!string.IsNullOrEmpty(caption))
                FitLabel(new Rect(rect.x + 22, rect.y, rect.width - 28, rect.height), caption,
                    labelStyle, GUI.enabled ? textColor : dimColor);
            return clicked;
        }
    }
}
