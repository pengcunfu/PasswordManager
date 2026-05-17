using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PasswordManager.Models
{
    /// <summary>
    /// 密码条目实体类
    /// </summary>
    public class PasswordEntry : INotifyPropertyChanged
    {
        private string _id;
        private string _title;
        private string _username;
        private string _password;
        private string _url;
        private string _notes;
        private string _category;
        private DateTime _createdAt;
        private DateTime _updatedAt;

        public PasswordEntry()
        {
            _id = GenerateId();
            _title = string.Empty;
            _username = string.Empty;
            _password = string.Empty;
            _url = string.Empty;
            _notes = string.Empty;
            _category = string.Empty;
            _createdAt = DateTime.Now;
            _updatedAt = DateTime.Now;
        }

        public PasswordEntry(string title, string username, string password, string url, string notes, string category)
            : this()
        {
            _title = title;
            _username = username;
            _password = password;
            _url = url;
            _notes = notes;
            _category = category;
        }

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string URL
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
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
            return DateTime.Now.Ticks.ToString();
        }

        /// <summary>
        /// 更新修改时间
        /// </summary>
        public void UpdateModifiedTime()
        {
            UpdatedAt = DateTime.Now;
        }
    }
}
