using MudBlazor;

namespace Web.Theming;

public sealed record AppThemeOption(string Key, string Label, MudTheme Theme);

public static class AppThemeCatalog
{
    public const string ModernSlateKey = "modern-slate";
    public const string SoftSandKey = "soft-sand";
    public const string CalmOceanKey = "calm-ocean";

    private static readonly AppThemeOption[] AvailableThemes =
    [
        new(
            ModernSlateKey,
            "Ardoise moderne",
            BuildTheme(
                primary: "#2458C5",
                secondary: "#00897B",
                info: "#1D4ED8",
                background: "#F4F6FA",
                surface: "#FFFFFF",
                textPrimary: "#1F2937",
                textSecondary: "#4B5563")),
        new(
            SoftSandKey,
            "Sable clair",
            BuildTheme(
                primary: "#8A5A1A",
                secondary: "#355C7D",
                info: "#0F766E",
                background: "#FAF7F1",
                surface: "#FFFFFF",
                textPrimary: "#2D2A26",
                textSecondary: "#57534E")),
        new(
            CalmOceanKey,
            "Océan doux",
            BuildTheme(
                primary: "#0E7490",
                secondary: "#475569",
                info: "#0284C7",
                background: "#F1F7FB",
                surface: "#FFFFFF",
                textPrimary: "#0F172A",
                textSecondary: "#334155"))
    ];

    private static readonly IReadOnlyDictionary<string, AppThemeOption> ThemesByKey =
        AvailableThemes.ToDictionary(item => item.Key, StringComparer.Ordinal);

    public static string DefaultThemeKey => ModernSlateKey;

    public static IReadOnlyCollection<AppThemeOption> All => AvailableThemes;

    public static MudTheme ResolveTheme(string key) =>
        ThemesByKey.TryGetValue(key, out var option)
            ? option.Theme
            : ThemesByKey[DefaultThemeKey].Theme;

    public static string ResolveLabel(string key) =>
        ThemesByKey.TryGetValue(key, out var option)
            ? option.Label
            : ThemesByKey[DefaultThemeKey].Label;

    private static MudTheme BuildTheme(
        string primary,
        string secondary,
        string info,
        string background,
        string surface,
        string textPrimary,
        string textSecondary) =>
        new()
        {
            PaletteLight = new PaletteLight
            {
                Primary = primary,
                Secondary = secondary,
                Info = info,
                Success = "#15803D",
                Warning = "#B45309",
                Error = "#B91C1C",
                Background = background,
                Surface = surface,
                AppbarBackground = "rgba(255, 255, 255, 0.94)",
                AppbarText = textPrimary,
                TextPrimary = textPrimary,
                TextSecondary = textSecondary,
                DrawerBackground = surface,
                DrawerText = textPrimary,
                DrawerIcon = textSecondary,
                ActionDefault = textSecondary,
                ActionDisabled = "#9CA3AF",
                ActionDisabledBackground = "#E5E7EB",
                LinesDefault = "#D7DEE8",
                LinesInputs = "#CBD5E1",
                TableLines = "#E2E8F0",
                TableStriped = "#F8FAFC"
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px"
            }
        };
}
