using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using MovieRecV5.Models;
using MovieRecV5.Services;

namespace MovieRecV5.ViewModels
{
    public partial class EditProfilePage : Page
    {
        private User _currentUser;
        private DatabaseService _databaseService;
        private string _selectedAvatarPath;

        public EditProfilePage(User user)
        {
            InitializeComponent();
            _currentUser = user;
            _databaseService = new DatabaseService();
            _selectedAvatarPath = null;

            LoadUserData();
        }

        private void LoadUserData()
        {
            // Загружаем данные пользователя
            DisplayNameTextBox.Text = _currentUser.DisplayName;
            EmailTextBox.Text = _currentUser.Email;

            // Загружаем аватарку
            LoadAvatar();
        }

        private void LoadAvatar()
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentUser.AvatarUrl))
                {
                    // Если это Base64 строка
                    if (_currentUser.AvatarUrl.StartsWith("data:image"))
                    {
                        var base64Data = _currentUser.AvatarUrl.Split(',')[1];
                        var imageBytes = Convert.FromBase64String(base64Data);
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(imageBytes);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        AvatarImage.Source = bitmap;
                    }
                    else if (File.Exists(_currentUser.AvatarUrl))
                    {
                        // Если это путь к файлу
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage(
                            new Uri(_currentUser.AvatarUrl));
                        AvatarImage.Source = bitmap;
                    }
                    else
                    {
                        // Загружаем стандартный аватар
                        SetDefaultAvatar();
                    }
                }
                else
                {
                    SetDefaultAvatar();
                }
            }
            catch
            {
                SetDefaultAvatar();
            }
        }

        private void SetDefaultAvatar()
        {
            // Создаем простой градиентный круг как аватар по умолчанию
            var drawingVisual = new System.Windows.Media.DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = 150,
                    Height = 150,
                    Fill = new System.Windows.Media.LinearGradientBrush(
                        System.Windows.Media.Colors.LightBlue,
                        System.Windows.Media.Colors.DodgerBlue,
                        45)
                };
                ellipse.Arrange(new Rect(0, 0, 150, 150));
            }

            var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(
                150, 150, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            AvatarImage.Source = renderTarget;
        }

        private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Выберите аватарку"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    _selectedAvatarPath = openFileDialog.FileName;

                    // Превью выбранного изображения
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri(_selectedAvatarPath));
                    AvatarImage.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе файла: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedAvatarPath = null;
            SetDefaultAvatar();
        }

        private bool ValidateInput()
        {
            bool isValid = true;

            // Проверка отображаемого имени
            if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
            {
                MessageBox.Show("Введите отображаемое имя",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверка email
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                MessageBox.Show("Введите email",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверка формата email
            try
            {
                var addr = new System.Net.Mail.MailAddress(EmailTextBox.Text);
                if (addr.Address != EmailTextBox.Text)
                {
                    throw new FormatException();
                }
            }
            catch
            {
                MessageBox.Show("Введите корректный email адрес",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверка уникальности email
            if (!_databaseService.IsEmailAvailable(_currentUser.Id, EmailTextBox.Text))
            {
                EmailErrorText.Text = "Этот email уже используется другим пользователем";
                EmailErrorText.Visibility = Visibility.Visible;
                return false;
            }
            else
            {
                EmailErrorText.Visibility = Visibility.Collapsed;
            }

            return isValid;
        }

        private string ConvertImageToBase64(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                    return "";

                byte[] imageBytes = File.ReadAllBytes(imagePath);
                string base64String = Convert.ToBase64String(imageBytes);

                // Определяем тип файла
                string extension = Path.GetExtension(imagePath).ToLower();
                string mimeType = extension switch
                {
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".bmp" => "image/bmp",
                    _ => "image/jpeg"
                };

                return $"data:{mimeType};base64,{base64String}";
            }
            catch
            {
                return "";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                // Конвертируем изображение в Base64 если выбрано новое
                string avatarUrl = _currentUser.AvatarUrl;
                if (!string.IsNullOrEmpty(_selectedAvatarPath))
                {
                    avatarUrl = ConvertImageToBase64(_selectedAvatarPath);
                }
                else if (_selectedAvatarPath == "")
                {
                    // Если пользователь удалил аватар
                    avatarUrl = "";
                }

                // Обновляем профиль
                bool success = _databaseService.UpdateUserProfile(
                    _currentUser.Id,
                    DisplayNameTextBox.Text.Trim(),
                    EmailTextBox.Text.Trim(),
                    avatarUrl);

                if (success)
                {
                    // Обновляем объект пользователя
                    _currentUser.DisplayName = DisplayNameTextBox.Text.Trim();
                    _currentUser.Email = EmailTextBox.Text.Trim();
                    _currentUser.AvatarUrl = avatarUrl;

                    MessageBox.Show("Профиль успешно обновлен!",
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Закрываем окно или возвращаемся назад
                    Window.GetWindow(this)?.Close();
                }
                else
                {
                    MessageBox.Show("Не удалось обновить профиль",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}