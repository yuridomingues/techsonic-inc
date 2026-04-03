using MudBlazor;

namespace TicketPrime.Web.Theme;

public static class TicketPrimeTheme
{
    public const string Primary = "#7C3AED";
    public const string AccentMint = "#10B981";
    public const string Background = "#0F172A";

    public static MudTheme Create() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = Primary,
            Secondary = AccentMint,
            Tertiary = "#A78BFA",
            Background = Background,
            Surface = "#1E293B",
            DrawerBackground = "#1E293B",
            DrawerText = "#F1F5F9",
            AppbarBackground = Background,
            AppbarText = "#F8FAFC",
            TextPrimary = "#F1F5F9",
            TextSecondary = "#94A3B8",
            TextDisabled = "#64748B",
            ActionDefault = "#94A3B8",
            Divider = "#334155",
            DividerLight = "#475569",
            TableLines = "#334155",
            LinesDefault = "#334155",
            LinesInputs = "#475569",
            HoverOpacity = 0.08,
            DarkContrastText = "#0F172A",
            PrimaryContrastText = "#FFFFFF",
            SecondaryContrastText = "#042F2E",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "280px",
            DrawerMiniWidthLeft = "72px",
            AppbarHeight = "64px",
        },
        Shadows = new Shadow(),
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = ".875rem",
                FontWeight = "400",
                LineHeight = "1.5",
                LetterSpacing = ".00938em",
            },
            H1 = new H1Typography
            {
                FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = "2rem",
                FontWeight = "600",
                LineHeight = "1.2",
                LetterSpacing = "-.01562em",
            },
            H2 = new H2Typography
            {
                FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = "1.5rem",
                FontWeight = "600",
            },
            H3 = new H3Typography
            {
                FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = "1.25rem",
                FontWeight = "600",
            },
            H4 = new H4Typography
            {
                FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = "1.125rem",
                FontWeight = "600",
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = ".875rem",
                FontWeight = "600",
                TextTransform = "none",
            },
        },
    };
}
