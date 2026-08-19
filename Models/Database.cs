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
        private List<Group> _groups;

        public Database()
        {
            _salt = string.Empty;
            _entries = new List<PasswordEntry>();
            _version = "1.0";
            _createdAt = DateTime.Now;
            _groups = new List<Group>();
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

        public List<Group> Groups
        {
            get => _groups;
            set => SetProperty(ref _groups, value);
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
    /// 密码分组实体类
    /// </summary>
    public class Group : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _description;
        private string _color;
        private int _sortOrder;
        private DateTime _createdAt;
        private DateTime _updatedAt;

        public Group()
        {
            _id = GenerateId();
            _name = string.Empty;
            _description = string.Empty;
            _color = "#4A90E2";
            _sortOrder = 0;
            _createdAt = DateTime.Now;
            _updatedAt = DateTime.Now;
        }

        public Group(string name, string description = "", string color = "#4A90E2") : this()
        {
            _name = name;
            _description = description;
            _color = color;
        }

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public int SortOrder
        {
            get => _sortOrder;
            set => SetProperty(ref _sortOrder, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
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

        /// <summary>
        /// 生成唯一ID
        /// </summary>
        private static string GenerateId()
        {
            return $"group_{DateTime.Now.Ticks}";
        }

        /// <summary>
        /// 更新修改时间
        /// </summary>
        public void UpdateModifiedTime()
        {
            UpdatedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 密码分类实体类（已弃用，使用Group替代）
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
