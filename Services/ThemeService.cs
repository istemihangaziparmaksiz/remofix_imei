using System;
using System.Windows;
using System.Windows.Media;
using ModernWpf;

namespace ImeiStudio.Services
{
    public static class ThemeService
    {
        public static bool IsDarkTheme { get; private set; } = true;

        public static event Action<bool>? ThemeChanged;

        public static void Initialize()
        {
            EmbeddedStorageService.Initialize();
            var pkg = EmbeddedStorageService.LoadPackage();
            SetTheme(pkg.IsDarkTheme, persist: false);
        }

        public static void ToggleTheme()
        {
            SetTheme(!IsDarkTheme, persist: true);
        }

        public static void SetTheme(bool isDark, bool persist = true)
        {
            IsDarkTheme = isDark;

            ThemeManager.Current.ApplicationTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

            ApplyPalette(isDark);

            if (persist)
            {
                try
                {
                    var pkg = EmbeddedStorageService.LoadPackage();
                    pkg.IsDarkTheme = isDark;
                    EmbeddedStorageService.SavePackage(pkg);
                }
                catch { }
            }

            ThemeChanged?.Invoke(isDark);
        }

        private static void ApplyPalette(bool isDark)
        {
            var res = Application.Current.Resources;

            if (isDark)
            {
                // OBSIDIAN / CARBON DARK PALETTE (Linear / Raycast / Geist Inspired - Zero AI Blue Slate)
                res["AppBackgroundBrush"] = CreateBrush("#09090B");
                res["SidebarBackgroundBrush"] = CreateBrush("#0C0C0E");
                res["SidebarBorderBrush"] = CreateBrush("#1C1C21");

                res["CardBackgroundBrush"] = CreateBrush("#111114");
                res["CardBorderBrush"] = CreateBrush("#222228");

                res["SubCardBackgroundBrush"] = CreateBrush("#16161A");
                res["SubCardBorderBrush"] = CreateBrush("#27272E");

                res["TextPrimaryBrush"] = CreateBrush("#FAFAFA");
                res["TextSecondaryBrush"] = CreateBrush("#A1A1AA");
                res["TextMutedBrush"] = CreateBrush("#71717A");

                // High-contrast monochrome primary CTA (Alabaster on Obsidian)
                res["PrimaryBtnBg"] = CreateBrush("#F4F4F5");
                res["PrimaryBtnHoverBg"] = CreateBrush("#FFFFFF");
                res["PrimaryBtnPressedBg"] = CreateBrush("#D4D4D8");
                res["PrimaryBtnText"] = CreateBrush("#09090B");

                res["PrimaryNavyBrush"] = CreateBrush("#FAFAFA");
                res["PrimaryNavyHoverBrush"] = CreateBrush("#E4E4E7");
                res["PrimaryNavyPressedBrush"] = CreateBrush("#D4D4D8");
                res["AccentBlueBrush"] = CreateBrush("#10B981");

                res["SecondaryActionBtnBg"] = CreateBrush("#18181C");
                res["SecondaryActionBtnHoverBg"] = CreateBrush("#232329");
                res["SecondaryActionBtnBorder"] = CreateBrush("#2C2C35");
                res["SecondaryActionBtnText"] = CreateBrush("#E4E4E7");

                res["TableActionBtnBg"] = CreateBrush("#16161A");
                res["TableActionBtnHoverBg"] = CreateBrush("#24242B");
                res["TableActionBtnBorder"] = CreateBrush("#2A2A32");
                res["TableActionBtnText"] = CreateBrush("#D4D4D8");

                res["SubTabBgBrush"] = CreateBrush("#0C0C0E");
                res["SubTabActiveBgBrush"] = CreateBrush("#232329");
                res["SubTabActiveTextBrush"] = CreateBrush("#FAFAFA");

                res["DataGridHeaderBg"] = CreateBrush("#0E0E11");
                res["DataGridHeaderBorder"] = CreateBrush("#222228");
                res["DataGridHeaderText"] = CreateBrush("#71717A");
                res["DataGridRowBg"] = CreateBrush("#111114");
                res["DataGridRowHoverBg"] = CreateBrush("#16161B");
                res["DataGridGridLines"] = CreateBrush("#1C1C22");

                res["StatusSuccessBg"] = CreateBrush("#052E16");
                res["StatusSuccessBorder"] = CreateBrush("#065F46");
                res["StatusSuccessText"] = CreateBrush("#34D399");

                res["StatusDangerBg"] = CreateBrush("#2A0A0E");
                res["StatusDangerBorder"] = CreateBrush("#7F1D1D");
                res["StatusDangerText"] = CreateBrush("#F87171");

                res["StatusWarningBg"] = CreateBrush("#291503");
                res["StatusWarningBorder"] = CreateBrush("#78350F");
                res["StatusWarningText"] = CreateBrush("#FBBF24");
            }
            else
            {
                // SWISS PAPER & INK LIGHT PALETTE
                res["AppBackgroundBrush"] = CreateBrush("#F4F4F5");
                res["SidebarBackgroundBrush"] = CreateBrush("#FAFAFA");
                res["SidebarBorderBrush"] = CreateBrush("#E4E4E7");

                res["CardBackgroundBrush"] = CreateBrush("#FFFFFF");
                res["CardBorderBrush"] = CreateBrush("#E4E4E7");

                res["SubCardBackgroundBrush"] = CreateBrush("#F8F8FA");
                res["SubCardBorderBrush"] = CreateBrush("#E4E4E7");

                res["TextPrimaryBrush"] = CreateBrush("#09090B");
                res["TextSecondaryBrush"] = CreateBrush("#52525B");
                res["TextMutedBrush"] = CreateBrush("#71717A");

                // Deep Carbon Ink Primary CTA on Light surface
                res["PrimaryBtnBg"] = CreateBrush("#18181B");
                res["PrimaryBtnHoverBg"] = CreateBrush("#27272A");
                res["PrimaryBtnPressedBg"] = CreateBrush("#09090B");
                res["PrimaryBtnText"] = CreateBrush("#FAFAFA");

                res["PrimaryNavyBrush"] = CreateBrush("#18181B");
                res["PrimaryNavyHoverBrush"] = CreateBrush("#27272A");
                res["PrimaryNavyPressedBrush"] = CreateBrush("#09090B");
                res["AccentBlueBrush"] = CreateBrush("#059669");

                res["SecondaryActionBtnBg"] = CreateBrush("#FFFFFF");
                res["SecondaryActionBtnHoverBg"] = CreateBrush("#F4F4F5");
                res["SecondaryActionBtnBorder"] = CreateBrush("#D4D4D8");
                res["SecondaryActionBtnText"] = CreateBrush("#18181B");

                res["TableActionBtnBg"] = CreateBrush("#FAFAFA");
                res["TableActionBtnHoverBg"] = CreateBrush("#E4E4E7");
                res["TableActionBtnBorder"] = CreateBrush("#D4D4D8");
                res["TableActionBtnText"] = CreateBrush("#18181B");

                res["SubTabBgBrush"] = CreateBrush("#F4F4F5");
                res["SubTabActiveBgBrush"] = CreateBrush("#18181B");
                res["SubTabActiveTextBrush"] = CreateBrush("#FFFFFF");

                res["DataGridHeaderBg"] = CreateBrush("#FAFAFA");
                res["DataGridHeaderBorder"] = CreateBrush("#E4E4E7");
                res["DataGridHeaderText"] = CreateBrush("#71717A");
                res["DataGridRowBg"] = CreateBrush("#FFFFFF");
                res["DataGridRowHoverBg"] = CreateBrush("#F8F8FA");
                res["DataGridGridLines"] = CreateBrush("#F4F4F5");

                res["StatusSuccessBg"] = CreateBrush("#ECFDF5");
                res["StatusSuccessBorder"] = CreateBrush("#A7F3D0");
                res["StatusSuccessText"] = CreateBrush("#065F46");

                res["StatusDangerBg"] = CreateBrush("#FEF2F2");
                res["StatusDangerBorder"] = CreateBrush("#FECACA");
                res["StatusDangerText"] = CreateBrush("#991B1B");

                res["StatusWarningBg"] = CreateBrush("#FFFBEB");
                res["StatusWarningBorder"] = CreateBrush("#FDE68A");
                res["StatusWarningText"] = CreateBrush("#92400E");
            }
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
            brush.Freeze();
            return brush;
        }
    }
}
