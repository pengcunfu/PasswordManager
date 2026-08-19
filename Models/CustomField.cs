using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PasswordManager.Models
{
    /// <summary>
    /// 自定义字段实体类
    /// </summary>
    public class CustomField : INotifyPropertyChanged
    {
        private string _key;
        private string _value;
        private bool _isHidden;

        public CustomField()
        {
            _key = string.Empty;
            _value = string.Empty;
            _isHidden = false;
        }

        public CustomField(string key, string value, bool isHidden = false)
        {
            _key = key;
            _value = value;
            _isHidden = isHidden;
        }

        /// <summary>
        /// 字段名（如：邮箱、手机号、密保手机）
        /// </summary>
        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        /// <summary>
        /// 字段值
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>
        /// 是否隐藏显示（敏感信息）
        /// </summary>
        public bool IsHidden
        {
            get => _isHidden;
            set => SetProperty(ref _isHidden, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
