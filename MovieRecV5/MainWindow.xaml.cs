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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace MovieRecV5
{
    public partial class MainWindow : Window
    {
        private PostgresDatabaseService _databaseService;
        private TmdbParser _tmdbParser;
        private Dictionary<string, Tuple<DateTime, List<Movie>>> _searchCache = new Dictionary<string, Tuple<DateTime, List<Movie>>>();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public bool IsLogged { get; private set; }
        public User CurrentUser { get; private set; }

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                IsLogged = false;
                CurrentUser = null;

                _databaseService = new PostgresDatabaseService();
                _tmdbParser = new TmdbParser();

                _databaseService.InitializeDatabase();

                SearchTextBox.KeyDown += SearchTextBox_KeyDown;

                AutoLogin();
                UpdateUserButton();

                SearchTextBox.GotFocus += SearchTextBox_GotFocus;
                SearchTextBox.LostFocus += SearchTextBox_LostFocus;

                SearchProgressBar.Visibility = Visibility.Hidden;

                Loaded += async (s, e) => await LoadPopularMoviesAsync();
            }
            catch (Exception ex)
            {
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

                Application.Current.Shutdown();
            }
        }

        // ===== МЕТОДЫ ЗАГРУЗКИ ПОПУЛЯРНЫХ ФИЛЬМОВ =====

        private async Task LoadPopularMoviesAsync()
        {
            try
            {
                SetProgressStatus(true);
                SearchProgressBar.IsIndeterminate = true;

                int userId = CurrentUser?.Id ?? 0;
                List<Movie> popularMovies = new List<Movie>();

                Console.WriteLine("🌐 Загружаем популярные фильмы из TMDB API...");

                popularMovies = await _tmdbParser.GetPopularMoviesAsync(1, 50, 30);

                Console.WriteLine($"✅ Загружено {popularMovies.Count} популярных фильмов из TMDB");

                foreach (var movie in popularMovies)
                {
                    try
                    {
                        if (!_databaseService.MovieExists(movie.Slug))
                        {
                            _databaseService.AddMovie(movie);
                            Console.WriteLine($"📁 Сохранен в БД: {movie.Title} ({movie.Year})");
                        }

                        if (userId > 0)
                        {
                            movie.IsWatched = _databaseService.IsMovieWatched(userId, movie.Slug);
                            movie.InWatchList = _databaseService.IsInWatchList(userId, movie.Slug);
                            movie.IsFavorite = _databaseService.IsInFavorites(userId, movie.Slug);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Ошибка при сохранении фильма {movie.Title}: {ex.Message}");
                    }

                    await Task.Delay(50);
                }

                if (popularMovies.Any())
                {
                    SearchProgressBar.Value = 100;
                    DisplayMovies(popularMovies);

                    SearchTextBox.Text = "Введите название...";
                    SearchTextBox.Foreground = Brushes.Gray;
                }
                else
                {
                    MoviesPanel.Children.Clear();
                    var noMoviesText = new TextBlock
                    {
                        Text = "Не удалось загрузить популярные фильмы из TMDB.\nПопробуйте выполнить поиск.",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 14,
                        Foreground = Brushes.Gray,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(20)
                    };
                    MoviesPanel.Children.Add(noMoviesText);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при загрузке популярных фильмов: {ex.Message}");

                MoviesPanel.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = $"Ошибка загрузки популярных фильмов из TMDB.\n{ex.Message}\n\nПопробуйте выполнить поиск.",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    Foreground = Brushes.Red,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(20)
                };
                MoviesPanel.Children.Add(errorText);
            }
            finally
            {
                SetProgressStatus(false);
                SearchProgressBar.IsIndeterminate = false;
                SearchProgressBar.Value = 0;
            }
        }

        // ===== МЕТОДЫ ПОИСКА =====

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetProgressStatus(true);
                SearchProgressBar.IsIndeterminate = true;

                if (SearchTextBox.Text == "Введите название..." || string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    await LoadPopularMoviesAsync();
                    return;
                }

                string searchTitle = SearchTextBox.Text.Trim();
                Console.WriteLine($"🔍 Поиск: '{searchTitle}'");

                var results = await GetSearchResultsWithCache(searchTitle, CurrentUser?.Id ?? 0);

                SearchProgressBar.Value = 100;
                DisplayMovies(results);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                SetProgressStatus(false);
                SearchProgressBar.IsIndeterminate = false;
                SearchProgressBar.Value = 0;
            }
        }

        private async Task<List<Movie>> GetSearchResultsWithCache(string searchQuery, int userId)
        {
            string cacheKey = $"{searchQuery}_{userId}";

            if (_searchCache.ContainsKey(cacheKey))
            {
                var cached = _searchCache[cacheKey];
                if (DateTime.Now - cached.Item1 < _cacheDuration)
                {
                    Console.WriteLine($"📦 Используем кэш для '{searchQuery}'");
                    return cached.Item2;
                }
                else
                {
                    _searchCache.Remove(cacheKey);
                }
            }

            var results = await PerformSearch(searchQuery, userId);
            _searchCache[cacheKey] = Tuple.Create(DateTime.Now, results);

            return results;
        }

        private async Task<List<Movie>> PerformSearch(string searchTitle, int userId)
        {
            var movies = new List<Movie>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 1. Поиск в БД
            SearchProgressBar.Value = 30;
            Console.WriteLine("📁 Поиск в базе данных...");

            var dbMovies = _databaseService.SearchMoviesInDatabaseFast(searchTitle, userId, 30);
            movies.AddRange(dbMovies);
            Console.WriteLine($"   Найдено в БД: {dbMovies.Count} фильмов");

            // 2. Если мало, ищем в TMDB
            if (movies.Count < 20)
            {
                int neededCount = 20 - movies.Count;
                Console.WriteLine($"🌐 Нужно еще {neededCount} фильмов, ищем в TMDB...");

                SearchProgressBar.Value = 60;

                var tmdbIds = await _tmdbParser.SearchMoviesFastOptimized(searchTitle, neededCount * 2, 50);
                Console.WriteLine($"   Найдено в TMDB: {tmdbIds.Count} ID фильмов");

                SearchProgressBar.Value = 80;

                // Загружаем детали параллельно
                var downloadTasks = new List<Task<Movie>>();

                foreach (var tmdbMovie in tmdbIds)
                {
                    if (movies.Any(m => m.Id == tmdbMovie.Id)) continue;

                    downloadTasks.Add(DownloadMovieDetailsAsync(tmdbMovie, userId));

                    if (downloadTasks.Count >= 3)
                    {
                        var completed = await Task.WhenAny(downloadTasks);
                        downloadTasks.Remove(completed);
                        var movie = await completed;
                        if (movie != null) movies.Add(movie);
                    }
                }

                while (downloadTasks.Any())
                {
                    var completed = await Task.WhenAny(downloadTasks);
                    downloadTasks.Remove(completed);
                    var movie = await completed;
                    if (movie != null) movies.Add(movie);
                }
            }

            // 3. Сортировка
            movies = movies
                .Where(m => !string.IsNullOrEmpty(m.Poster))
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .OrderByDescending(m => GetRelevanceScore(m, searchTitle))
                .ThenByDescending(m => m.VoteCount)
                .ThenByDescending(m => m.Rating)
                .Take(40)
                .ToList();

            stopwatch.Stop();
            Console.WriteLine($"✅ Поиск завершен за {stopwatch.ElapsedMilliseconds} мс");
            Console.WriteLine($"🎬 Найдено фильмов: {movies.Count}");

            return movies;
        }

        private async Task<Movie> DownloadMovieDetailsAsync(Movie fastResult, int userId)
        {
            try
            {
                var fullMovie = await _tmdbParser.GetMovieByTmdbId(fastResult.Id);

                if (fullMovie != null && !string.IsNullOrEmpty(fullMovie.Poster))
                {
                    fullMovie.Id = fastResult.Id;

                    _ = Task.Run(() => {
                        try
                        {
                            if (!_databaseService.MovieExists(fullMovie.Slug))
                            {
                                _databaseService.AddMovie(fullMovie);
                            }
                        }
                        catch { }
                    });

                    if (userId > 0)
                    {
                        fullMovie.IsWatched = _databaseService.IsMovieWatched(userId, fullMovie.Slug);
                        fullMovie.InWatchList = _databaseService.IsInWatchList(userId, fullMovie.Slug);
                        fullMovie.IsFavorite = _databaseService.IsInFavorites(userId, fullMovie.Slug);
                    }

                    return fullMovie;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка загрузки {fastResult.Title}: {ex.Message}");
            }

            return null;
        }

        private double GetRelevanceScore(Movie movie, string searchQuery)
        {
            double score = 0;

            if (string.Equals(movie.Title, searchQuery, StringComparison.OrdinalIgnoreCase))
                score += 100;
            else if (movie.Title.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase))
                score += 50;
            else if (movie.Title.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                score += 30;

            score += Math.Log10(movie.VoteCount + 1) * 2;
            score += movie.Rating;

            return score;
        }

        // ===== МЕТОДЫ ОТОБРАЖЕНИЯ =====

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
                    Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)),
                    Margin = new Thickness(20)
                };
                MoviesPanel.Children.Add(noResultsText);
                return;
            }

            foreach (var movie in movies)
            {
                var movieButton = MovieCardHelper.CreateMovieCard(
                    movie,
                    CurrentUser?.Id ?? 0,
                    _databaseService,
                    ShowMovieDetails
                );
                MoviesPanel.Children.Add(movieButton);
            }
        }

        private Button CreateMovieButton(Movie movie)
        {
            bool isInWatchList = CurrentUser != null && _databaseService.IsInWatchList(CurrentUser.Id, movie.Slug);
            bool isWatched = CurrentUser != null && _databaseService.IsMovieWatched(CurrentUser.Id, movie.Slug);
            bool isFavorite = CurrentUser != null && _databaseService.IsInFavorites(CurrentUser.Id, movie.Slug);

            movie.IsWatched = isWatched;
            movie.InWatchList = isInWatchList;
            movie.IsFavorite = isFavorite;

            Brush backgroundColor;
            if (isWatched && isInWatchList)
                backgroundColor = Brushes.LightCoral;
            else if (isFavorite)
                backgroundColor = Brushes.LightPink;
            else if (isInWatchList)
                backgroundColor = Brushes.LightYellow;
            else if (isWatched)
                backgroundColor = Brushes.LightGreen;
            else
                backgroundColor = Brushes.White;

            var button = new Button
            {
                Style = (Style)FindResource("MovieCardStyle"),
                Width = 180,
                Height = 320,
                Margin = new Thickness(10),
                ToolTip = $"{movie.Title}\n★ {movie.Rating:F1}/10 • {movie.FormatVoteCount(movie.VoteCount)} оценок"
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(10)
            };

            var stackPanel = new StackPanel();

            // Постер с эффектом тени
            var posterBorder = new Border
            {
                Width = 160,
                Height = 220,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var posterImage = CreatePosterImage(movie);
            posterImage.Width = 160;
            posterImage.Height = 220;
            posterBorder.Child = posterImage;

            posterBorder.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 10,
                Opacity = 0.3
            };

            stackPanel.Children.Add(posterBorder);

            // Иконки статусов
            var statusPanel = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            if (isWatched)
                statusPanel.Children.Add(CreateStatusBadge("✓", "#00B894"));
            if (isInWatchList)
                statusPanel.Children.Add(CreateStatusBadge("📋", "#FDCB6E"));
            if (isFavorite)
                statusPanel.Children.Add(CreateStatusBadge("❤️", "#E17055"));

            stackPanel.Children.Add(statusPanel);

            // Название
            stackPanel.Children.Add(new TextBlock
            {
                Text = movie.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxHeight = 40,
                Margin = new Thickness(0, 5, 0, 3)
            });

            // Рейтинг
            var ratingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            ratingPanel.Children.Add(new TextBlock
            {
                Text = "★",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
                FontSize = 14,
                Margin = new Thickness(0, 0, 3, 0)
            });

            ratingPanel.Children.Add(new TextBlock
            {
                Text = $"{movie.Rating:F1}/10",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            });

            stackPanel.Children.Add(ratingPanel);

            border.Child = stackPanel;
            button.Content = border;
            button.Click += (s, e) => ShowMovieDetails(movie);

            return button;
        }

        private Border CreateStatusBadge(string icon, string color)
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(2),
                Child = new TextBlock
                {
                    Text = icon,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };
        }

        private Image CreatePosterImage(Movie movie)
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(movie.Poster) && movie.Poster != "null")
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
                catch { }
            }

            image.Source = CreatePlaceholderImage();
            return image;
        }

        private ImageSource CreatePlaceholderImage()
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(Brushes.LightGray, new Pen(Brushes.Gray, 1),
                    new Rect(0, 0, 140, 200));

                var text = new FormattedText(
                    "Нет изображения",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    12,
                    Brushes.Gray,
                    1.0
                );

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

            // Обновляем кнопки после закрытия
            if (CurrentUser != null)
            {
                RefreshCurrentDisplay();
            }
        }

        private void RefreshCurrentDisplay()
        {
            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text) &&
                SearchTextBox.Text != "Введите название...")
            {
                SearchButton_Click(null, null);
            }
            else
            {
                _ = LoadPopularMoviesAsync();
            }
        }

        // ===== МЕТОДЫ ПОЛЬЗОВАТЕЛЯ =====

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

                RecommendationsButton.IsEnabled = true;
                RecommendationsButton.ToolTip = "Персональные рекомендации фильмов";
            }
            else
            {
                UserProfileButton.Content = "Вход/Регистрация";
                UserProfileButton.ToolTip = "Войти или зарегистрироваться";
                UserProfileButton.FontWeight = FontWeights.Normal;

                RecommendationsButton.IsEnabled = false;
                RecommendationsButton.ToolTip = "Для рекомендаций требуется вход в систему";
            }
        }

        public void LoginUser(User user)
        {
            CurrentUser = user;
            IsLogged = true;
            UpdateUserButton();

            var settings = SettingsManager.LoadSettings();
            settings.WasProperlyClosed = true;
            SettingsManager.SaveSettings(settings);

            Console.WriteLine($"✅ Пользователь {user.Login} вошел в систему");
        }

        public void LogoutUser()
        {
            CurrentUser = null;
            IsLogged = false;
            UpdateUserButton();

            SearchTextBox.Text = "Введите название...";
            SearchTextBox.Foreground = Brushes.Gray;

            _ = LoadPopularMoviesAsync();

            SettingsManager.MarkImproperShutdown();

            Console.WriteLine("👋 Пользователь вышел из аккаунта");
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
                        RefreshCurrentDisplay();
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

                if (settings.RememberMe &&
                    !string.IsNullOrEmpty(settings.LastLogin) &&
                    settings.WasProperlyClosed)
                {
                    var user = _databaseService.FindUserByLogin(settings.LastLogin);

                    if (user != null)
                    {
                        LoginUser(user);
                        Console.WriteLine($"✅ Автоматический вход выполнен для: {user.Login}");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Сохраненный пользователь не найден в БД");
                        SettingsManager.ClearSettings();
                    }
                }
                else
                {
                    Console.WriteLine("ℹ️ Автоматический вход не выполнен");

                    if (!settings.WasProperlyClosed)
                    {
                        SettingsManager.ClearSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка автоматического входа: {ex.Message}");
            }
        }

        // ===== ОБРАБОТЧИКИ СОБЫТИЙ =====

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

        private void RecommendationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLogged || CurrentUser == null)
            {
                MessageBox.Show("Для получения рекомендаций необходимо войти в систему",
                               "Требуется вход",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);

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

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsLogged && CurrentUser != null)
            {
                var settings = SettingsManager.LoadSettings();
                settings.WasProperlyClosed = true;
                SettingsManager.SaveSettings(settings);
                Console.WriteLine("💾 Сохранено состояние: корректное закрытие приложения");
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Этот метод нужен для обновления плейсхолдера через триггеры
            // Тело метода может быть пустым, но метод должен существовать
            // чтобы TextChanged событие работало
        }
    }
}