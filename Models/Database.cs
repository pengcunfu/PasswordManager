using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PasswordManager.Models
{
    /// <summary>
    /// 密码数据库实体类
    /// </summary>
    public class Database : INotifyPropertyChanged
    {
        private string _salt;
        private List<PasswordEntry> _entries;
        private string _version;
        private DateTime _createdAt;

        public Database()
        {
            _salt = string.Empty;
            _entries = new List<PasswordEntry>();
            _version = "1.0";
            _createdAt = DateTime.Now;
        }

        public Database(string salt) : this()
        {
            _salt = salt;
        }

        public string Salt
        {
            get => _salt;
            set => SetProperty(ref _salt, value);
        }

        public List<PasswordEntry> Entries
        {
            get => _entries;
            set => SetProperty(ref _entries, value);
        }

        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
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

    /// <summary>
    /// 密码分类实体类
    /// </summary>
    public class Category : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _color = string.Empty;
        private string _icon = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
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

    /// <summary>
    /// 应用设置实体类
    /// </summary>
    public class AppSettings : INotifyPropertyChanged
    {
        private string _theme = "light";
        private int _autoLockTime = 15;
        private int _clearClipboardTime = 30;
        private bool _showPasswords = false;
        private bool _backupEnabled = true;

        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        public int AutoLockTime
        {
            get => _autoLockTime;
            set => SetProperty(ref _autoLockTime, value);
        }

        public int ClearClipboardTime
        {
            get => _clearClipboardTime;
            set => SetProperty(ref _clearClipboardTime, value);
        }

        public bool ShowPasswords
        {
            get => _showPasswords;
            set => SetProperty(ref _showPasswords, value);
        }

        public bool BackupEnabled
        {
            get => _backupEnabled;
            set => SetProperty(ref _backupEnabled, value);
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
