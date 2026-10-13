using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ModernWpf;

namespace ImeiStudio.Services
{
    public static class ThemeService
    {
        public static bool IsDarkTheme { get; private set; } = true; // Varsayılan olarak Koyu Tema

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

            // 1. ModernWpf Tema Modunu Değiştir
            ThemeManager.Current.ApplicationTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

            // 2. Uygulama Renk Paletini Dinamik Olarak Güncelle
            ApplyPalette(isDark);

            // 3. Tercihi Remofix.exe içerisine gömülü kaydet
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
                // KOYU TEMA (Modern, kurumsal koyu lacivert/arduvaz)
                res["AppBackgroundBrush"] = CreateBrush("#0B0F19");
                res["SidebarBackgroundBrush"] = CreateBrush("#111827");
                res["SidebarBorderBrush"] = CreateBrush("#1F2937");

                res["CardBackgroundBrush"] = CreateBrush("#1E293B");
                res["CardBorderBrush"] = CreateBrush("#334155");

                res["SubCardBackgroundBrush"] = CreateBrush("#0F172A");
                res["SubCardBorderBrush"] = CreateBrush("#334155");

                res["TextPrimaryBrush"] = CreateBrush("#F8FAFC");
                res["TextSecondaryBrush"] = CreateBrush("#94A3B8");
                res["TextMutedBrush"] = CreateBrush("#64748B");

                res["PrimaryNavyBrush"] = CreateBrush("#2563EB");
                res["PrimaryNavyHoverBrush"] = CreateBrush("#3B82F6");
                res["PrimaryNavyPressedBrush"] = CreateBrush("#1D4ED8");
                res["AccentBlueBrush"] = CreateBrush("#38BDF8");

                res["SecondaryActionBtnBg"] = CreateBrush("#0F172A");
                res["SecondaryActionBtnBorder"] = CreateBrush("#334155");
                res["SecondaryActionBtnText"] = CreateBrush("#F8FAFC");

                res["TableActionBtnBg"] = CreateBrush("#0F172A");
                res["TableActionBtnBorder"] = CreateBrush("#334155");
                res["TableActionBtnText"] = CreateBrush("#E2E8F0");

                res["SubTabBgBrush"] = CreateBrush("#0F172A");
                res["SubTabActiveBgBrush"] = CreateBrush("#2563EB");
                res["SubTabActiveTextBrush"] = CreateBrush("#FFFFFF");

                res["DataGridHeaderBg"] = CreateBrush("#0F172A");
                res["DataGridHeaderBorder"] = CreateBrush("#334155");
                res["DataGridHeaderText"] = CreateBrush("#94A3B8");
                res["DataGridRowBg"] = CreateBrush("#1E293B");
                res["DataGridGridLines"] = CreateBrush("#334155");

                res["StatusSuccessBg"] = CreateBrush("#064E3B");
                res["StatusSuccessBorder"] = CreateBrush("#059669");
                res["StatusSuccessText"] = CreateBrush("#34D399");

                res["StatusDangerBg"] = CreateBrush("#450A0A");
                res["StatusDangerBorder"] = CreateBrush("#DC2626");
                res["StatusDangerText"] = CreateBrush("#F87171");

                res["StatusWarningBg"] = CreateBrush("#451A03");
                res["StatusWarningBorder"] = CreateBrush("#D97706");
                res["StatusWarningText"] = CreateBrush("#FBBF24");
            }
            else
            {
                // AÇIK TEMA
                res["AppBackgroundBrush"] = CreateBrush("#F4F6F9");
                res["SidebarBackgroundBrush"] = CreateBrush("#FFFFFF");
                res["SidebarBorderBrush"] = CreateBrush("#E2E8F0");

                res["CardBackgroundBrush"] = CreateBrush("#FFFFFF");
                res["CardBorderBrush"] = CreateBrush("#E2E8F0");

                res["SubCardBackgroundBrush"] = CreateBrush("#F8FAFC");
                res["SubCardBorderBrush"] = CreateBrush("#E2E8F0");

                res["TextPrimaryBrush"] = CreateBrush("#0F172A");
                res["TextSecondaryBrush"] = CreateBrush("#475569");
                res["TextMutedBrush"] = CreateBrush("#64748B");

                res["PrimaryNavyBrush"] = CreateBrush("#1E3A8A");
                res["PrimaryNavyHoverBrush"] = CreateBrush("#1D4ED8");
                res["PrimaryNavyPressedBrush"] = CreateBrush("#172554");
                res["AccentBlueBrush"] = CreateBrush("#2563EB");

                res["SecondaryActionBtnBg"] = CreateBrush("#F8FAFC");
                res["SecondaryActionBtnBorder"] = CreateBrush("#CBD5E1");
                res["SecondaryActionBtnText"] = CreateBrush("#0F172A");

                res["TableActionBtnBg"] = CreateBrush("#F1F5F9");
                res["TableActionBtnBorder"] = CreateBrush("#CBD5E1");
                res["TableActionBtnText"] = CreateBrush("#0F172A");

                res["SubTabBgBrush"] = CreateBrush("#F1F5F9");
                res["SubTabActiveBgBrush"] = CreateBrush("#FFFFFF");
                res["SubTabActiveTextBrush"] = CreateBrush("#1E3A8A");

                res["DataGridHeaderBg"] = CreateBrush("#F8FAFC");
                res["DataGridHeaderBorder"] = CreateBrush("#E2E8F0");
                res["DataGridHeaderText"] = CreateBrush("#475569");
                res["DataGridRowBg"] = CreateBrush("#FFFFFF");
                res["DataGridGridLines"] = CreateBrush("#F1F5F9");

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
