using Microsoft.Win32;
using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Data.SQLite;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MovieRecV5.ViewModels
{
    public partial class EditProfileWindow : Window
    {
        private User currentUser;
        private DatabaseService _databaseService;
        private string selectedAvatarPath;
        private Regex emailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public EditProfileWindow(User user)
        {
            InitializeComponent();
            currentUser = user;
            _databaseService = new DatabaseService();
            selectedAvatarPath = user.AvatarUrl;
            LoadUserData();
        }

        private void LoadUserData()
        {
            LoginTextBox.Text = currentUser.Login;
            DisplayNameTextBox.Text = currentUser.DisplayName;
            EmailTextBox.Text = currentUser.Email;

            // Загружаем аватар
            LoadAvatar(currentUser.AvatarUrl);
        }

        private void LoadAvatar(string avatarPath)
        {
            try
            {
                if (avatarPath == "default" || string.IsNullOrEmpty(avatarPath))
                {
                    // Показываем инициалы как аватар по умолчанию
                    AvatarImage.Source = null;
                    // Можно добавить генерацию инициалов здесь
                }
                else if (File.Exists(avatarPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    AvatarImage.Source = bitmap;
                }
                else if (avatarPath.StartsWith("http"))
                {
                    // Загрузка из интернета
                    var bitmap = new BitmapImage(new Uri(avatarPath));
                    AvatarImage.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading avatar: {ex.Message}");
            }
        }

        private void SelectAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedAvatarPath = openFileDialog.FileName;
                LoadAvatar(selectedAvatarPath);
            }
        }

        private void ResetAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            selectedAvatarPath = "default";
            AvatarImage.Source = null;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
                {
                    MessageBox.Show("Введите отображаемое имя", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
                {
                    MessageBox.Show("Введите email", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!emailRegex.IsMatch(EmailTextBox.Text))
                {
                    MessageBox.Show("Введите корректный email адрес", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка пароля
                if (!string.IsNullOrEmpty(NewPasswordBox.Password))
                {
                    if (NewPasswordBox.Password.Length < 6)
                    {
                        MessageBox.Show("Пароль должен содержать минимум 6 символов", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
                    {
                        MessageBox.Show("Пароли не совпадают", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Сохраняем изменения
                bool profileUpdated = _databaseService.UpdateUserProfile(
                    currentUser.Id,
                    DisplayNameTextBox.Text.Trim(),
                    EmailTextBox.Text.Trim(),
                    selectedAvatarPath
                );

                if (profileUpdated)
                {
                    // Обновляем пароль, если он был изменен
                    if (!string.IsNullOrEmpty(NewPasswordBox.Password))
                    {
                        // Здесь нужно добавить метод для обновления пароля
                        UpdatePassword(NewPasswordBox.Password);
                    }

                    MessageBox.Show("Профиль успешно обновлен", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Обновляем данные пользователя в текущем объекте
                    currentUser.DisplayName = DisplayNameTextBox.Text.Trim();
                    currentUser.Email = EmailTextBox.Text.Trim();
                    currentUser.AvatarUrl = selectedAvatarPath;

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Ошибка при обновлении профиля", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePassword(string newPassword)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_databaseService.GetDatabasePath()}"))
                {
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = "UPDATE Users SET Password = $password WHERE Id = $userId";
                    command.Parameters.AddWithValue("$password", User.HashPassword(newPassword));
                    command.Parameters.AddWithValue("$userId", currentUser.Id);

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating password: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}