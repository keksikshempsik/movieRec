using MovieRecV5.Models;
using MovieRecV5.Services;
using MovieRecV5.ViewModels;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MovieRecV5
{
    public partial class MainWindow : Window
    {
        private PostgresDatabaseService _databaseService;
        private readonly SemaphoreSlim _throttler;
        public bool IsLogged { get; private set; }
        public User CurrentUser { get; private set; }

        public MainWindow()
        {
            try
            {
                // 1. Инициализация WPF компонентов
                InitializeComponent();

                // 2. Инициализация переменных
                IsLogged = false;
                CurrentUser = null;
                _throttler = new SemaphoreSlim(3, 3);

                // 3. Инициализация базы данных
                _databaseService = new PostgresDatabaseService();
                _databaseService.InitializeDatabase();

                // 4. Настройка элементов управления
                SearchTextBox.KeyDown += SearchTextBox_KeyDown;

                AutoLogin();

                UpdateUserButton();

                // 5. Настройка начального состояния
                SearchTextBox.GotFocus += SearchTextBox_GotFocus;
                SearchTextBox.LostFocus += SearchTextBox_LostFocus;

                // Скрываем прогресс-бар
                SearchProgressBar.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                // Критические ошибки инициализации
                Console.WriteLine($"❌ КРИТИЧЕСКАЯ ОШИБКА: {ex}");

                string errorMessage = $"Не удалось запустить приложение:\n\n{ex.Message}";

                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nДетали: {ex.InnerException.Message}";
                }

                MessageBox.Show(errorMessage,
                               "Критическая ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);

                // Закрываем приложение при критической ошибке
                Application.Current.Shutdown();
            }
        }

        private void ShowDetailedError(Exception ex)
        {
            string errorMessage = $"Ошибка: {ex.Message}\n\n";
            errorMessage += $"Тип: {ex.GetType().Name}\n";

            if (ex is PostgresException pgEx)
            {
                errorMessage += $"Код ошибки PostgreSQL: {pgEx.SqlState}\n";
                errorMessage += $"Сообщение PostgreSQL: {pgEx.MessageText}\n";
                errorMessage += $"Детали: {pgEx.Detail}\n";
            }

            if (ex.InnerException != null)
            {
                errorMessage += $"\nВнутреннее исключение: {ex.InnerException.Message}";
            }

            errorMessage += $"\n\nStack Trace:\n{ex.StackTrace}";

            MessageBox.Show(errorMessage, "Ошибка базы данных",
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void UpdateUserButton()
        {
            if (IsLogged && CurrentUser != null)
            {
                string displayName = !string.IsNullOrEmpty(CurrentUser.DisplayName)
                    ? CurrentUser.DisplayName
                    : CurrentUser.Login;

                if (displayName.Length > 15)
                {
                    displayName = displayName.Substring(0, 12) + "...";
                }

                UserProfileButton.Content = displayName;
                UserProfileButton.ToolTip = $"Профиль пользователя: {CurrentUser.Login}";
                UserProfileButton.FontWeight = FontWeights.SemiBold;

                // Включаем кнопку рекомендаций
                RecommendationsButton.IsEnabled = true;
                RecommendationsButton.ToolTip = "Персональные рекомендации фильмов";
            }
            else
            {
                UserProfileButton.Content = "Вход/Регистрация";
                UserProfileButton.ToolTip = "Войти или зарегистрироваться";
                UserProfileButton.FontWeight = FontWeights.Normal;

                // Выключаем кнопку рекомендаций
                RecommendationsButton.IsEnabled = false;
                RecommendationsButton.ToolTip = "Для рекомендаций требуется вход в систему";
            }
        }

        public void LoginUser(User user)
        {
            CurrentUser = user;
            IsLogged = true;
            UpdateUserButton();
        }

        public void LogoutUser()
        {
            CurrentUser = null;
            IsLogged = false;
            UpdateUserButton();
        }

        private void UserProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsLogged && CurrentUser != null)
            {
                var profileWindow = new UserProfileWindow(CurrentUser, this)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                profileWindow.ShowDialog();
            }
            else
            {
                var loginWindow = new Login(this)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                loginWindow.ShowDialog();
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetProgressStatus(true);
                SearchProgressBar.IsIndeterminate = true;

                if (SearchTextBox.Text == "Введите название..." || string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    MessageBox.Show("Введите название фильма");
                    return;
                }

                string searchTitle = SearchTextBox.Text.Trim();
                Console.WriteLine($"🔍 Поиск: '{searchTitle}'");

                int userId = CurrentUser?.Id ?? 0;
                List<Movie> finalMovies = new List<Movie>();

                // 1. Сначала быстрый поиск ID фильмов в TMDB
                SearchProgressBar.Value = 10;
                TmdbParser parser = new TmdbParser();

                var fastSearchResults = await parser.SearchMoviesFast(searchTitle, 40, 100); 
                Console.WriteLine($"🌐 Быстрый поиск TMDB: {fastSearchResults.Count} ID фильмов");

                if (!fastSearchResults.Any())
                {
                    MessageBox.Show("Фильмы не найдены.");
                    return;
                }

                SearchProgressBar.Value = 30;
                int progressStep = 60 / Math.Max(fastSearchResults.Count, 1);
                int processedCount = 0;

                // 2. Для каждого найденного фильма проверяем базу
                foreach (var fastResult in fastSearchResults)
                {
                    // ФИЛЬТРУЕМ: пропускаем фильмы с оценками меньше 100
                    if (fastResult.VoteCount < 100)
                    {
                        processedCount++;
                        SearchProgressBar.Value = 30 + (processedCount * progressStep);
                        continue;
                    }

                    var moviesFromDb = _databaseService.SearchMoviesInDatabase(searchTitle, userId, 100);
                    var existingMovie = moviesFromDb.FirstOrDefault(m =>
                        string.Equals(m.Title, fastResult.Title, StringComparison.OrdinalIgnoreCase) &&
                        m.Year == fastResult.Year);

                    if (existingMovie != null)
                    {
                        if (existingMovie.VoteCount < 100)
                        {
                            processedCount++;
                            SearchProgressBar.Value = 30 + (processedCount * progressStep);
                            continue;
                        }

                        // Фильм есть в базе - используем его
                        if (existingMovie.Id != fastResult.Id)
                        {
                            existingMovie.Id = fastResult.Id;
                        }

                        if (userId > 0)
                        {
                            existingMovie.IsWatched = _databaseService.IsMovieWatched(userId, existingMovie.Slug);
                            existingMovie.InWatchList = _databaseService.IsInWatchList(userId, existingMovie.Slug);
                        }
                        finalMovies.Add(existingMovie);
                        Console.WriteLine($"📁 Используем из базы: {existingMovie.Title} ({existingMovie.Year}, {existingMovie.VoteCount} оценок)");
                    }
                    else
                    {
                        // Фильма нет в базе - получаем полные данные из TMDB
                        var fullMovie = await parser.GetMovieByTmdbId(fastResult.Id);
                        if (fullMovie != null && !string.IsNullOrEmpty(fullMovie.Poster))
                        {
                            if (fullMovie.VoteCount < 100)
                            {
                                processedCount++;
                                SearchProgressBar.Value = 30 + (processedCount * progressStep);
                                continue;
                            }

                            fullMovie.Id = fastResult.Id;

                            // Сохраняем в базу
                            _databaseService.AddMovie(fullMovie);

                            if (userId > 0)
                            {
                                fullMovie.IsWatched = _databaseService.IsMovieWatched(userId, fullMovie.Slug);
                                fullMovie.InWatchList = _databaseService.IsInWatchList(userId, fullMovie.Slug);
                            }

                            finalMovies.Add(fullMovie);
                        }
                    }

                    processedCount++;
                    SearchProgressBar.Value = 30 + (processedCount * progressStep);

                    await Task.Delay(150);
                }

                // 3. Также ищем в базе по названию (на случай пропусков)
                SearchProgressBar.Value = 95;
                var additionalMoviesFromDb = _databaseService.SearchMoviesInDatabase(searchTitle, userId)
                    .Where(m => !string.IsNullOrEmpty(m.Poster) && m.Poster != "null")
                    .Where(m => m.VoteCount >= 100) // ФИЛЬТР: минимум 100 оценок
                    .Where(m => !finalMovies.Any(fm =>
                        string.Equals(fm.Title, m.Title, StringComparison.OrdinalIgnoreCase) &&
                        fm.Year == m.Year))
                    .OrderByDescending(m => m.VoteCount)
                    .ThenByDescending(m => m.Rating)
                    .Take(15)
                    .ToList();

                if (additionalMoviesFromDb.Any())
                {
                    finalMovies.AddRange(additionalMoviesFromDb);
                }

                // 4. Сортируем по популярности
                finalMovies = finalMovies
                    .OrderByDescending(m => m.VoteCount)
                    .ThenByDescending(m => m.Rating)
                    .ThenByDescending(m => m.Year)
                    .ToList();

                SearchProgressBar.Value = 100;

                if (!finalMovies.Any())
                {
                    MessageBox.Show("Фильмы не найдены. Попробуйте другой запрос или снизьте требования к популярности.");
                    return;
                }

                Console.WriteLine($"🎬 Итоговый список: {finalMovies.Count} фильмов (минимум 100 оценок каждый)");
                DisplayMovies(finalMovies);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                SetProgressStatus(false);
                SearchProgressBar.Value = 0;
            }
        }

        private void DisplayMovies(List<Movie> movies)
        {
            MoviesPanel.Children.Clear();

            if (movies == null || !movies.Any())
            {
                var noResultsText = new TextBlock
                {
                    Text = "Фильмы не найдены",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16,
                    Foreground = Brushes.Gray
                };
                MoviesPanel.Children.Add(noResultsText);
                return;
            }

            // Показываем фильмы в том порядке, в котором они пришли (уже отсортированные)
            foreach (var movie in movies)
            {
                var movieButton = CreateMovieButton(movie);
                MoviesPanel.Children.Add(movieButton);
            }
        }

        private Button CreateMovieButton(Movie movie)
        {
            Brush backgroundColor;
            Brush borderColor;

            bool isInWatchList = CurrentUser != null &&
                                 _databaseService.IsInWatchList(CurrentUser.Id, movie.Slug);
            bool isWatched = CurrentUser != null &&
                             _databaseService.IsMovieWatched(CurrentUser.Id, movie.Slug);

            movie.IsWatched = isWatched;
            movie.InWatchList = isInWatchList;

            if (isWatched && isInWatchList)
            {
                backgroundColor = Brushes.LightCoral;
                borderColor = Brushes.Red;
            }
            else if (isInWatchList)
            {
                backgroundColor = Brushes.LightYellow;
                borderColor = Brushes.Orange;
            }
            else if (isWatched)
            {
                backgroundColor = Brushes.LightGreen;
                borderColor = Brushes.Green;
            }
            else
            {
                backgroundColor = Brushes.White;
                borderColor = Brushes.LightGray;
            }

            var button = new Button
            {
                Margin = new Thickness(10),
                Padding = new Thickness(0),
                Background = backgroundColor,
                BorderBrush = borderColor,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                Width = 160,
                Height = 280,
                ToolTip = $"{movie.Title}\nРейтинг: {movie.Rating:F1}/10\nОценок: {movie.FormatVoteCount(movie.VoteCount)}\nГод: {movie.Year}"
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var posterContainer = new Border
            {
                Width = 140,
                Height = 200,
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Child = CreatePosterImage(movie)
            };
            stackPanel.Children.Add(posterContainer);

            if (isWatched && isInWatchList)
            {
                var statusIcon = new TextBlock
                {
                    Text = "⚠️ Несоответствие",
                    FontSize = 9,
                    Foreground = Brushes.Red,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    FontWeight = FontWeights.Bold
                };
                stackPanel.Children.Add(statusIcon);
            }
            else if (isWatched)
            {
                var watchedIcon = new TextBlock
                {
                    Text = "✓ Просмотрено",
                    FontSize = 10,
                    Foreground = Brushes.Green,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    FontWeight = FontWeights.Bold
                };
                stackPanel.Children.Add(watchedIcon);
            }
            else if (isInWatchList)
            {
                var watchlistIcon = new TextBlock
                {
                    Text = "📋 В списке",
                    FontSize = 10,
                    Foreground = Brushes.Orange,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    FontWeight = FontWeights.Bold
                };
                stackPanel.Children.Add(watchlistIcon);
            }

            var textContainer = new StackPanel
            {
                Margin = new Thickness(5, 8, 5, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 140
            };

            var titleText = new TextBlock
            {
                Text = $"{movie.Title} ({movie.Year})",
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = Brushes.Black,
                MaxHeight = 35
            };
            textContainer.Children.Add(titleText);

            // РЕЙТИНГ И ПОПУЛЯРНОСТЬ
            var ratingStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };

            var starIcon = new TextBlock
            {
                Text = "★",
                FontSize = 11,
                Foreground = Brushes.Gold,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 2, 0)
            };

            var ratingText = new TextBlock
            {
                Text = $"{movie.Rating:F1}",
                FontSize = 11,
                Foreground = Brushes.Gold,
                FontWeight = FontWeights.Bold
            };

            var votesText = new TextBlock
            {
                Text = $" ({movie.FormatVoteCount(movie.VoteCount)})",
                FontSize = 10,
                Foreground = Brushes.Gray
            };

            ratingStack.Children.Add(starIcon);
            ratingStack.Children.Add(ratingText);
            ratingStack.Children.Add(votesText);
            textContainer.Children.Add(ratingStack);

            if (movie.Genres != null && movie.Genres.Any())
            {
                var genresText = new TextBlock
                {
                    Text = string.Join(", ", movie.Genres.Take(2)),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 3, 0, 0),
                    MaxHeight = 30
                };
                textContainer.Children.Add(genresText);
            }

            stackPanel.Children.Add(textContainer);
            button.Content = stackPanel;
            button.Click += (s, e) => ShowMovieDetails(movie);

            return button;
        }

        private Image CreatePosterImage(Movie movie)
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(movie.Poster))
            {
                try
                {
                    var posterService = new MoviePosterService();
                    var bitmap = posterService.Base64ToBitmapImage(movie.Poster);
                    if (bitmap != null)
                    {
                        image.Source = bitmap;
                        return image;
                    }
                }
                catch
                {
                    // Если ошибка - показываем заглушку
                }
            }

            // Заглушка
            image.Source = CreatePlaceholderImage();
            return image;
        }

        private ImageSource CreatePlaceholderImage()
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Серый фон
                context.DrawRectangle(Brushes.LightGray, new Pen(Brushes.Gray, 1),
                    new Rect(0, 0, 140, 200));

                // Текст
                var text = new FormattedText(
                    "Нет изображения",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    12,
                    Brushes.Gray,
                    1.0
                );

                // Центрируем текст
                double x = (140 - text.Width) / 2;
                double y = (200 - text.Height) / 2;
                context.DrawText(text, new Point(x, y));
            }

            var bitmap = new RenderTargetBitmap(140, 200, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            return bitmap;
        }

        private void ShowMovieDetails(Movie movie)
        {
            // Передаем userId текущего пользователя в MovieInfo
            var movieInfoPage = new MovieInfo(movie, CurrentUser?.Id ?? 0);

            var movieInfoWindow = new Window
            {
                Content = movieInfoPage,
                Title = $"{movie.Title} ({movie.Year})",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            movieInfoWindow.ShowDialog();
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Введите название...")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = Brushes.Black;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Введите название...";
                SearchTextBox.Foreground = Brushes.Gray;
            }
        }

        private void SetProgressStatus(bool isProgress)
        {
            if (isProgress)
            {
                SearchProgressBar.Visibility = Visibility.Visible;
                SearchButton.IsEnabled = false;
            }
            else
            {
                SearchProgressBar.Visibility = Visibility.Hidden;
                SearchButton.IsEnabled = true;
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginPage = new Login(this);
            loginPage.ShowDialog();
        }

        private Brush GetMovieBackground(Movie movie)
        {
            // Проверяем, находится ли фильм в WatchList (используем метод из DatabaseService)
            bool isInWatchList = CurrentUser != null &&
                                 _databaseService.IsInWatchList(CurrentUser.Id, movie.Slug);

            if (movie.IsWatched && isInWatchList)
            {
                return Brushes.LightCoral; // Красный - просмотрен и хочет пересмотреть
            }
            else if (isInWatchList)
            {
                return Brushes.LightYellow; // Желтый - в WatchList
            }
            else if (movie.IsWatched)
            {
                return Brushes.LightGreen; // Зеленый - просмотрен
            }
            else
            {
                return Brushes.White; // Белый - обычный фильм
            }
        }

        private Brush GetMovieBorderColor(Movie movie)
        {
            bool isInWatchList = CurrentUser != null &&
                                 _databaseService.IsInWatchList(CurrentUser.Id, movie.Slug);

            if (movie.IsWatched && isInWatchList)
            {
                return Brushes.Red; // Красная рамка
            }
            else if (isInWatchList)
            {
                return Brushes.Orange; // Оранжевая рамка
            }
            else if (movie.IsWatched)
            {
                return Brushes.Green; // Зеленая рамка
            }
            else
            {
                return Brushes.LightGray; // Серая рамка
            }
        }

        public void RefreshUserData()
        {
            if (CurrentUser != null)
            {
                try
                {
                    var updatedUser = _databaseService.GetUserByLogin(CurrentUser.Login);

                    if (updatedUser != null)
                    {
                        CurrentUser = updatedUser;

                        UpdateUserButton();

                        if (!string.IsNullOrWhiteSpace(SearchTextBox.Text) &&
                            SearchTextBox.Text != "Введите название...")
                        {
                            SearchButton_Click(null, null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error refreshing user data: {ex.Message}");
                }
            }
        }

        private void AutoLogin()
        {
            try
            {
                var settings = SettingsManager.LoadSettings();

                if (settings.RememberMe && !string.IsNullOrEmpty(settings.LastLogin))
                {
                    var user = _databaseService.FindUserByLogin(settings.LastLogin);

                    if (user != null)
                    {
                        LoginUser(user);
                        Console.WriteLine($"Автоматический вход выполнен для: {user.Login}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка автоматического входа: {ex.Message}");
            }
        }

        private void RecommendationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLogged || CurrentUser == null)
            {
                MessageBox.Show("Для получения рекомендаций необходимо войти в систему",
                               "Требуется вход",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);

                // Показываем окно входа
                var loginWindow = new Login(this)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                loginWindow.ShowDialog();
                return;
            }

            try
            {
                var recommendationsWindow = new RecommendationsWindow(CurrentUser)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                recommendationsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия рекомендаций: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
    }
}