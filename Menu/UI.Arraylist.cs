using Poison.Classes.Menu;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


using static Poison.Menu.Main;

namespace Poison.Menu
{
    public partial class UI
    {
        private sealed class ListRow
        {
            public ButtonInfo button;
            public string text;
            public bool active;
            public float alpha;
            public float measured;
            public int frame = -1;
        }

        private readonly Dictionary<ButtonInfo, ListRow> listRows = new Dictionary<ButtonInfo, ListRow>();
        private GUIStyle listStyle;

        public static bool ArraylistEnabled => Instance == null || Instance.options.arraylist;

        private void UpdateArraylist()
        {
            foreach (ListRow row in listRows.Values) row.active = false;
            foreach (Entry entry in entries)
            {
                ButtonInfo button = entry.button;
                if (!button.enabled || button.label || button.hideFromArraylist || entry.category == "Temporary Category") continue;
                if ((hideSettings && entry.category.Contains("Settings")) || (hideMacros && entry.category.Contains("Macro"))) continue;
                if (!listRows.TryGetValue(button, out ListRow row))
                {
                    row = new ListRow { button = button };
                    listRows.Add(button, row);
                }
                row.text = entry.title ?? button.buttonText;
                row.active = true;
            }
            foreach (ButtonInfo removed in listRows.Where(pair => !pair.Value.active).Select(pair => pair.Key).ToArray())
                listRows.Remove(removed);
        }

        private void DrawArraylist()
        {
            if (!options.arraylist || allHidden || !showEnabledModsVR || listRows.Count == 0) return;

            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            bool enabled = GUI.enabled;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
            GUI.enabled = true;

            listStyle ??= new GUIStyle(textStyle);
            float fit = Mathf.Min(1, ViewHeight / 720f);
            listStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(arraylistScale * options.listScale * fit), 10, 64);
            listStyle.alignment = flipArraylist ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

            float margin = options.listMargin * fit;
            float rowHeight = listStyle.fontSize + 12;
            float step = rowHeight + options.listSpacing * fit;

            foreach (ListRow row in listRows.Values)
                row.measured = Mathf.Min(ViewWidth * 0.65f, listStyle.CalcSize(new GUIContent(row.text)).x + 28);

            List<ListRow> rows = options.listByWidth
                ? listRows.Values.OrderByDescending(row => row.measured).ThenBy(row => row.text, System.StringComparer.Ordinal).ToList()
                : listRows.Values.OrderBy(row => row.text, System.StringComparer.Ordinal).ToList();

            float speed = options.animations ? Ease(12) : 1;
            int overflow = 0;
            float y = margin;
            foreach (ListRow row in rows)
            {
                if (Event.current.type == EventType.Repaint && row.frame != Time.frameCount)
                {
                    row.alpha = options.animations ? Mathf.Lerp(row.alpha, 1, speed) : 1;
                    row.frame = Time.frameCount;
                }
                if (y + rowHeight > ViewHeight - margin)
                {
                    overflow++;
                    continue;
                }
                float width = row.measured;
                float x = flipArraylist ? ViewWidth - margin - width : margin;
                GUI.color = new Color(1, 1, 1, row.alpha);
                Rect rect = new Rect(x, y, width, rowHeight);
                if (options.listBackground)
                    Box(rect, Alpha(background, options.listOpacity), 3);
                if (options.listAccent)
                    Box(new Rect(flipArraylist ? rect.xMax - 4 : rect.x + 2, rect.y + 4, 2, rect.height - 8), Alpha(accent, 0.9f), 0);
                float inset = options.listAccent ? 14 : 12;
                Rect text = new Rect(rect.x + inset, rect.y, rect.width - inset - 12, rect.height);
                Label(text, row.text, listStyle, advancedArraylist ? accent : bright);
                y += step;
            }
            if (overflow > 0)
            {
                GUI.color = Color.white;
                float width = listStyle.CalcSize(new GUIContent("+" + overflow)).x + 28;
                float x = flipArraylist ? ViewWidth - margin - width : margin;
                Label(new Rect(x, y, width, rowHeight), "+" + overflow, listStyle, muted);
            }

            GUI.matrix = matrix;
            GUI.color = color;
            GUI.enabled = enabled;
        }
    }
}
