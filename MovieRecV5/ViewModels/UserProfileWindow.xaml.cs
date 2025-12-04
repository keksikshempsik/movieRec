using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MovieRecV5.ViewModels
{
    public partial class UserProfileWindow : Window
    {
        private User currentUser;
        private MainWindow mainWindow;
        private DatabaseService _databaseService;

        public UserProfileWindow(User user, MainWindow mainWindow)
        {
            InitializeComponent();
            this.currentUser = user;
            this.mainWindow = mainWindow;
            _databaseService = new DatabaseService();

            // Инициализируем аватар
            InitializeAvatar();

            LoadUserData();
            LoadWatchedMovies();
        }

        private void InitializeAvatar()
        {
            try
            {
                // Проверяем наличие аватара у пользователя
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.AvatarUrl))
                {
                    if (currentUser.AvatarUrl == "default")
                    {
                        // Устанавливаем аватар по умолчанию с инициалами
                        SetDefaultAvatarWithInitials();
                    }
                    else if (File.Exists(currentUser.AvatarUrl))
                    {
                        // Загружаем из файла
                        LoadAvatarFromFile(currentUser.AvatarUrl);
                    }
                    else if (currentUser.AvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        // Загружаем из URL
                        LoadAvatarFromUrl(currentUser.AvatarUrl);
                    }
                    else
                    {
                        SetDefaultAvatarWithInitials();
                    }
                }
                else
                {
                    SetDefaultAvatarWithInitials();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing avatar: {ex.Message}");
                SetDefaultAvatarWithInitials();
            }
        }

        private void LoadAvatarFromFile(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Важно для безопасности в WPF
                UserAvatarImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading avatar from file: {ex.Message}");
                SetDefaultAvatarWithInitials();
            }
        }

        private async void LoadAvatarFromUrl(string url)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                UserAvatarImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading avatar from URL: {ex.Message}");
                SetDefaultAvatarWithInitials();
            }
        }

        private void SetDefaultAvatarWithInitials()
        {
            // Создаем аватар с инициалами пользователя
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Фон
                drawingContext.DrawEllipse(
                    Brushes.LightGray,
                    new Pen(Brushes.DarkGray, 1),
                    new Point(40, 40),
                    40, 40);

                // Инициалы
                string initials = GetUserInitials();
                var formattedText = new FormattedText(
                    initials,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    24,
                    Brushes.White,
                    96.0
                );

                // Центрируем текст
                double x = 40 - formattedText.Width / 2;
                double y = 40 - formattedText.Height / 2;
                drawingContext.DrawText(formattedText, new Point(x, y));
            }

            var renderTarget = new RenderTargetBitmap(80, 80, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();

            UserAvatarImage.Source = renderTarget;
        }

        private void LoadUserData()
        {
            // Основная информация
            UserNameText.Text = currentUser?.DisplayName ?? currentUser?.Login ?? "Пользователь";
            UserEmailText.Text = currentUser?.Email ?? "Email не указан";

            // Статистика
            int watchedCount = _databaseService.GetWatchedMoviesCount(currentUser.Id);
            int watchListCount = _databaseService.GetWatchListCount(currentUser.Id);
            int ratingsCount = _databaseService.GetUserRatingsCount(currentUser.Id);

            WatchedCountText.Text = watchedCount.ToString();
            WatchListCountText.Text = watchListCount.ToString();
            RatingsCountText.Text = ratingsCount.ToString();
            FavoritesCountText.Text = "0"; // Можно добавить функционал избранного

            // Активность
            string activity = "";
            if (watchListCount > 0)
                activity += $"{watchListCount} фильмов в списке 'Хочу посмотреть'\n";
            if (watchedCount > 0)
                activity += $"Просмотрено {watchedCount} фильмов\n";
            if (ratingsCount > 0)
                activity += $"Оставлено {ratingsCount} оценок";

            ActivityText.Text = string.IsNullOrEmpty(activity)
                ? "Активность отсутствует"
                : activity;
        }

        private string GetUserInitials()
        {
            if (string.IsNullOrEmpty(currentUser?.Login))
                return "??";

            var name = !string.IsNullOrEmpty(currentUser.DisplayName)
                ? currentUser.DisplayName
                : currentUser.Login;

            name = name.Trim();

            if (name.Length >= 2)
            {
                // Берем первые две буквы
                return name.Substring(0, 2).ToUpper();
            }

            return name.ToUpper() + "?";
        }

        private void LoadWatchedMovies()
        {
            // Этот метод остается без изменений
            var watchedMovies = _databaseService.GetWatchedMovies(currentUser.Id);
        }

        // ДОБАВЛЕННЫЙ МЕТОД: Обновление аватара после редактирования профиля
        public void RefreshUserAvatar()
        {
            // Обновляем аватар при изменении профиля
            InitializeAvatar();

            // Также обновляем остальные данные
            LoadUserData();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите выйти из аккаунта?",
                "Выход из аккаунта", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                mainWindow.LogoutUser();
                this.Close();
            }
        }

        private void EditProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new EditProfileWindow(currentUser)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (editWindow.ShowDialog() == true)
            {
                // Обновляем данные пользователя из базы
                var updatedUser = _databaseService.GetUserByLogin(currentUser.Login);
                if (updatedUser != null)
                {
                    currentUser = updatedUser;

                    // Обновляем аватар
                    RefreshUserAvatar();

                    // Сообщаем главному окну об изменениях
                    if (mainWindow != null)
                    {
                        mainWindow.RefreshUserData();
                    }
                }
            }
        }

        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция избранных фильмов в разработке",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var watchedMoviesWindow = new WatchedMoviesWindow(currentUser)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            watchedMoviesWindow.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция настроек в разработке",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void WatchListButton_Click(object sender, RoutedEventArgs e)
        {
            var watchListWindow = new WatchListWindow(currentUser)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            watchListWindow.ShowDialog();
        }
    }
}