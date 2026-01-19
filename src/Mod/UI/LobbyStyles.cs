using UnityEngine;

namespace SiroccoLobby.UI
{
    public static class LobbyStyles
    {
        public static GUIStyle? TitleStyle;
        public static GUIStyle? ButtonStyle;
        public static GUIStyle? CaptainButtonStyle;
        public static GUIStyle? LobbyCardStyle;
        public static GUIStyle? HeaderStyle;
        public static GUIStyle? SteamIdStyle;
        public static GUIStyle? WindowStyle;
        public static GUIStyle? BoxStyle;
        public static GUIStyle? TeamSelectedStyle;
        public static GUIStyle? TeamUnselectedStyle;
        public static GUIStyle? PlayerNameStyle;
        public static GUIStyle? PlayerCapStyle;
        public static GUIStyle? PlayerStatusStyle;

        private static bool _initialized;

        // Oceanic Palette
        private static readonly Color ColorOceanDeep = HexToColor("0a1628");   // Background
        private static readonly Color ColorOceanDark = HexToColor("0f2744");   // Secondary BG
        private static readonly Color ColorOceanMedium = HexToColor("1a3a5c"); // Component BG
        private static readonly Color ColorTealMedium = HexToColor("1a7a7a");  // Borders/Accents
        private static readonly Color ColorAquaGlow = HexToColor("5ce6e6");    // Text/Highlight
        private static readonly Color ColorSilver = HexToColor("c0c0c0");      // Subtitles

        public static void Init()
        {
            if (_initialized) return;
            if (GUI.skin == null) return;

            try
            {
                // Title
                TitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = ColorAquaGlow },
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(0,0,5,10) // Reduced margins
                };

                // Standard Button (Rounded)
                ButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { 
                        textColor = ColorAquaGlow,
                        background = MakeRoundedTex(128, 32, ColorOceanMedium, 8)
                    },
                    hover = { 
                        textColor = Color.white,
                        background = MakeRoundedTex(128, 32, HexToColor("2d5a8a"), 8)
                    },
                    active = {
                        background = MakeRoundedTex(128, 32, HexToColor("0d4d4d"), 8)
                    },
                    padding = new RectOffset(10, 10, 5, 5),
                    margin = new RectOffset(2, 2, 2, 2),
                    border = new RectOffset(8, 8, 8, 8) 
                };

                // Captain Selection Button (Rounded)
                CaptainButtonStyle = new GUIStyle(ButtonStyle)
                {
                    fontSize = 13,
                    padding = new RectOffset(8, 8, 4, 4),
                    normal = {
                        textColor = ColorAquaGlow,
                        background = MakeRoundedTex(128, 32, new Color(0.1f, 0.23f, 0.36f, 0.8f), 6)
                    }
                };

                // Lobby List / Card Item (Rounded)
                LobbyCardStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeRoundedTex(128, 128, ColorOceanMedium, 10) },
                    padding = new RectOffset(10, 10, 8, 8),
                    margin = new RectOffset(0, 0, 4, 4),
                    border = new RectOffset(10, 10, 10, 10)
                };

                // Headers
                HeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16, // Slightly smaller
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = ColorAquaGlow },
                    margin = new RectOffset(0, 0, 5, 2),
                    wordWrap = false // Prevent [READY] from breaking
                };

                // Small Text / Steam IDs
                SteamIdStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = ColorSilver }
                };
                
                // Main Window Background (Rounded!)
                WindowStyle = new GUIStyle(GUI.skin.window)
                {
                    normal = { background = MakeRoundedTex(512, 512, ColorOceanDeep, 16) },
                    padding = new RectOffset(15, 15, 15, 15),
                    border = new RectOffset(16, 16, 16, 16)
                };
                
                // Content Box (Rounded)
                BoxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeRoundedTex(128, 128, ColorOceanDark, 12) },
                    padding = new RectOffset(8, 8, 8, 8),
                    border = new RectOffset(12, 12, 12, 12)
                };

                TeamSelectedStyle = new GUIStyle(ButtonStyle)
                {
                    normal = { 
                        textColor = Color.white,
                        background = MakeRoundedTex(128, 32, new Color(0.18f, 0.48f, 0.72f, 1f), 6) // Vivid Blue/Teal
                    },
                    hover = {
                        textColor = Color.white,
                        background = MakeRoundedTex(128, 32, new Color(0.2f, 0.6f, 0.9f, 1f), 6)
                    },
                    margin = new RectOffset(0, 0, 0, 0)
                };

                TeamUnselectedStyle = new GUIStyle(ButtonStyle)
                {
                    normal = { 
                        textColor = ColorSilver,
                        background = MakeRoundedTex(128, 32, new Color(0.1f, 0.15f, 0.2f, 0.5f), 6) // Dim/Static
                    },
                    hover = {
                        textColor = Color.white,
                        background = MakeRoundedTex(128, 32, new Color(0.15f, 0.4f, 0.6f, 1f), 6) // Highlight on hover (looks like selected)
                    },
                    margin = new RectOffset(0, 0, 0, 0)
                };

                PlayerNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = Color.white },
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleLeft
                };

                PlayerCapStyle = new GUIStyle(PlayerNameStyle)
                {
                    fontSize = 12,
                    normal = { textColor = ColorSilver }
                };

                PlayerStatusStyle = new GUIStyle(PlayerNameStyle)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = ColorAquaGlow },
                    alignment = TextAnchor.MiddleRight
                };

                _initialized = true;
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Msg($"[LobbyStyles] Init failed: {ex.Message}");
            }
        }

        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public static Texture2D MakeRoundedTex(int width, int height, Color col, int radius)
        {
            Color[] pix = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Check corners
                    bool inside = true;
                    
                    // Bottom-Left
                    if (x < radius && y < radius) 
                        inside = Vector2.Distance(new Vector2(x,y), new Vector2(radius, radius)) <= radius;
                    // Bottom-Right
                    else if (x >= width - radius && y < radius) 
                        inside = Vector2.Distance(new Vector2(x,y), new Vector2(width - radius - 1, radius)) <= radius;
                    // Top-Left
                    else if (x < radius && y >= height - radius) 
                        inside = Vector2.Distance(new Vector2(x,y), new Vector2(radius, height - radius - 1)) <= radius;
                    // Top-Right
                    else if (x >= width - radius && y >= height - radius) 
                        inside = Vector2.Distance(new Vector2(x,y), new Vector2(width - radius - 1, height - radius - 1)) <= radius;

                    pix[y * width + x] = inside ? col : new Color(0,0,0,0);
                }
            }
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color color))
                return color;
            return Color.white;
        }
    }
}
