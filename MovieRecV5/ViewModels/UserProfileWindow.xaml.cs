using System;
using System.Windows;
using MovieRecV5.Models;

namespace MovieRecV5.ViewModels
{
    public partial class UserProfileWindow : Window
    {
        private User currentUser;
        private MainWindow mainWindow;

        public UserProfileWindow(User user, MainWindow mainWindow)
        {
            InitializeComponent();
            this.currentUser = user;
            this.mainWindow = mainWindow;
            LoadUserData();
        }

        private void LoadUserData()
        {
            // Основная информация
            UserNameText.Text = currentUser?.Login ?? "Пользователь";
            UserEmailText.Text = currentUser?.Email ?? "Email не указан";

            // Инициалы
            UserInitialsText.Text = GetUserInitials();

            // Статистика (заглушки)
            WatchedCountText.Text = "0";
            FavoritesCountText.Text = "0";
            RatingsCountText.Text = "0";

            // Активность
            ActivityText.Text = "Активность отсутствует";
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