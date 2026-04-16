using MudBlazor;

namespace TicketPrime.Web.Theme;

public static class TicketPrimeTheme
{
    public const string Primary = "#0891B2";
    public const string AccentMint = "#10B981";
    public const string Background = "#0F172A";
    public const string Surface = "#1E293B";

    public static MudTheme Create() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = Primary,
            Secondary = AccentMint,
            Tertiary = "#38BDF8",
            Background = Background,
            Surface = Surface,
            DrawerBackground = Surface,
            DrawerText = "#E2E8F0",
            AppbarBackground = Surface,
            AppbarText = "#E2E8F0",
            TextPrimary = "#E2E8F0",
            TextSecondary = "#94A3B8",
            TextDisabled = "#64748B",
            ActionDefault = "#94A3B8",
            Divider = "#1E293B",
            DividerLight = "#334155",
            TableLines = "#1E293B",
            LinesDefault = "#1E293B",
            LinesInputs = "#334155",
            HoverOpacity = 0.05,
            DarkContrastText = "#0F172A",
            PrimaryContrastText = "#E2E8F0",
            SecondaryContrastText = "#042F2E",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
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
