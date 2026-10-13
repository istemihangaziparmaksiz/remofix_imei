using System;
using System.Text.RegularExpressions;

namespace ImeiStudio.Services
{
    public static class LuhnHelper
    {
        public static bool IsValidImei(string imei)
        {
            if (string.IsNullOrWhiteSpace(imei) || imei.Length != 15 || !Regex.IsMatch(imei, @"^\d{15}$"))
                return false;

            int sum = 0;
            for (int i = 0; i < 14; i++)
            {
                int digit = imei[i] - '0';
                if (i % 2 != 0) // çift indeks (1, 3, 5...)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit = (digit % 10) + (digit / 10);
                }
                sum += digit;
            }

            int checkDigit = (10 - (sum % 10)) % 10;
            return checkDigit == (imei[14] - '0');
        }

        public static int CalculateCheckDigit(string imei14)
        {
            if (string.IsNullOrWhiteSpace(imei14) || imei14.Length != 14 || !Regex.IsMatch(imei14, @"^\d{14}$"))
                return -1;

            int sum = 0;
            for (int i = 0; i < 14; i++)
            {
                int digit = imei14[i] - '0';
                if (i % 2 != 0)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit = (digit % 10) + (digit / 10);
                }
                sum += digit;
            }

            return (10 - (sum % 10)) % 10;
        }

        public static string CompleteToValidImei(string imei14)
        {
            int checkDigit = CalculateCheckDigit(imei14);
            return checkDigit >= 0 ? imei14 + checkDigit : string.Empty;
        }
    }
}
