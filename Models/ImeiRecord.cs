using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ImeiStudio.Models
{
    public class TacPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public string DisplayText => $"{Name} ({Prefix})";
    }

    public class ImeiRecord : INotifyPropertyChanged
    {
        private string _imei = string.Empty;
        private string _status = "Bilinmiyor";
        private string _brandModel = "-";
        private string _source = "e-Devlet / BTK";
        private string _category = "BEKLIYOR";
        private DateTime _queryDate = DateTime.Now;
        private bool _isUsed = false;
        private string _note = string.Empty;
        private string _customerInfo = string.Empty;
        private string _targetDevice = string.Empty;
        private DateTime? _usedDate = null;
        private string _pairedImei = string.Empty;

        private bool _isChecking = false;
        private bool _isSelected = false;

        public string Imei
        {
            get => _imei;
            set { _imei = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string BrandModel
        {
            get => _brandModel;
            set { _brandModel = value; OnPropertyChanged(); }
        }

        public string Source
        {
            get => _source;
            set { _source = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public DateTime QueryDate
        {
            get => _queryDate;
            set { _queryDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveUsedDate)); }
        }

        public bool IsUsed
        {
            get => _isUsed;
            set { _isUsed = value; OnPropertyChanged(); }
        }

        public string Note
        {
            get => _note;
            set { _note = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayUsageSummary)); }
        }

        public string CustomerInfo
        {
            get => _customerInfo;
            set { _customerInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayUsageSummary)); }
        }

        public string TargetDevice
        {
            get => _targetDevice;
            set { _targetDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayUsageSummary)); }
        }

        public DateTime? UsedDate
        {
            get => _usedDate;
            set { _usedDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveUsedDate)); }
        }

        public string PairedImei
        {
            get => _pairedImei;
            set { _pairedImei = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPairedImei)); }
        }

        [JsonIgnore]
        public bool HasPairedImei => !string.IsNullOrWhiteSpace(_pairedImei);

        [JsonIgnore]
        public DateTime EffectiveUsedDate => _usedDate ?? _queryDate;

        [JsonIgnore]
        public string DisplayUsageSummary
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(_customerInfo)) parts.Add($"Müşteri: {_customerInfo}");
                if (!string.IsNullOrWhiteSpace(_targetDevice)) parts.Add($"Cihaz: {_targetDevice}");
                if (!string.IsNullOrWhiteSpace(_note)) parts.Add(_note);
                return parts.Count > 0 ? string.Join("  |  ", parts) : "—";
            }
        }

        [JsonIgnore]
        public bool IsChecking
        {
            get => _isChecking;
            set { _isChecking = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
