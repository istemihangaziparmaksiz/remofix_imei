using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ImeiStudio.Models
{
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
            set { _queryDate = value; OnPropertyChanged(); }
        }

        public bool IsUsed
        {
            get => _isUsed;
            set { _isUsed = value; OnPropertyChanged(); }
        }

        private bool _isChecking = false;

        public string Note
        {
            get => _note;
            set { _note = value; OnPropertyChanged(); }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsChecking
        {
            get => _isChecking;
            set { _isChecking = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
