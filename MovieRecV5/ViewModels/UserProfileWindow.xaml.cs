using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;

namespace MovieRecV5.ViewModels
{
    public partial class UserProfileWindow : Window
    {
        private User currentUser;
        private MainWindow mainWindow;
        private PostgresDatabaseService _databaseService;

        // Цвета для диаграмм
        private Color[] _chartColors = new[]
        {
            Colors.DodgerBlue,
            Colors.Orange,
            Colors.LimeGreen,
            Colors.Red,
            Colors.Purple,
            Colors.Teal,
            Colors.Gold,
            Colors.Coral,
            Colors.MediumOrchid,
            Colors.SkyBlue
        };

        public UserProfileWindow(User user, MainWindow mainWindow)
        {
            InitializeComponent();
            this.currentUser = user;
            this.mainWindow = mainWindow;
            _databaseService = new PostgresDatabaseService();

            InitializeAvatar();
            LoadUserData();
            LoadUserStats();
        }

        private void LoadUserData()
        {
            UserNameText.Text = currentUser?.DisplayName ?? currentUser?.Login ?? "Пользователь";
            UserEmailText.Text = currentUser?.Email ?? "Email не указан";

            int watchedCount = _databaseService.GetWatchedMoviesCount(currentUser.Id);
            int watchListCount = _databaseService.GetWatchListCount(currentUser.Id);
            int favoritesCount = _databaseService.GetFavoritesCount(currentUser.Id); // НОВОЕ
            int ratingsCount = _databaseService.GetUserRatingsCount(currentUser.Id);

            WatchedCountText.Text = watchedCount.ToString();
            WatchListCountText.Text = watchListCount.ToString();
            FavoritesCountText.Text = favoritesCount.ToString(); // НОВОЕ (добавить в XAML)
            RatingsCountText.Text = ratingsCount.ToString();

            // Рассчитываем среднюю оценку
            double avgRating = 0;
            var stats = _databaseService.GetUserStats(currentUser.Id);
            if (stats.RatingTimeline.Any())
            {
                avgRating = stats.RatingTimeline.Average(p => p.Rating);
            }
            AvgRatingText.Text = avgRating.ToString("F1");
        }

        private void LoadUserStats()
        {
            try
            {
                var stats = _databaseService.GetUserStats(currentUser.Id);
                int watchedCount = _databaseService.GetWatchedMoviesCount(currentUser.Id);

                if (watchedCount > 0)
                {
                    // 1. Диаграмма по жанрам
                    CreateGenreChart(stats.GenreDistribution, watchedCount);

                    // 2. Диаграмма по годам
                    CreateYearChart(stats.YearDistribution, watchedCount);

                    // 3. Диаграмма по оценкам
                    CreateRatingChart(stats.RatingDistribution);
                }
                else
                {
                    ShowNoDataMessage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading stats: {ex.Message}");
                ShowNoDataMessage();
            }
        }

        private void CreateGenreChart(Dictionary<string, int> genreDistribution, int totalCount)
        {
            GenresGrid.Children.Clear();
            GenresGrid.ColumnDefinitions.Clear();

            var topGenres = genreDistribution
                .OrderByDescending(g => g.Value)
                .Take(8)
                .ToList();

            if (!topGenres.Any())
            {
                GenresSummaryText.Text = "Нет данных по жанрам";
                return;
            }

            int maxCount = topGenres.Max(g => g.Value);

            for (int i = 0; i < topGenres.Count; i++)
            {
                var genre = topGenres[i];
                double percentage = (double)genre.Value / totalCount * 100;

                // Добавляем колонку
                GenresGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Создаем контейнер для столбца
                var columnStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(2)
                };

                // Столбец диаграммы
                var barHeight = (genre.Value / (double)maxCount) * 80;
                var bar = new Border
                {
                    Height = barHeight,
                    Width = 30,
                    Background = new SolidColorBrush(_chartColors[i % _chartColors.Length]),
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Margin = new Thickness(0, 0, 0, 2)
                };

                columnStack.Children.Add(bar);

                // Подпись
                var label = new TextBlock
                {
                    Text = genre.Key,
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 50,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                columnStack.Children.Add(label);

                // Количество
                var countLabel = new TextBlock
                {
                    Text = genre.Value.ToString(),
                    FontSize = 8,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                columnStack.Children.Add(countLabel);

                Grid.SetColumn(columnStack, i);
                GenresGrid.Children.Add(columnStack);
            }

            GenresSummaryText.Text = $"Всего {topGenres.Count} уникальных жанров. " +
                                   $"Самый популярный: {topGenres.First().Key} ({topGenres.First().Value} фильмов)";
        }

        private void CreateYearChart(Dictionary<int, int> yearDistribution, int totalCount)
        {
            YearsGrid.Children.Clear();
            YearsGrid.ColumnDefinitions.Clear();

            var topYears = yearDistribution
                .OrderByDescending(y => y.Value)
                .Take(8)
                .ToList();

            if (!topYears.Any())
            {
                YearsSummaryText.Text = "Нет данных по годам";
                return;
            }

            int maxCount = topYears.Max(y => y.Value);

            for (int i = 0; i < topYears.Count; i++)
            {
                var year = topYears[i];
                double percentage = (double)year.Value / totalCount * 100;

                YearsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var columnStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(2)
                };

                var barHeight = (year.Value / (double)maxCount) * 80;
                var bar = new Border
                {
                    Height = barHeight,
                    Width = 30,
                    Background = new SolidColorBrush(_chartColors[(i + 2) % _chartColors.Length]),
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Margin = new Thickness(0, 0, 0, 2)
                };

                columnStack.Children.Add(bar);

                var label = new TextBlock
                {
                    Text = year.Key.ToString(),
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                columnStack.Children.Add(label);

                var countLabel = new TextBlock
                {
                    Text = year.Value.ToString(),
                    FontSize = 8,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                columnStack.Children.Add(countLabel);

                Grid.SetColumn(columnStack, i);
                YearsGrid.Children.Add(columnStack);
            }

            YearsSummaryText.Text = $"Всего {topYears.Count} уникальных лет. " +
                                  $"Самый популярный год: {topYears.First().Key} ({topYears.First().Value} фильмов)";
        }

        private void CreateRatingChart(Dictionary<int, int> ratingDistribution)
        {
            RatingsGrid.Children.Clear();
            RatingsGrid.ColumnDefinitions.Clear();

            // Заполняем все оценки от 1 до 10
            for (int rating = 1; rating <= 10; rating++)
            {
                if (!ratingDistribution.ContainsKey(rating))
                    ratingDistribution[rating] = 0;
            }

            var sortedRatings = ratingDistribution
                .OrderBy(r => r.Key)
                .ToList();

            int maxCount = sortedRatings.Max(r => r.Value);
            if (maxCount == 0) maxCount = 1;

            for (int i = 0; i < sortedRatings.Count; i++)
            {
                var rating = sortedRatings[i];

                RatingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var columnStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(2)
                };

                var barHeight = (rating.Value / (double)maxCount) * 80;
                var bar = new Border
                {
                    Height = barHeight,
                    Width = 20,
                    Background = GetRatingColor(rating.Key),
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Margin = new Thickness(0, 0, 0, 2)
                };

                columnStack.Children.Add(bar);

                var label = new TextBlock
                {
                    Text = rating.Key.ToString(),
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                columnStack.Children.Add(label);

                var countLabel = new TextBlock
                {
                    Text = rating.Value.ToString(),
                    FontSize = 8,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                columnStack.Children.Add(countLabel);

                Grid.SetColumn(columnStack, i);
                RatingsGrid.Children.Add(columnStack);
            }

            var totalRatings = sortedRatings.Sum(r => r.Value);
            if (totalRatings > 0)
            {
                var mostCommonRating = sortedRatings.OrderByDescending(r => r.Value).First();
                RatingsSummaryText.Text = $"Всего оценок: {totalRatings}. " +
                                        $"Самая частая оценка: {mostCommonRating.Key}/10 ({mostCommonRating.Value} раз)";
            }
            else
            {
                RatingsSummaryText.Text = "Нет оценок";
            }
        }


        private Brush GetRatingColor(int rating)
        {
            // Градиент от красного (1) к зеленому (10)
            var hue = (rating - 1) * 0.11; // 0.0 - 1.0
            var color = HsvToRgb(hue * 120, 0.8, 0.8); // От красного (0°) к зеленому (120°)
            return new SolidColorBrush(color);
        }

        private Color HsvToRgb(double h, double s, double v)
        {
            h = Math.Max(0, Math.Min(360, h));
            s = Math.Max(0, Math.Min(1, s));
            v = Math.Max(0, Math.Min(1, v));

            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;

            if (h >= 0 && h < 60) { r = c; g = x; b = 0; }
            else if (h >= 60 && h < 120) { r = x; g = c; b = 0; }
            else if (h >= 120 && h < 180) { r = 0; g = c; b = x; }
            else if (h >= 180 && h < 240) { r = 0; g = x; b = c; }
            else if (h >= 240 && h < 300) { r = x; g = 0; b = c; }
            else if (h >= 300 && h < 360) { r = c; g = 0; b = x; }

            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255));
        }

        private void ShowNoDataMessage()
        {
            GenresSummaryText.Text = "Нет данных о просмотренных фильмах";
            YearsSummaryText.Text = "Добавьте фильмы в 'Просмотренные' чтобы увидеть статистику";
            RatingsSummaryText.Text = "Оцените фильмы чтобы увидеть распределение оценок";
        }

        private void InitializeAvatar()
        {
            try
            {
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.AvatarUrl))
                {
                    if (currentUser.AvatarUrl == "default")
                    {
                        SetDefaultAvatarWithInitials();
                    }
                    else if (File.Exists(currentUser.AvatarUrl))
                    {
                        LoadAvatarFromFile(currentUser.AvatarUrl);
                    }
                    else if (currentUser.AvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
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
                bitmap.Freeze(); 
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
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawEllipse(
                    Brushes.LightGray,
                    new Pen(Brushes.DarkGray, 1),
                    new Point(40, 40),
                    40, 40);

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

                double x = 40 - formattedText.Width / 2;
                double y = 40 - formattedText.Height / 2;
                drawingContext.DrawText(formattedText, new Point(x, y));
            }

            var renderTarget = new RenderTargetBitmap(80, 80, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();

            UserAvatarImage.Source = renderTarget;
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
                return name.Substring(0, 2).ToUpper();
            }

            return name.ToUpper() + "?";
        }

        private void LoadWatchedMovies()
        {
            var watched_Movies = _databaseService.GetWatchedMovies(currentUser.Id);
        }

        public void RefreshUserAvatar()
        {
            InitializeAvatar();

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

                MessageBox.Show("Вы вышли из аккаунта. Для входа используйте свои данные.",
                               "Выход выполнен", MessageBoxButton.OK, MessageBoxImage.Information);
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
                var updatedUser = _databaseService.GetUserByLogin(currentUser.Login);
                if (updatedUser != null)
                {
                    currentUser = updatedUser;

                    RefreshUserAvatar();

                    if (mainWindow != null)
                    {
                        mainWindow.RefreshUserData();
                    }
                }
            }
        }

        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            var favoritesWindow = new FavoritesWindow(currentUser)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            favoritesWindow.ShowDialog();
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