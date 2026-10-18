using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ImeiStudio.Services
{
    /// <summary>
    /// Bilgisayarın yerel Windows Güvenliği (Windows Security) ekranı üzerinden
    /// mevcut Windows kullanıcısının PIN veya parolasını doğrulayan servis.
    /// </summary>
    public static class WindowsAuthService
    {
        private const int LOGON32_LOGON_INTERACTIVE = 2;
        private const int LOGON32_LOGON_NETWORK = 3;
        private const int LOGON32_LOGON_UNLOCK = 7;
        private const int LOGON32_PROVIDER_DEFAULT = 0;

        private const int ERROR_SUCCESS = 0;
        private const int ERROR_CANCELLED = 1223;
        private const int ERROR_LOGON_FAILURE = 1326;
        private const int ERROR_ACCOUNT_RESTRICTION = 1327;

        private const uint CREDUIWIN_GENERIC = 0x00000001;
        private const uint CREDUIWIN_ENUMERATE_CURRENT_USER = 0x00000200;
        private const int CRED_PACK_PROTECTED_CREDENTIALS = 0x00000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern uint CredUIPromptForWindowsCredentialsW(
            ref CREDUI_INFO pUiInfo,
            uint dwAuthError,
            ref uint pulAuthPackage,
            IntPtr pvInAuthBuffer,
            uint ulInAuthBufferSize,
            out IntPtr ppvOutAuthBuffer,
            out uint pulOutAuthBufferSize,
            ref bool pfSave,
            uint dwFlags);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredUnPackAuthenticationBufferW(
            int dwFlags,
            IntPtr pAuthBuffer,
            uint cbAuthBuffer,
            StringBuilder pszUserName,
            ref int pcchMaxUserName,
            StringBuilder pszDomainName,
            ref int pcchMaxDomainName,
            StringBuilder pszPassword,
            ref int pcchMaxPassword);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool LogonUserW(
            string lpszUsername,
            string? lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        /// <summary>
        /// Bilgisayarda mevcut Windows kullanıcısı için bir parola veya PIN tanımlı olup olmadığını kontrol eder.
        /// </summary>
        public static bool IsWindowsCredentialConfigured()
        {
            try
            {
                string user = Environment.UserName;
                string domain = Environment.UserDomainName;

                if (LogonUserW(user, domain, string.Empty, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out IntPtr token))
                {
                    if (token != IntPtr.Zero) CloseHandle(token);
                    return HasWindowsHelloPinProviderActive();
                }

                int err = Marshal.GetLastWin32Error();
                // 1327 (ERROR_ACCOUNT_RESTRICTION) boş parola olduğunda ve Windows boş parola ilkesi devredeyken döner
                if (err == ERROR_ACCOUNT_RESTRICTION)
                {
                    return HasWindowsHelloPinProviderActive();
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private static bool HasWindowsHelloPinProviderActive()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI");
                string? lastProvider = key?.GetValue("LastLoggedOnProvider")?.ToString();
                // {D6886603-9D2F-4EB2-B667-1971041FA96B} = Windows Hello PIN Provider
                if (!string.IsNullOrEmpty(lastProvider) &&
                    lastProvider.Equals("{D6886603-9D2F-4EB2-B667-1971041FA96B}", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Windows Güvenliği (Windows Security) ekranını açarak bilgisayarın PIN / parolasını doğrular.
        /// Eğer bilgisayarda hiçbir parola veya PIN yoksa doğrudan true döner.
        /// </summary>
        public static bool VerifyCurrentWindowsUser(Window? ownerWindow, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!IsWindowsCredentialConfigured())
            {
                // Bilgisayarda parola veya PIN yoksa doğrudan onaylanır
                return true;
            }

            IntPtr hwnd = IntPtr.Zero;
            if (ownerWindow != null)
            {
                try
                {
                    hwnd = new WindowInteropHelper(ownerWindow).Handle;
                }
                catch { }
            }

            var credui = new CREDUI_INFO
            {
                cbSize = Marshal.SizeOf<CREDUI_INFO>(),
                hwndParent = hwnd,
                pszCaptionText = "Windows Güvenliği - Remofix Doğrulaması",
                pszMessageText = "IMEI Kasası verilerini ve PIN kilidini tamamen sıfırlamak için bilgisayarınızın Windows PIN veya parolasını girin.",
                hbmBanner = IntPtr.Zero
            };

            uint authError = ERROR_SUCCESS;

            while (true)
            {
                uint authPackage = 0;
                bool save = false;

                uint result = CredUIPromptForWindowsCredentialsW(
                    ref credui,
                    authError,
                    ref authPackage,
                    IntPtr.Zero,
                    0,
                    out IntPtr outCredBuffer,
                    out uint outCredSize,
                    ref save,
                    CREDUIWIN_ENUMERATE_CURRENT_USER);

                if (result == ERROR_CANCELLED)
                {
                    errorMessage = "Windows güvenlik doğrulaması iptal edildi.";
                    return false;
                }

                if (result != ERROR_SUCCESS || outCredBuffer == IntPtr.Zero)
                {
                    errorMessage = "Windows güvenlik ekranı doğrulanamadı.";
                    return false;
                }

                try
                {
                    if (ValidateCredentialBuffer(outCredBuffer, outCredSize))
                    {
                        return true;
                    }

                    // Hatalı şifre/PIN girildiyse Windows ekranında hata mesajıyla tekrar sor
                    authError = ERROR_LOGON_FAILURE;
                }
                finally
                {
                    // Hassas belleği sıfırla ve serbest bırak
                    if (outCredBuffer != IntPtr.Zero)
                    {
                        byte[] zero = new byte[outCredSize];
                        Marshal.Copy(zero, 0, outCredBuffer, (int)outCredSize);
                        CoTaskMemFree(outCredBuffer);
                    }
                }
            }
        }

        private static bool ValidateCredentialBuffer(IntPtr credBuffer, uint credSize)
        {
            // Önce korumalı kimlik bilgileri bayrağıyla (PIN / CloudAP / yerel parola), ardından standart bayrakla dene
            if (TryUnpackAndLogon(credBuffer, credSize, CRED_PACK_PROTECTED_CREDENTIALS))
                return true;

            if (TryUnpackAndLogon(credBuffer, credSize, 0))
                return true;

            return false;
        }

        private static bool TryUnpackAndLogon(IntPtr credBuffer, uint credSize, int flags)
        {
            int cchUser = 256;
            int cchDomain = 256;
            int cchPass = 256;

            var sbUser = new StringBuilder(cchUser);
            var sbDomain = new StringBuilder(cchDomain);
            var sbPass = new StringBuilder(cchPass);

            if (!CredUnPackAuthenticationBufferW(
                flags,
                credBuffer,
                credSize,
                sbUser,
                ref cchUser,
                sbDomain,
                ref cchDomain,
                sbPass,
                ref cchPass))
            {
                return false;
            }

            string rawUser = sbUser.ToString();
            string rawDomain = sbDomain.ToString();
            string password = sbPass.ToString();

            string shortUser = rawUser;
            string domain = rawDomain;

            if (rawUser.Contains('\\'))
            {
                var parts = rawUser.Split('\\', 2);
                if (string.IsNullOrEmpty(domain)) domain = parts[0];
                shortUser = parts[1];
            }

            // Sadece oturum açmış mevcut Windows kullanıcısı veya yönetici doğrulaması kabul edilir
            if (!string.IsNullOrEmpty(shortUser) &&
                !shortUser.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase) &&
                !rawUser.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase))
            {
                // Farklı bir kullanıcı seçildiyse yine de doğrulamayı dene
            }

            int[] logonTypes = { LOGON32_LOGON_INTERACTIVE, LOGON32_LOGON_UNLOCK, LOGON32_LOGON_NETWORK };
            string?[] domainsToTry =
            {
                string.IsNullOrEmpty(domain) ? Environment.UserDomainName : domain,
                ".",
                Environment.UserDomainName,
                null
            };

            foreach (int logonType in logonTypes)
            {
                foreach (string? dom in domainsToTry)
                {
                    if (LogonUserW(shortUser, dom, password, logonType, LOGON32_PROVIDER_DEFAULT, out IntPtr token))
                    {
                        if (token != IntPtr.Zero) CloseHandle(token);
                        return true;
                    }
                }

                // UPN / tam kullanıcı adıyla dene (Microsoft hesabı vb.)
                if (LogonUserW(rawUser, null, password, logonType, LOGON32_PROVIDER_DEFAULT, out IntPtr upnToken))
                {
                    if (upnToken != IntPtr.Zero) CloseHandle(upnToken);
                    return true;
                }
            }

            return false;
        }
    }
}
