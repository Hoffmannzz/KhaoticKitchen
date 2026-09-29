using UnityEngine;

namespace KitchenChaos.UI
{
    /// <summary>
    /// Base component for the multiplayer menus. They are drawn with IMGUI so they
    /// work without any extra scene or prefab setup.
    /// </summary>
    public abstract class MenuPanel : MonoBehaviour
    {
        protected const float referenceWidth = 1920F;
        protected const float referenceHeight = 1080F;

        protected GUIStyle PanelStyle { get; private set; }
        protected GUIStyle TitleStyle { get; private set; }
        protected GUIStyle LabelStyle { get; private set; }
        protected GUIStyle SmallLabelStyle { get; private set; }
        protected GUIStyle ButtonStyle { get; private set; }
        protected GUIStyle FieldStyle { get; private set; }
        protected GUIStyle ErrorStyle { get; private set; }

        private static Texture2D panelTexture;
        private static Texture2D buttonTexture;
        private static Texture2D buttonHoverTexture;
        private static Texture2D fieldTexture;

        protected static readonly Color accent = new Color(1F, 0.64F, 0.16F);
        protected static readonly Color[] chefColors =
        {
            new Color(0F, 0.73F, 0.93F),
            new Color(0.3F, 0.85F, 0.3F),
            new Color(0.89F, 0.18F, 0F),
            new Color(0.97F, 0.71F, 0F)
        };
        protected static readonly string[] chefNames = { "Blue", "Green", "Red", "Yellow" };

        private void OnGUI()
        {
            CreateStyles();

            var scale = Mathf.Min(Screen.width / referenceWidth, Screen.height / referenceHeight);
            var offset = new Vector2((Screen.width - referenceWidth * scale) * 0.5F, (Screen.height - referenceHeight * scale) * 0.5F);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1F));
            GUI.depth = -100;

            DrawPanel();

            GUI.matrix = Matrix4x4.identity;
        }

        protected abstract void DrawPanel();

        protected Rect BeginWindow(float width, float height, string title)
        {
            var rect = new Rect((referenceWidth - width) * 0.5F, (referenceHeight - height) * 0.5F - 60F, width, height);
            GUI.Box(rect, GUIContent.none, PanelStyle);
            GUILayout.BeginArea(new Rect(rect.x + 40F, rect.y + 30F, rect.width - 80F, rect.height - 60F));
            GUILayout.Label(title, TitleStyle);
            GUILayout.Space(10F);
            return rect;
        }

        protected static void EndWindow() => GUILayout.EndArea();

        protected bool Button(string text, float width = 0F)
        {
            var options = width > 0F ? new[] { GUILayout.Width(width), GUILayout.Height(56F) } : new[] { GUILayout.Height(56F) };
            return GUILayout.Button(text, ButtonStyle, options);
        }

        protected string TextField(string label, string value, int maxLength, float labelWidth = 220F)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, LabelStyle, GUILayout.Width(labelWidth));
            value = GUILayout.TextField(value ?? string.Empty, maxLength, FieldStyle, GUILayout.Height(48F));
            GUILayout.EndHorizontal();
            return value;
        }

        protected void ColoredLabel(string text, Color color)
        {
            var previous = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(text, LabelStyle);
            GUI.contentColor = previous;
        }

        protected static Color GetChefColor(int index) => chefColors[Mathf.Clamp(index, 0, chefColors.Length - 1)];
        protected static string GetChefName(int index) => chefNames[Mathf.Clamp(index, 0, chefNames.Length - 1)];

        private void CreateStyles()
        {
            if (PanelStyle != null) return;

            if (panelTexture == null) panelTexture = CreateTexture(new Color(0.09F, 0.07F, 0.06F, 0.94F));
            if (buttonTexture == null) buttonTexture = CreateTexture(new Color(0.85F, 0.45F, 0.08F, 1F));
            if (buttonHoverTexture == null) buttonHoverTexture = CreateTexture(new Color(1F, 0.6F, 0.15F, 1F));
            if (fieldTexture == null) fieldTexture = CreateTexture(new Color(1F, 1F, 1F, 0.12F));

            PanelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };

            TitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = accent }
            };

            LabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            SmallLabelStyle = new GUIStyle(LabelStyle)
            {
                fontSize = 22,
                normal = { textColor = new Color(1F, 1F, 1F, 0.7F) }
            };

            ErrorStyle = new GUIStyle(LabelStyle) { normal = { textColor = new Color(1F, 0.4F, 0.35F) } };

            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { background = buttonTexture, textColor = Color.white },
                hover = { background = buttonHoverTexture, textColor = Color.white },
                active = { background = buttonHoverTexture, textColor = Color.black },
                focused = { background = buttonTexture, textColor = Color.white }
            };

            FieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 6, 6),
                normal = { background = fieldTexture, textColor = Color.white },
                focused = { background = fieldTexture, textColor = Color.white },
                hover = { background = fieldTexture, textColor = Color.white }
            };
        }

        private static Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
