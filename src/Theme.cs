namespace SimpleGsxIntegrator;

public static class Theme
{
    public static bool IsDarkMode { get; set; } = false;

    public static class Light
    {
        public static Color Background = Color.FromArgb(240, 240, 240);
        public static Color Surface = Color.White;
        public static Color Text = Color.Black;
        public static Color TextSecondary = Color.FromArgb(90, 90, 90);
        public static Color Border = Color.FromArgb(200, 200, 200);
        public static Color ButtonBackground = SystemColors.Control;
        public static Color ButtonText = Color.Black;
    }

    public static class Dark
    {
        public static Color Background = Color.FromArgb(30, 30, 30);
        public static Color Surface = Color.FromArgb(45, 45, 45);
        public static Color Text = Color.FromArgb(220, 220, 220);
        public static Color TextSecondary = Color.FromArgb(160, 160, 160);
        public static Color Border = Color.FromArgb(60, 60, 60);
        public static Color ButtonBackground = Color.FromArgb(55, 55, 55);
        public static Color ButtonText = Color.FromArgb(220, 220, 220);
    }

    public static Color Background => IsDarkMode ? Dark.Background : Light.Background;
    public static Color Surface => IsDarkMode ? Dark.Surface : Light.Surface;
    public static Color Text => IsDarkMode ? Dark.Text : Light.Text;
    public static Color TextSecondary => IsDarkMode ? Dark.TextSecondary : Light.TextSecondary;
    public static Color Border => IsDarkMode ? Dark.Border : Light.Border;
    public static Color ButtonBackground => IsDarkMode ? Dark.ButtonBackground : Light.ButtonBackground;
    public static Color ButtonText => IsDarkMode ? Dark.ButtonText : Light.ButtonText;
}
