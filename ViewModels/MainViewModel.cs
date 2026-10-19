using System;
using System.Collections.Generic;
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

        // Bulk Query & Auto-Stop & Pause/Cancel
        private string _bulkInputText = string.Empty;
        private bool _isBulkQueryBusy = false;
        private bool _isBulkPaused = false;
        private bool _isBulkCancelled = false;
        private CancellationTokenSource? _bulkCts;
        private bool _bulkAutoStopEnabled = false;
        private int _bulkAutoStopTarget = 5;
        private int _bulkTotalCount = 0;
        private int _bulkProcessedCount = 0;
        private double _bulkProgressPercentage = 0;
        private string _bulkStatusMessage = "Hazır";
        private bool _isBulkFinished = false;

        // Generator
        private string _genPrefix = "86945206";
        private int _genCount = 100;
        private string _genMode = "sequential";
        private string _genLogMessage = "Üretime hazır.";
        private bool _canSendGenToBulk = false;
        private string _generatedBatchText = string.Empty;
        private string _newTacName = string.Empty;

        // Vault state (Usable vs Used) & Check Pause/Cancel
        private string _vaultSubTab = "Usable"; // "Usable" or "Used"
        private string _vaultSearchText = string.Empty;
        private bool _isVaultCheckBusy = false;
        private bool _isVaultCheckPaused = false;
        private bool _isVaultCheckCancelled = false;
        private CancellationTokenSource? _vaultCheckCts;
        private bool _isVaultCheckBarVisible = false;
        private double _vaultCheckProgressPercentage = 0;
        private string _vaultCheckStatusMessage = "Hazır";

        // Counts
        private int _countUsable = 0;
        private int _countUsed = 0;
        private int _countTotalVault = 0;

        public ObservableCollection<ImeiRecord> BulkResults { get; } = new();
        public ObservableCollection<ImeiRecord> AllVaultRecords { get; } = new();
        public ObservableCollection<ImeiRecord> UsableRecords { get; } = new();
        public ObservableCollection<ImeiRecord> UsedRecords { get; } = new();
        public ObservableCollection<TacPreset> TacPresets { get; } = new();

        public MainViewModel()
        {
            var savedRecords = _vaultStorage.LoadVault();
            foreach (var r in savedRecords)
            {
                AllVaultRecords.Add(r);
            }

            LoadTacPresets();
            UpdateStatsAndFilters();
        }

        private void LoadTacPresets()
        {
            var pkg = EmbeddedStorageService.LoadPackage();
            if (pkg.TacPresets != null && pkg.TacPresets.Count > 0)
            {
                foreach (var t in pkg.TacPresets)
                {
                    TacPresets.Add(t);
                }
            }
            else
            {
                var defaults = new List<TacPreset>
                {
                    new TacPreset { Name = "Redmi Note 12 Pro", Prefix = "86945206" },
                    new TacPreset { Name = "Redmi Note 11", Prefix = "86622806" },
                    new TacPreset { Name = "Samsung Galaxy S23 Ultra", Prefix = "35419609" },
                    new TacPreset { Name = "Samsung Galaxy A54 5G", Prefix = "35096348" },
                    new TacPreset { Name = "iPhone 14 Pro Max", Prefix = "35678911" },
                    new TacPreset { Name = "Nokia 2G Tuşlu Seri", Prefix = "35824005" }
                };
                foreach (var d in defaults) TacPresets.Add(d);
            }
        }

        private void SaveTacPresets()
        {
            var pkg = EmbeddedStorageService.LoadPackage();
            pkg.TacPresets = TacPresets.ToList();
            EmbeddedStorageService.SavePackage(pkg);
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
                string digits = Regex.Replace(value ?? "", @"\D", "");
                if (digits.Length > 14) digits = digits.Substring(0, 14);

                _singleImei14 = digits;
                OnPropertyChanged();

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

        public bool IsBulkPaused
        {
            get => _isBulkPaused;
            set
            {
                _isBulkPaused = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BulkPauseButtonText));
            }
        }

        public string BulkPauseButtonText => _isBulkPaused ? "Devam Et" : "Duraklat";

        public bool BulkAutoStopEnabled
        {
            get => _bulkAutoStopEnabled;
            set { _bulkAutoStopEnabled = value; OnPropertyChanged(); }
        }

        public int BulkAutoStopTarget
        {
            get => _bulkAutoStopTarget;
            set { _bulkAutoStopTarget = value > 0 ? value : 1; OnPropertyChanged(); }
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

        public string NewTacName
        {
            get => _newTacName;
            set { _newTacName = value; OnPropertyChanged(); }
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
                OnPropertyChanged(nameof(CanCheckVault));
                FilterVault();
                RefreshSelectionStats();
            }
        }

        public bool IsVaultCheckBusy
        {
            get => _isVaultCheckBusy;
            set
            {
                _isVaultCheckBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCheckVault));
            }
        }

        public bool IsVaultCheckPaused
        {
            get => _isVaultCheckPaused;
            set
            {
                _isVaultCheckPaused = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VaultCheckPauseButtonText));
            }
        }

        public string VaultCheckPauseButtonText => _isVaultCheckPaused ? "Devam Et" : "Duraklat";

        public bool CanCheckVault => !_isVaultCheckBusy;

        public bool IsVaultCheckBarVisible
        {
            get => _isVaultCheckBarVisible;
            set { _isVaultCheckBarVisible = value; OnPropertyChanged(); }
        }

        public double VaultCheckProgressPercentage
        {
            get => _vaultCheckProgressPercentage;
            set { _vaultCheckProgressPercentage = value; OnPropertyChanged(); }
        }

        public string VaultCheckStatusMessage
        {
            get => _vaultCheckStatusMessage;
            set { _vaultCheckStatusMessage = value; OnPropertyChanged(); }
        }

        public string VaultSearchText
        {
            get => _vaultSearchText;
            set
            {
                _vaultSearchText = value;
                OnPropertyChanged();
                FilterVault();
                RefreshSelectionStats();
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

        private ObservableCollection<ImeiRecord> CurrentTabCollection => _vaultSubTab == "Usable" ? UsableRecords : UsedRecords;

        public int SelectedVaultCount => CurrentTabCollection.Count(r => r.IsSelected);

        public bool HasSelectedVaultItems => SelectedVaultCount > 0;

        public bool CanPairDualSim => _vaultSubTab == "Usable" && UsableRecords.Count(r => r.IsSelected) == 2;

        public string CheckVaultButtonText => SelectedVaultCount > 0
            ? $"Seçilileri Kontrol Et ({SelectedVaultCount})"
            : "Kontrol Et";

        public void RefreshSelectionStats()
        {
            OnPropertyChanged(nameof(SelectedVaultCount));
            OnPropertyChanged(nameof(HasSelectedVaultItems));
            OnPropertyChanged(nameof(CanPairDualSim));
            OnPropertyChanged(nameof(CheckVaultButtonText));
        }

        #endregion

        #region Actions

        private async Task WaitWithPauseAndCancelAsync(int delayMs, Func<bool> isPaused, Func<bool> isCancelled)
        {
            int elapsed = 0;
            while (elapsed < delayMs)
            {
                if (isCancelled()) return;
                while (isPaused() && !isCancelled())
                {
                    await Task.Delay(150);
                }
                if (isCancelled()) return;
                await Task.Delay(100);
                elapsed += 100;
            }
        }

        public void ToggleBulkPause()
        {
            if (!IsBulkQueryBusy) return;
            IsBulkPaused = !IsBulkPaused;
            BulkStatusMessage = IsBulkPaused
                ? $"⏸ Sorgu duraklatıldı ({BulkProcessedCount}/{BulkTotalCount}). Devam etmek için tıklayın."
                : $"▶ Sorgu devam ediyor ({BulkProcessedCount}/{BulkTotalCount})...";
        }

        public void StopBulkQuery()
        {
            if (!IsBulkQueryBusy) return;
            _isBulkCancelled = true;
            IsBulkPaused = false;
            try { _bulkCts?.Cancel(); } catch { }
            BulkProgressPercentage = 0;
            BulkStatusMessage = $"Sorgu durduruldu ({BulkProcessedCount}/{BulkTotalCount} IMEI kontrol edildi).";
        }

        public void ToggleVaultCheckPause()
        {
            if (!IsVaultCheckBusy) return;
            IsVaultCheckPaused = !IsVaultCheckPaused;
            VaultCheckStatusMessage = IsVaultCheckPaused
                ? "⏸ Kasa kontrolü duraklatıldı. Devam etmek için tıklayın."
                : "▶ Kasa kontrolü devam ediyor...";
        }

        public void StopVaultCheck()
        {
            if (!IsVaultCheckBusy) return;
            _isVaultCheckCancelled = true;
            IsVaultCheckPaused = false;
            IsVaultCheckBarVisible = false;
            VaultCheckProgressPercentage = 0;
            try { _vaultCheckCts?.Cancel(); } catch { }
        }

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

            var lines = new List<string>();
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

            _bulkCts?.Dispose();
            _bulkCts = new CancellationTokenSource();

            IsBulkQueryBusy = true;
            IsBulkPaused = false;
            _isBulkCancelled = false;
            IsBulkFinished = false;
            BulkResults.Clear();
            BulkTotalCount = lines.Count;
            BulkProcessedCount = 0;
            BulkProgressPercentage = 0;

            bool stoppedByAutoTarget = false;
            var semaphore = new SemaphoreSlim(3, 3);
            var queueLock = new object();
            int nextIndex = 0;
            var activeTasks = new List<Task>();

            while (nextIndex < lines.Count && !_isBulkCancelled && !stoppedByAutoTarget)
            {
                while (IsBulkPaused && !_isBulkCancelled && !stoppedByAutoTarget)
                {
                    await Task.Delay(150);
                }
                if (_isBulkCancelled || stoppedByAutoTarget) break;

                try
                {
                    await semaphore.WaitAsync(_bulkCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (_isBulkCancelled || stoppedByAutoTarget)
                {
                    semaphore.Release();
                    break;
                }

                int currentIndex;
                lock (queueLock)
                {
                    currentIndex = nextIndex++;
                }

                if (currentIndex >= lines.Count)
                {
                    semaphore.Release();
                    break;
                }

                string currentImei = lines[currentIndex];

                var task = Task.Run(async () =>
                {
                    try
                    {
                        if (_isBulkCancelled || stoppedByAutoTarget) return;

                        ImeiRecord record;
                        try
                        {
                            record = await QueryEngineAsync(currentImei, _bulkCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            if (_isBulkCancelled) return;
                            record = new ImeiRecord
                            {
                                Imei = currentImei,
                                Category = "HATA",
                                Status = "Sorgu hatası: " + ex.Message,
                                BrandModel = "-",
                                Source = "e-Devlet",
                                QueryDate = DateTime.Now
                            };
                        }

                        if (_isBulkCancelled || stoppedByAutoTarget) return;

                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (_isBulkCancelled || stoppedByAutoTarget) return;

                            BulkResults.Add(record);
                            BulkProcessedCount++;
                            BulkProgressPercentage = Math.Round(((double)BulkProcessedCount / BulkTotalCount) * 100, 1);
                            BulkStatusMessage = $"Sorgulanıyor ({BulkProcessedCount}/{BulkTotalCount}): {record.Imei} -> {record.Category}";

                            // Hedef Bulunca Otomatik Durdurma Kontrolü
                            if (BulkAutoStopEnabled && BulkAutoStopTarget > 0)
                            {
                                int regCount = BulkResults.Count(r => r.Category == "KAYITLI");
                                if (regCount >= BulkAutoStopTarget)
                                {
                                    stoppedByAutoTarget = true;
                                    try { _bulkCts?.Cancel(); } catch { }
                                }
                            }
                        });

                        await Task.Delay(500);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                activeTasks.Add(task);
            }

            try
            {
                await Task.WhenAll(activeTasks);
            }
            catch { }

            IsBulkQueryBusy = false;
            IsBulkPaused = false;
            IsBulkFinished = BulkResults.Any();

            if (stoppedByAutoTarget)
            {
                SaveBulkRegisteredToVault();
                int regCount = BulkResults.Count(r => r.Category == "KAYITLI");
                BulkStatusMessage = $"Hedeflenen {regCount} adet KAYITLI IMEI bulundu, sorgu otomatik durduruldu ve kasaya kaydedildi.";
            }
            else if (_isBulkCancelled)
            {
                BulkProgressPercentage = 0;
                BulkStatusMessage = $"Sorgu durduruldu. {BulkProcessedCount}/{lines.Count} IMEI kontrol edildi.";
                Notify("Sorgu Durduruldu", $"Toplu sorgulama {BulkProcessedCount}. kayıtta durduruldu.", ToastType.Info);
            }
            else
            {
                BulkStatusMessage = $"İşlem tamamlandı. Toplam {lines.Count} IMEI kontrol edildi.";
            }
        }

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

        public void AddCurrentTacToPresets()
        {
            string name = (NewTacName ?? "").Trim();
            string prefix = Regex.Replace(GenPrefix ?? "", @"\D", "");

            if (string.IsNullOrEmpty(name))
            {
                Notify("Model Adı Gerekli", "TAC rehberine kaydetmek için lütfen bir model adı yazın (Örn: Redmi Note 11).", ToastType.Warning);
                return;
            }

            if (prefix.Length < 6)
            {
                Notify("Geçersiz TAC Prefix", "Lütfen en az 6-8 haneli geçerli bir TAC numarası girin.", ToastType.Warning);
                return;
            }

            var existing = TacPresets.FirstOrDefault(t => t.Prefix == prefix || string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Name = name;
                existing.Prefix = prefix;
            }
            else
            {
                TacPresets.Insert(0, new TacPreset { Name = name, Prefix = prefix });
            }

            NewTacName = string.Empty;
            SaveTacPresets();
            Notify("TAC Rehberine Eklendi", $"{name} ({prefix}) rehbere kaydedildi.", ToastType.Success);
        }

        public void RemoveTacPreset(TacPreset preset)
        {
            if (preset == null) return;
            TacPresets.Remove(preset);
            SaveTacPresets();
            Notify("Rehberden Silindi", $"{preset.Name} TAC rehberinden kaldırıldı.", ToastType.Info);
        }

        #endregion

        #region Vault Management

        private void PersistVault()
        {
            _vaultStorage.SaveVault(AllVaultRecords);
        }

        public void MarkAsUsedDetailed(ImeiRecord record, string customer, string targetDevice, string note)
        {
            record.IsUsed = true;
            record.IsSelected = false;
            record.CustomerInfo = customer;
            record.TargetDevice = targetDevice;
            record.Note = note;
            record.UsedDate = DateTime.Now;
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void MarkDualSimAsUsed(ImeiRecord sim1, ImeiRecord sim2, string customer, string targetDevice, string note)
        {
            DateTime now = DateTime.Now;

            sim1.IsUsed = true;
            sim1.IsSelected = false;
            sim1.PairedImei = sim2.Imei;
            sim1.CustomerInfo = customer;
            sim1.TargetDevice = targetDevice;
            sim1.Note = string.IsNullOrEmpty(note) ? $"[SIM 1 - Eşleşen: {sim2.Imei}]" : $"[SIM 1] {note}";
            sim1.UsedDate = now;

            sim2.IsUsed = true;
            sim2.IsSelected = false;
            sim2.PairedImei = sim1.Imei;
            sim2.CustomerInfo = customer;
            sim2.TargetDevice = targetDevice;
            sim2.Note = string.IsNullOrEmpty(note) ? $"[SIM 2 - Eşleşen: {sim1.Imei}]" : $"[SIM 2] {note}";
            sim2.UsedDate = now;

            PersistVault();
            UpdateStatsAndFilters();
        }

        public string? RestoreToUsable(ImeiRecord record)
        {
            string? pairedImei = !string.IsNullOrWhiteSpace(record.PairedImei) ? record.PairedImei : null;

            record.IsUsed = false;
            record.IsSelected = false;
            record.PairedImei = string.Empty;

            if (!string.IsNullOrEmpty(pairedImei))
            {
                var pairedRecord = AllVaultRecords.FirstOrDefault(r => r.Imei == pairedImei);
                if (pairedRecord != null)
                {
                    pairedRecord.IsUsed = false;
                    pairedRecord.IsSelected = false;
                    pairedRecord.PairedImei = string.Empty;
                }
            }

            PersistVault();
            UpdateStatsAndFilters();
            return pairedImei;
        }

        public void DeleteVaultRecord(ImeiRecord record)
        {
            AllVaultRecords.Remove(record);
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void WipeAllVaultRecords()
        {
            AllVaultRecords.Clear();
            UsableRecords.Clear();
            UsedRecords.Clear();
            EmbeddedStorageService.ResetAllVaultAndPin();
            UpdateStatsAndFilters();
        }

        public void UpdateUsageDetails(ImeiRecord record, string customer, string targetDevice, string note)
        {
            record.CustomerInfo = customer;
            record.TargetDevice = targetDevice;
            record.Note = note;
            PersistVault();
            UpdateStatsAndFilters();
        }

        public void SetAllCurrentTabSelection(bool isSelected)
        {
            foreach (var r in CurrentTabCollection)
            {
                r.IsSelected = isSelected;
            }
            RefreshSelectionStats();
        }

        public void CopySelectedVaultImeis()
        {
            var selected = CurrentTabCollection.Where(r => r.IsSelected).ToList();
            if (!selected.Any())
            {
                Notify("Seçim Yapılmadı", "Kopyalamak için lütfen listeden en az bir IMEI seçin.", ToastType.Warning);
                return;
            }

            string text = string.Join(Environment.NewLine, selected.Select(r => r.Imei));
            Clipboard.SetText(text);
            Notify("Panoya Kopyalandı", $"Seçilen {selected.Count} adet IMEI numarası panoya kopyalandı.", ToastType.Success);
        }

        public int DeleteSelectedVaultRecords()
        {
            var selected = CurrentTabCollection.Where(r => r.IsSelected).ToList();
            foreach (var item in selected)
            {
                AllVaultRecords.Remove(item);
            }
            PersistVault();
            UpdateStatsAndFilters();
            return selected.Count;
        }

        public void AddOrUpdateVault(ImeiRecord record)
        {
            var existing = AllVaultRecords.FirstOrDefault(r => r.Imei == record.Imei);
            if (existing != null)
            {
                existing.Status = record.Status;
                existing.BrandModel = record.BrandModel;
                existing.Category = record.Category;
                if (!string.IsNullOrEmpty(record.Note)) existing.Note = record.Note;
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

            IEnumerable<ImeiRecord> query = AllVaultRecords;

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Imei.Contains(search) ||
                                         (r.BrandModel ?? "").ToLowerInvariant().Contains(search) ||
                                         (r.Status ?? "").ToLowerInvariant().Contains(search) ||
                                         (r.CustomerInfo ?? "").ToLowerInvariant().Contains(search) ||
                                         (r.TargetDevice ?? "").ToLowerInvariant().Contains(search) ||
                                         (r.Note ?? "").ToLowerInvariant().Contains(search));
            }

            foreach (var r in query)
            {
                if (r.IsUsed)
                    UsedRecords.Add(r);
                else
                    UsableRecords.Add(r);
            }

            RefreshSelectionStats();
        }

        public void UpdateStatsAndFilters()
        {
            CountTotalVault = AllVaultRecords.Count;
            CountUsable = AllVaultRecords.Count(r => !r.IsUsed);
            CountUsed = AllVaultRecords.Count(r => r.IsUsed);
            FilterVault();
        }

        public async Task<VaultCheckSummary?> ExecuteVaultCheckAsync(Action<ImeiRecord>? onCheckingRecord = null)
        {
            if (IsVaultCheckBusy)
                return null;

            bool isUsedTab = _vaultSubTab == "Used";
            var activeList = isUsedTab ? UsedRecords.ToList() : UsableRecords.ToList();
            var selectedList = activeList.Where(r => r.IsSelected).ToList();

            var targetList = selectedList.Count > 0 ? selectedList : activeList;

            if (!targetList.Any())
            {
                Notify("Kontrol Edilecek Kayıt Yok", "Bu sekmede kontrol edilecek herhangi bir IMEI numarası bulunmuyor.", ToastType.Warning);
                return null;
            }

            _vaultCheckCts?.Dispose();
            _vaultCheckCts = new CancellationTokenSource();

            IsVaultCheckBusy = true;
            IsVaultCheckPaused = false;
            _isVaultCheckCancelled = false;
            IsVaultCheckBarVisible = true;
            VaultCheckProgressPercentage = 0;

            var summary = new VaultCheckSummary
            {
                TotalChecked = 0
            };

            try
            {
                var semaphore = new SemaphoreSlim(3, 3);
                var queueLock = new object();
                int nextIndex = 0;
                int processedCount = 0;
                var activeTasks = new List<Task>();

                while (nextIndex < targetList.Count && !_isVaultCheckCancelled)
                {
                    while (IsVaultCheckPaused && !_isVaultCheckCancelled)
                    {
                        await Task.Delay(150);
                    }
                    if (_isVaultCheckCancelled) break;

                    try
                    {
                        await semaphore.WaitAsync(_vaultCheckCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (_isVaultCheckCancelled)
                    {
                        semaphore.Release();
                        break;
                    }

                    int currentIndex;
                    lock (queueLock)
                    {
                        currentIndex = nextIndex++;
                    }

                    if (currentIndex >= targetList.Count)
                    {
                        semaphore.Release();
                        break;
                    }

                    var rec = targetList[currentIndex];

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            if (_isVaultCheckCancelled) return;

                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                rec.IsChecking = true;
                                onCheckingRecord?.Invoke(rec);
                                VaultCheckStatusMessage = $"Kontrol ediliyor: {rec.Imei}";
                            });

                            try
                            {
                                var result = await QueryEngineAsync(rec.Imei, _vaultCheckCts.Token);
                                if (_isVaultCheckCancelled) return;

                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    rec.Category = result.Category;
                                    rec.Status = result.Status;
                                    if (!string.IsNullOrWhiteSpace(result.BrandModel) && result.BrandModel != "-")
                                    {
                                        rec.BrandModel = result.BrandModel;
                                    }
                                    rec.Source = result.Source;
                                    rec.QueryDate = DateTime.Now;

                                    lock (queueLock)
                                    {
                                        summary.TotalChecked++;
                                        string catUpper = (result.Category ?? "").ToUpperInvariant();
                                        if (catUpper == "KAYITLI") summary.RegisteredCount++;
                                        else if (catUpper == "KOPYALANMIS" || catUpper == "KLON") summary.ClonedCount++;
                                        else if (catUpper == "KAYIT_DISI" || catUpper == "KAYITSIZ") summary.UnregisteredCount++;
                                        else if (catUpper == "SAVCILIK_KAPATILMIS" || catUpper == "CALINTI" || catUpper == "KULLANILMAYAN_KAPATILMIS") summary.ClosedCount++;
                                        else summary.ErrorCount++;
                                    }
                                });
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }
                            catch (Exception ex)
                            {
                                if (_isVaultCheckCancelled) return;
                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    rec.Category = "HATA";
                                    rec.Status = "Sorgu hatası: " + ex.Message;
                                    rec.QueryDate = DateTime.Now;
                                    lock (queueLock)
                                    {
                                        summary.TotalChecked++;
                                        summary.ErrorCount++;
                                    }
                                });
                            }
                            finally
                            {
                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    rec.IsChecking = false;
                                    lock (queueLock)
                                    {
                                        processedCount++;
                                        VaultCheckProgressPercentage = Math.Round(((double)processedCount / targetList.Count) * 100, 1);
                                        VaultCheckStatusMessage = $"Kontrol ediliyor ({processedCount}/{targetList.Count})";
                                    }
                                });
                            }

                            await Task.Delay(500);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    activeTasks.Add(task);
                }

                try
                {
                    await Task.WhenAll(activeTasks);
                }
                catch { }

                try
                {
                    PersistVault();
                }
                catch { }

                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateStatsAndFilters();
                    });
                }
                else
                {
                    UpdateStatsAndFilters();
                }

                if (_isVaultCheckCancelled)
                {
                    Notify("Kontrol Durduruldu", $"Kasa kontrolü durduruldu ({summary.TotalChecked}/{targetList.Count} IMEI sorgulandı).", ToastType.Info);
                    return null;
                }

                return summary.TotalChecked > 0 ? summary : null;
            }
            finally
            {
                foreach (var r in targetList)
                {
                    r.IsChecking = false;
                }
                IsVaultCheckBusy = false;
                IsVaultCheckPaused = false;
                IsVaultCheckBarVisible = false;
                VaultCheckProgressPercentage = 0;
            }
        }

        public int KeepOnlyRegisteredInCurrentTab()
        {
            bool isUsedTab = _vaultSubTab == "Used";
            var nonRegistered = AllVaultRecords
                .Where(r => r.IsUsed == isUsedTab && !string.Equals(r.Category, "KAYITLI", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in nonRegistered)
            {
                AllVaultRecords.Remove(item);
            }
            PersistVault();
            UpdateStatsAndFilters();
            return nonRegistered.Count;
        }

        public void ExportCurrentVaultToTxt()
        {
            var targetList = VaultSubTab == "Usable" ? UsableRecords : UsedRecords;
            if (!targetList.Any())
            {
                Notify("Dışa Aktarılacak Kayıt Yok", "Bu sekmede dışa aktarılacak herhangi bir IMEI kaydı bulunmuyor.", ToastType.Warning);
                return;
            }

            string tabLabel = VaultSubTab == "Usable" ? "Kullanilabilir" : "Kullanilanlar";
            var saveDialog = new SaveFileDialog
            {
                Title = "IMEI Kasasını TXT Olarak Dışa Aktar",
                Filter = "Metin Dosyaları (*.txt)|*.txt",
                FileName = $"Remofix_Kasa_{tabLabel}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var lines = targetList.Select(r =>
                    $"{r.Imei}\t{r.BrandModel}\t{r.Status}\t{(string.IsNullOrEmpty(r.Note) ? "-" : r.Note)}\t{r.Category}\t{(string.IsNullOrEmpty(r.CustomerInfo) ? "-" : r.CustomerInfo)}\t{(string.IsNullOrEmpty(r.TargetDevice) ? "-" : r.TargetDevice)}");
                File.WriteAllLines(saveDialog.FileName, lines, System.Text.Encoding.UTF8);
                Notify("Dışa Aktarma Başarılı", $"{targetList.Count} adet IMEI başarıyla dışa aktarıldı:\n{Path.GetFileName(saveDialog.FileName)}", ToastType.Success);
            }
        }

        public void ImportVaultFromTxt()
        {
            var openDialog = new OpenFileDialog
            {
                Title = "TXT Dosyasından IMEI İçe Aktar",
                Filter = "Metin Dosyaları (*.txt)|*.txt|Tüm Dosyalar (*.*)|*.*",
                Multiselect = true
            };

            if (openDialog.ShowDialog() != true)
                return;

            int addedCount = 0;
            int skippedCount = 0;
            bool markAsUsed = VaultSubTab == "Used";

            foreach (string filePath in openDialog.FileNames)
            {
                if (!File.Exists(filePath)) continue;

                string[] rawLines;
                try
                {
                    rawLines = File.ReadAllLines(filePath);
                }
                catch (Exception ex)
                {
                    Notify("Okuma Hatası", $"{Path.GetFileName(filePath)} okunamadı: {ex.Message}", ToastType.Danger);
                    continue;
                }

                foreach (var rawLine in rawLines)
                {
                    string line = (rawLine ?? "").Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.Contains('\t'))
                    {
                        var parts = line.Split('\t');
                        string rawImei = Regex.Replace(parts[0], @"\D", "");
                        if (rawImei.Length == 14)
                        {
                            int cd = LuhnHelper.CalculateCheckDigit(rawImei);
                            if (cd >= 0) rawImei += cd.ToString();
                        }

                        if (rawImei.Length == 15)
                        {
                            if (AllVaultRecords.Any(r => r.Imei == rawImei))
                            {
                                skippedCount++;
                                continue;
                            }

                            string brandModel = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : "-";
                            string status = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : "Kayıtlı (İçe Aktarıldı)";
                            string note = parts.Length > 3 && parts[3].Trim() != "-" ? parts[3].Trim() : string.Empty;
                            string category = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4].Trim() : "KAYITLI";
                            string customer = parts.Length > 5 && parts[5].Trim() != "-" ? parts[5].Trim() : string.Empty;
                            string targetDev = parts.Length > 6 && parts[6].Trim() != "-" ? parts[6].Trim() : string.Empty;

                            AllVaultRecords.Insert(0, new ImeiRecord
                            {
                                Imei = rawImei,
                                BrandModel = brandModel,
                                Status = status,
                                Note = note,
                                Category = category,
                                CustomerInfo = customer,
                                TargetDevice = targetDev,
                                Source = "TXT İçe Aktarım",
                                IsUsed = markAsUsed,
                                UsedDate = markAsUsed ? DateTime.Now : null,
                                QueryDate = DateTime.Now
                            });
                            addedCount++;
                            continue;
                        }
                    }

                    var match = Regex.Match(line, @"^(\d{14,15})(?:[\s\-:;,\|]+(.*))?$");
                    if (match.Success)
                    {
                        string imeiDigits = match.Groups[1].Value;
                        if (imeiDigits.Length == 14)
                        {
                            int cd = LuhnHelper.CalculateCheckDigit(imeiDigits);
                            if (cd >= 0) imeiDigits += cd.ToString();
                        }

                        if (imeiDigits.Length == 15)
                        {
                            if (AllVaultRecords.Any(r => r.Imei == imeiDigits))
                            {
                                skippedCount++;
                                continue;
                            }

                            string extraText = match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;
                            AllVaultRecords.Insert(0, new ImeiRecord
                            {
                                Imei = imeiDigits,
                                BrandModel = !string.IsNullOrEmpty(extraText) ? extraText : "-",
                                Status = "Kayıtlı (İçe Aktarıldı)",
                                Note = markAsUsed && !string.IsNullOrEmpty(extraText) ? extraText : string.Empty,
                                Category = "KAYITLI",
                                Source = "TXT İçe Aktarım",
                                IsUsed = markAsUsed,
                                UsedDate = markAsUsed ? DateTime.Now : null,
                                QueryDate = DateTime.Now
                            });
                            addedCount++;
                            continue;
                        }
                    }

                    var matches15 = Regex.Matches(line, @"\d{15}");
                    if (matches15.Count > 0)
                    {
                        foreach (Match m in matches15)
                        {
                            string imei15 = m.Value;
                            if (AllVaultRecords.Any(r => r.Imei == imei15))
                            {
                                skippedCount++;
                                continue;
                            }

                            AllVaultRecords.Insert(0, new ImeiRecord
                            {
                                Imei = imei15,
                                BrandModel = "-",
                                Status = "Kayıtlı (İçe Aktarıldı)",
                                Category = "KAYITLI",
                                Source = "TXT İçe Aktarım",
                                IsUsed = markAsUsed,
                                UsedDate = markAsUsed ? DateTime.Now : null,
                                QueryDate = DateTime.Now
                            });
                            addedCount++;
                        }
                    }
                }
            }

            if (addedCount > 0)
            {
                PersistVault();
                UpdateStatsAndFilters();
                string skipMsg = skippedCount > 0 ? $" ({skippedCount} mükerrer kayıt atlandı)" : "";
                Notify("İçe Aktarma Başarılı", $"{addedCount} adet IMEI başarıyla kasaya aktarıldı.{skipMsg}", ToastType.Success);
            }
            else if (skippedCount > 0)
            {
                Notify("Mükerrer Kayıtlar", $"Dosyadaki {skippedCount} adet IMEI zaten kasada mevcut olduğu için tekrar eklenmedi.", ToastType.Info);
            }
            else
            {
                Notify("Geçerli IMEI Bulunamadı", "Seçilen TXT dosyasında geçerli (14 veya 15 haneli) IMEI numarası bulunamadı.", ToastType.Warning);
            }
        }

        private async Task<ImeiRecord> QueryEngineAsync(string imei, CancellationToken ct = default)
        {
            await EngineProcessManager.EnsureEngineRunningAsync();
            ct.ThrowIfCancellationRequested();

            string url = $"http://127.0.0.1:5892/api/query/single?imei={imei}";
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct);
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
