using UnityEngine;

namespace PrehistoricFrontiers
{
    public static class HudTheme
    {
        public static readonly Color Ink = new Color(.20f, .23f, .19f);
        public static readonly Color Muted = new Color(.39f, .42f, .34f);
        public static readonly Color Accent = new Color(.28f, .45f, .29f);
        public static readonly Color Danger = new Color(.65f, .22f, .14f);
        static GUISkin skin;
        static Texture2D panel, normal, hover, pressed, selected, alert;

        public static GUISkin Skin
        {
            get
            {
                if (skin) return skin;
                skin = Object.Instantiate(GUI.skin);
                skin.name = "Battle HUD";
                panel = Surface(new Color(.94f, .92f, .85f), new Color(.54f, .44f, .30f), false);
                normal = Surface(new Color(.86f, .77f, .59f), new Color(.56f, .45f, .29f));
                hover = Surface(new Color(.97f, .86f, .65f), Accent);
                pressed = Surface(new Color(.67f, .75f, .57f), Accent);
                selected = Surface(new Color(.75f, .83f, .63f), Accent);
                alert = Surface(new Color(.97f, .84f, .72f), Danger);
                skin.label.normal.textColor = Ink;
                skin.box.normal.background = panel; skin.box.normal.textColor = Ink;
                skin.box.border = new RectOffset(3, 3, 3, 3);
                skin.window.normal.background = panel; skin.window.onNormal.background = panel;
                skin.window.border = new RectOffset(3, 3, 3, 3);
                StyleButton(skin.button);
                skin.verticalScrollbar.fixedWidth = 10;
                skin.verticalScrollbar.normal.background = normal;
                skin.verticalScrollbarThumb.normal.background = pressed;
                skin.verticalScrollbarThumb.border = new RectOffset(3, 3, 3, 3);
                skin.verticalScrollbarUpButton.fixedHeight = 0;
                skin.verticalScrollbarDownButton.fixedHeight = 0;
                return skin;
            }
        }
        static Texture2D Surface(Color fill, Color edge, bool wood = true)
        {
            const int width = 256, height = 128, rim = 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = wood ? "HUD wood" : "HUD limestone",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int distance = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                if ((x < 2 || x >= width - 2) && (y < 2 || y >= height - 2))
                { pixels[y * width + x] = Color.clear; continue; }
                Color color;
                if (distance < rim)
                {
                    color = Color.Lerp(edge, new Color(.65f, .51f, .32f), .28f) * (1 + WoodGrain(x, y) * .18f);
                    if (y == height - 1 || x == 0) color = Color.Lerp(color, Color.white, .20f);
                    else if (y == 0 || x == width - 1) color *= .78f;
                }
                else if (wood)
                {
                    color = fill * (1 + WoodGrain(x, y) * .075f);
                    if (distance == rim) color = Color.Lerp(color, Color.white, .15f);
                }
                else
                {
                    float cloud = Mathf.PerlinNoise(x * .043f + 9, y * .043f + 3) - .5f;
                    float fleck = Mathf.PerlinNoise(x * .65f, y * .65f) - .5f;
                    color = fill * (1 + cloud * .09f + fleck * .045f);
                    if (Mathf.PerlinNoise(x * .31f + 23, y * .31f + 8) > .79f) color *= .96f;
                }
                color.a = 1; pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }
        static float WoodGrain(float x, float y)
        {
            float bend = Mathf.PerlinNoise(x * .014f + 7, y * .018f + 13);
            float line = Mathf.Sin(y * .82f + bend * 8 + Mathf.Sin(x * .022f) * .8f);
            float fibre = Mathf.PerlinNoise(x * .035f, y * .46f) - .5f;
            return line * .48f + fibre * .7f;
        }
        static void StyleButton(GUIStyle style)
        {
            style.border = new RectOffset(3, 3, 3, 3);
            style.normal.background = normal; style.hover.background = hover; style.active.background = pressed;
            style.onNormal.background = selected; style.onHover.background = hover; style.onActive.background = pressed;
            style.focused.background = hover; style.onFocused.background = hover;
            style.normal.textColor = style.hover.textColor = style.active.textColor = Ink;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = Ink;
            style.focused.textColor = style.onFocused.textColor = Ink;
        }
        public static GUIStyle AlertButton(GUIStyle source)
        {
            var result = new GUIStyle(source); result.normal.background = alert; result.normal.textColor = Danger; return result;
        }
        public static void Panel(Rect rect)
        {
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, .16f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Box(rect, GUIContent.none, Skin.box);
        }
        public static void Progress(Rect rect, float value)
        {
            var previous = GUI.color;
            GUI.color = new Color(.57f, .61f, .48f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = value < .3f ? Danger : Accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        public static string UnitName(string name) => name == "Raptor" ? "Раптор" : name == "Triceratops" ? "Трицератопс" : name == "Mammoth" ? "Мамонт" : name == "Worker" ? "Рабочий" : name;
    }
}
