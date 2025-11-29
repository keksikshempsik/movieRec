using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Windows;

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
            LoadUserData();
            LoadWatchedMovies();
        }

        private void LoadUserData()
        {
            // Основная информация
            UserNameText.Text = currentUser?.Login ?? "Пользователь";
            UserEmailText.Text = currentUser?.Email ?? "Email не указан";

            // Инициалы
            UserInitialsText.Text = GetUserInitials();

            // Статистика
            int watchedCount = _databaseService.GetWatchedMoviesCount(currentUser.Id);
            WatchedCountText.Text = watchedCount.ToString();
            FavoritesCountText.Text = "0"; // Можно добавить функционал избранного
            RatingsCountText.Text = "0"; // Можно подсчитать количество оценок

            // Активность
            ActivityText.Text = watchedCount > 0
                ? $"Вы посмотрели {watchedCount} фильмов"
                : "Активность отсутствует";
        }

        private void LoadWatchedMovies()
        {
            var watchedMovies = _databaseService.GetWatchedMovies(currentUser.Id);
            // Здесь можно добавить отображение списка просмотренных фильмов
        }

        private string GetUserInitials()
        {
            if (string.IsNullOrEmpty(currentUser?.Login))
                return "??";

            var login = currentUser.Login.Trim();
            if (login.Length >= 2)
                return login.Substring(0, 2).ToUpper();

            return login.ToUpper() + "?";
        }

        // ДОБАВЛЕННЫЙ МЕТОД
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
            MessageBox.Show("Функция редактирования профиля в разработке",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция избранных фильмов в разработке",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция истории просмотров в разработке",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция настроек в разработке",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}