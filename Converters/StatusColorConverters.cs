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
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#052E16")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#2A0A0E")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#291503")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#2A0A0E")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#18181C")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#16161A")!
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
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#F4F4F5")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#F4F4F5")!
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
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#A1A1AA")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#E4E4E7")!
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
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#52525B")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#18181B")!
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
                    "KAYITLI" => (Brush)new BrushConverter().ConvertFrom("#065F46")!,
                    "KAYIT_DISI" or "KAYITSIZ" => (Brush)new BrushConverter().ConvertFrom("#7F1D1D")!,
                    "KOPYALANMIS" or "KLON" => (Brush)new BrushConverter().ConvertFrom("#78350F")!,
                    "SAVCILIK_KAPATILMIS" or "CALINTI" => (Brush)new BrushConverter().ConvertFrom("#7F1D1D")!,
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#27272A")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#27272E")!
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
                    "KULLANILMAYAN_KAPATILMIS" => (Brush)new BrushConverter().ConvertFrom("#E4E4E7")!,
                    _ => (Brush)new BrushConverter().ConvertFrom("#E4E4E7")!
                };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
