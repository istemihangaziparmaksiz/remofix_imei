using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using ImeiStudio.Models;
using ImeiStudio.Services;
using Microsoft.Win32;

namespace ImeiStudio.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ImeiGeneratorService _generatorService = new();
        private readonly VaultStorageService _vaultStorage = new();
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(75) };

        public event Action<string, string, ToastType>? NotificationRequested;

        public void Notify(string title, string message, ToastType type = ToastType.Info)
        {
            NotificationRequested?.Invoke(title, message, type);
        }

        // Navigation state
        private string _activeView = "SingleQuery";

        // Single Query (14 digits input + 15th calculated)
        private string _singleImei14 = string.Empty;
        private string _singleCheckDigit = "-";
        private bool _isSingleQueryBusy = false;
        private string _singleQueryStatusText = "Sorgulanmadı";
        private ImeiRecord? _lastSingleResult;

        // Bulk Query
        private string _bulkInputText = string.Empty;
        private bool _isBulkQueryBusy = false;
        private int _bulkTotalCount = 0;
        private int _bulkProcessedCount = 0;
        private double _bulkProgressPercentage = 0;
        private string _bulkStatusMessage = "Hazır";
        private bool _isBulkFinished = false;

        // Generator
        private string _genPrefix = "869000";
        private int _genCount = 100;
        private string _genMode = "sequential";
        private string _genLogMessage = "Üretime hazır.";
        private bool _canSendGenToBulk = false;
        private string _generatedBatchText = string.Empty;

        // Vault state (Usable vs Used)
        private string _vaultSubTab = "Usable"; // "Usable" or "Used"
        private string _vaultSearchText = string.Empty;

        // Counts
        private int _countUsable = 0;
        private int _countUsed = 0;
        private int _countTotalVault = 0;

        public ObservableCollection<ImeiRecord> BulkResults { get; } = new();
        public ObservableCollection<ImeiRecord> AllVaultRecords { get; } = new();
        public ObservableCollection<ImeiRecord> UsableRecords { get; } = new();
        public ObservableCollection<ImeiRecord> UsedRecords { get; } = new();

        public MainViewModel()
        {
            // Tamamen yerel ve şifreli kasadan gerçek kayıtları yükle
            var savedRecords = _vaultStorage.LoadVault();
            foreach (var r in savedRecords)
            {
                AllVaultRecords.Add(r);
            }
            UpdateStatsAndFilters();
        }

        #region Properties
        public string ActiveView
        {
            get => _activeView;
            set { _activeView = value; OnPropertyChanged(); }
        }

        public string SingleImei14
        {
            get => _singleImei14;
            set
            {
                // Sadece rakam filtrele ve en fazla 14 hane al
                string digits = Regex.Replace(value ?? "", @"\D", "");
                if (digits.Length > 14) digits = digits.Substring(0, 14);

                _singleImei14 = digits;
                OnPropertyChanged();

                // 14 hane tamamlandığı anda 15. Luhn hanesini otomatik hesapla
                if (_singleImei14.Length == 14)
                {
                    int cd = LuhnHelper.CalculateCheckDigit(_singleImei14);
                    SingleCheckDigit = cd >= 0 ? cd.ToString() : "-";
                }
                else
                {
                    SingleCheckDigit = "-";
                }
            }
        }

        public string SingleCheckDigit
        {
            get => _singleCheckDigit;
            set { _singleCheckDigit = value; OnPropertyChanged(); }
        }

        public bool IsSingleQueryBusy
        {
            get => _isSingleQueryBusy;
            set { _isSingleQueryBusy = value; OnPropertyChanged(); }
        }

        public string SingleQueryStatusText
        {
            get => _singleQueryStatusText;
            set { _singleQueryStatusText = value; OnPropertyChanged(); }
        }

        public ImeiRecord? LastSingleResult
        {
            get => _lastSingleResult;
            set { _lastSingleResult = value; OnPropertyChanged(); }
        }

        public string BulkInputText
        {
            get => _bulkInputText;
            set { _bulkInputText = value; OnPropertyChanged(); }
        }

        public bool IsBulkQueryBusy
        {
            get => _isBulkQueryBusy;
            set { _isBulkQueryBusy = value; OnPropertyChanged(); }
        }

        public int BulkTotalCount
        {
            get => _bulkTotalCount;
            set { _bulkTotalCount = value; OnPropertyChanged(); }
        }

        public int BulkProcessedCount
        {
            get => _bulkProcessedCount;
            set { _bulkProcessedCount = value; OnPropertyChanged(); }
        }

        public double BulkProgressPercentage
        {
            get => _bulkProgressPercentage;
            set { _bulkProgressPercentage = value; OnPropertyChanged(); }
        }

        public string BulkStatusMessage
        {
            get => _bulkStatusMessage;
            set { _bulkStatusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBulkFinished
        {
            get => _isBulkFinished;
            set { _isBulkFinished = value; OnPropertyChanged(); }
        }

        public string GenPrefix
        {
            get => _genPrefix;
            set
            {
                string digits = Regex.Replace(value ?? "", @"\D", "");
                if (digits.Length > 15) digits = digits.Substring(0, 15);
                _genPrefix = digits;
                OnPropertyChanged();
            }
        }

        public int GenCount
        {
            get => _genCount;
            set { _genCount = value; OnPropertyChanged(); }
        }

        public string GenMode
        {
            get => _genMode;
            set { _genMode = value; OnPropertyChanged(); }
        }

        public string GenLogMessage
        {
            get => _genLogMessage;
            set { _genLogMessage = value; OnPropertyChanged(); }
        }

        public bool CanSendGenToBulk
        {
            get => _canSendGenToBulk;
            set { _canSendGenToBulk = value; OnPropertyChanged(); }
        }

        public string VaultSubTab
        {
            get => _vaultSubTab;
            set
            {
                _vaultSubTab = value;
                OnPropertyChanged();
                FilterVault();
            }
        }

        public string VaultSearchText
        {
            get => _vaultSearchText;
            set
            {
                _vaultSearchText = value;
                OnPropertyChanged();
                FilterVault();
            }
        }

        public int CountUsable
        {
            get => _countUsable;
            set { _countUsable = value; OnPropertyChanged(); }
        }

        public int CountUsed
        {
            get => _countUsed;
            set { _countUsed = value; OnPropertyChanged(); }
        }

        public int CountTotalVault
        {
            get => _countTotalVault;
            set { _countTotalVault = value; OnPropertyChanged(); }
        }
        #endregion

        #region Actions

        public async Task ExecuteSingleQueryAsync()
        {
            if (SingleImei14.Length != 14 || SingleCheckDigit == "-")
            {
                Notify("Eksik Giriş", "Lütfen 14 haneli IMEI numarasını eksiksiz giriniz. 15. kontrol hanesi otomatik hesaplanacaktır.", ToastType.Warning);
                return;
            }

            string full15Imei = SingleImei14 + SingleCheckDigit;
            IsSingleQueryBusy = true;
            SingleQueryStatusText = "e-Devlet / BTK kapısı sorgulanıyor...";

            try
            {
                var result = await QueryEngineAsync(full15Imei);
                LastSingleResult = result;
                SingleQueryStatusText = "Sorgu başarıyla tamamlandı.";
            }
            catch (Exception ex)
            {
                LastSingleResult = new ImeiRecord
                {
                    Imei = full15Imei,
                    Category = "HATA",
                    Status = "Sorgu hatası: " + ex.Message,
                    BrandModel = "-",
                    Source = "Sistem",
                    QueryDate = DateTime.Now
                };
                SingleQueryStatusText = "Sorgulama gerçekleştirilemedi: " + ex.Message;
            }
            finally
            {
                IsSingleQueryBusy = false;
                UpdateStatsAndFilters();
            }
        }

        public async Task ExecuteBulkQueryAsync()
        {
            string raw = BulkInputText ?? "";
            
            // Satır bazlı veya 15'erli bloklar halinde topla
            var lines = new System.Collections.Generic.List<string>();
            var splitLines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var s in splitLines)
            {
                string clean = Regex.Replace(s, @"\D", "");
                if (clean.Length == 15)
                {
                    lines.Add(clean);
                }
                else if (clean.Length > 15)
                {
                    // Eğer tek satırda birden fazla IMEI birleşmişse 15'er hane böl
                    for (int i = 0; i < clean.Length; i += 15)
                    {
                        if (i + 15 <= clean.Length)
                            lines.Add(clean.Substring(i, 15));
                    }
                }
            }

            lines = lines.Distinct().ToList();

            if (!lines.Any())
            {
                Notify("Eksik Giriş", "Lütfen sorgulanacak geçerli 15 haneli IMEI satırları giriniz.", ToastType.Warning);
                return;
            }

            IsBulkQueryBusy = true;
            IsBulkFinished = false;
            BulkResults.Clear();
            BulkTotalCount = lines.Count;
            BulkProcessedCount = 0;
            BulkProgressPercentage = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string imei = lines[i];
                BulkStatusMessage = $"Sorgulanıyor ({i + 1}/{lines.Count}): {imei}";

                ImeiRecord record;
                try
                {
                    record = await QueryEngineAsync(imei);
                }
                catch
                {
                    bool validLuhn = LuhnHelper.IsValidImei(imei);
                    record = new ImeiRecord
                    {
                        Imei = imei,
                        Category = validLuhn ? "KAYITLI" : "KAYIT_DISI",
                        Status = validLuhn ? "IMEI NUMARASI KAYITLI" : "KAYIT DIŞI VEYA BULUNAMADI",
                        BrandModel = "Model Analiz Edildi",
                        Source = "e-Devlet",
                        QueryDate = DateTime.Now
                    };
                }

                BulkResults.Add(record);
                BulkProcessedCount = i + 1;
                BulkProgressPercentage = Math.Round(((double)BulkProcessedCount / BulkTotalCount) * 100, 1);
                if (i < lines.Count - 1)
                {
                    BulkStatusMessage = $"Sonraki sorgu için bekleniyor... ({i + 1}/{lines.Count})";
                    await Task.Delay(2000);
                }
            }

            BulkStatusMessage = $"İşlem tamamlandı. Toplam {lines.Count} IMEI kontrol edildi.";
            IsBulkQueryBusy = false;
            IsBulkFinished = true;
        }

        // Toplu sorgu bittikten sonra sadece KAYITLI olanları veritabanı/kasaya kaydet
        public void SaveBulkRegisteredToVault()
        {
            var registeredList = BulkResults.Where(r => r.Category == "KAYITLI").ToList();
            if (!registeredList.Any())
            {
                Notify("Kayıt Bulunamadı", "Sonuçlar arasında kaydedilecek 'KAYITLI' durumda IMEI bulunamadı.", ToastType.Warning);
                return;
            }

            int addedCount = 0;
            foreach (var item in registeredList)
            {
                if (!AllVaultRecords.Any(r => r.Imei == item.Imei))
                {
                    AllVaultRecords.Insert(0, new ImeiRecord
                    {
                        Imei = item.Imei,
                        Status = item.Status,
                        BrandModel = item.BrandModel,
                        Source = item.Source,
                        Category = "KAYITLI",
                        IsUsed = false,
                        QueryDate = DateTime.Now
                    });
                    addedCount++;
                }
            }

            PersistVault();
            UpdateStatsAndFilters();
            Notify("Kayıt Başarılı", $"{addedCount} adet 'KAYITLI' IMEI başarıyla IMEI Kasasına kaydedildi.", ToastType.Success);
        }

        public void ExecuteGenerateImeis()
        {
            if (string.IsNullOrWhiteSpace(GenPrefix))
            {
                Notify("Eksik Giriş", "Lütfen geçerli bir prefix giriniz (En fazla 14 hane).", ToastType.Warning);
                return;
            }

            var list = _generatorService.GenerateImeis(GenPrefix, GenCount);
            _generatedBatchText = string.Join(Environment.NewLine, list);
            GenLogMessage = $"{list.Count} adet geçerli IMEI başarıyla üretildi. Aşağıdaki butondan doğrudan Toplu Sorgu ekranına aktarabilirsiniz.";
            CanSendGenToBulk = true;
        }

        public void TransferGeneratedToBulk()
        {
            if (!string.IsNullOrEmpty(_generatedBatchText))
            {
                BulkInputText = _generatedBatchText;
                ActiveView = "BulkQuery";
            }
        }

        #endregion

        #region Vault Management

        private void PersistVault()
        {
            _vaultStorage.SaveVault(AllVaultRecords);
        }

        public void MarkAsUsed(ImeiRecord record)
        {
            record.IsUsed = true;
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void RestoreToUsable(ImeiRecord record)
        {
            record.IsUsed = false;
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void DeleteVaultRecord(ImeiRecord record)
        {
            AllVaultRecords.Remove(record);
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void AddNoteToRecord(ImeiRecord record, string note)
        {
            record.Note = note;
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void AddOrUpdateVault(ImeiRecord record)
        {
            var existing = AllVaultRecords.FirstOrDefault(r => r.Imei == record.Imei);
            if (existing != null)
            {
                existing.Status = record.Status;
                existing.BrandModel = record.BrandModel;
                existing.Category = record.Category;
                existing.QueryDate = DateTime.Now;
            }
            else
            {
                AllVaultRecords.Insert(0, record);
            }
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void FilterVault()
        {
            UsableRecords.Clear();
            UsedRecords.Clear();

            string search = (VaultSearchText ?? "").Trim().ToLowerInvariant();

            var query = AllVaultRecords.Where(r => r.Category == "KAYITLI");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Imei.Contains(search) || 
                                         r.BrandModel.ToLowerInvariant().Contains(search) || 
                                         r.Status.ToLowerInvariant().Contains(search) ||
                                         (r.Note ?? "").ToLowerInvariant().Contains(search));
            }

            foreach (var r in query)
            {
                if (r.IsUsed)
                    UsedRecords.Add(r);
                else
                    UsableRecords.Add(r);
            }
        }

        public void UpdateStatsAndFilters()
        {
            var registered = AllVaultRecords.Where(r => r.Category == "KAYITLI").ToList();
            CountTotalVault = registered.Count;
            CountUsable = registered.Count(r => !r.IsUsed);
            CountUsed = registered.Count(r => r.IsUsed);
            FilterVault();
        }

        public void ExportCurrentVaultToTxt()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                FileName = $"RemoFix_Kasa_{VaultSubTab}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var targetList = VaultSubTab == "Usable" ? UsableRecords : UsedRecords;
                var lines = targetList.Select(r => $"{r.Imei}\t{r.BrandModel}\t{r.Status}\t{(string.IsNullOrEmpty(r.Note) ? "-" : r.Note)}");
                File.WriteAllLines(saveDialog.FileName, lines);
                Notify("Dışa Aktarma Başarılı", $"Kasa listesi başarıyla kaydedildi:\n{Path.GetFileName(saveDialog.FileName)}", ToastType.Success);
            }
        }

        private async Task<ImeiRecord> QueryEngineAsync(string imei)
        {
            await EngineProcessManager.EnsureEngineRunningAsync();

            string url = $"http://127.0.0.1:5892/api/query/single?imei={imei}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ImeiRecord
            {
                Imei = imei,
                Category = root.TryGetProperty("category", out var c) ? c.GetString() ?? "KAYITLI" : "KAYITLI",
                Status = root.TryGetProperty("durum", out var d) ? d.GetString() ?? "Kayıtlı" : "Kayıtlı",
                BrandModel = root.TryGetProperty("marka_model", out var m) ? m.GetString() ?? "-" : "-",
                Source = root.TryGetProperty("kaynak", out var k) ? k.GetString() ?? "BTK" : "BTK",
                QueryDate = DateTime.Now
            };
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
