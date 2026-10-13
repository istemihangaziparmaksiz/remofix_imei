using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ImeiStudio.Services
{
    public class ImeiGeneratorService
    {
        public List<string> GenerateImeis(string prefix, int count)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "869000";

            // Sadece rakamları al
            prefix = Regex.Replace(prefix, @"\D", "");

            // En fazla 14 haneyi baz al (çünkü 15. hane Luhn ile hesaplanacak)
            if (prefix.Length > 14)
                prefix = prefix.Substring(0, 14);

            // 14 haneye tamamlamak için gerekirse sonuna 0 ekle
            string base14 = prefix.PadRight(14, '0');

            if (!long.TryParse(base14, out long current14))
            {
                current14 = 86900000000000L;
            }

            for (int i = 0; i < count; i++)
            {
                long next14Num = current14 + i;
                string next14Str = next14Num.ToString().PadLeft(14, '0');
                if (next14Str.Length > 14)
                    next14Str = next14Str.Substring(next14Str.Length - 14);

                // 15. haneyi Luhn algoritması ile hesapla
                int checkDigit = LuhnHelper.CalculateCheckDigit(next14Str);
                string full15 = next14Str + (checkDigit >= 0 ? checkDigit.ToString() : "0");
                results.Add(full15);
            }

            return results;
        }
    }
}
