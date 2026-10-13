using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ImeiStudio.Services;

namespace ImeiStudio.Converters
{
    public class CategoryToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string cat = (value?.ToString() ?? "").ToUpperInvariant();
            bool isDark = ThemeService.IsDarkTheme;

            if (isDark)
            {
                return cat switch
                {
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#064E3B")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#450A0A")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#451A03")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#450A0A")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#1F2937")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#1E293B")!
                };
            }
            else
            {
                return cat switch
                {
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#ECFDF5")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#FEF2F2")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#FFFBEB")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#FEF2F2")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#F3F4F6")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#EFF6FF")!
                };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class CategoryToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string cat = (value?.ToString() ?? "").ToUpperInvariant();
            bool isDark = ThemeService.IsDarkTheme;

            if (isDark)
            {
                return cat switch
                {
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#34D399")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#F87171")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#FBBF24")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#F87171")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#94A3B8")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#38BDF8")!
                };
            }
            else
            {
                return cat switch
                {
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#065F46")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#991B1B")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#92400E")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#991B1B")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#374151")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#1E40AF")!
                };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class CategoryToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string cat = (value?.ToString() ?? "").ToUpperInvariant();
            bool isDark = ThemeService.IsDarkTheme;

            if (isDark)
            {
                return cat switch
                {
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#059669")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#DC2626")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#D97706")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#DC2626")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#374151")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#2563EB")!
                };
            }
            else
            {
                return cat switch
                {
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#A7F3D0")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#FECACA")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#FDE68A")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#FECACA")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#E5E7EB")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#BFDBFE")!
                };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
