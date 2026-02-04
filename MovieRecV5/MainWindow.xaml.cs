using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using MovieRecV5.Models;
using MovieRecV5.Services;
using MovieRecV5.ViewModels;

namespace MovieRecV5
{
    public partial class MainWindow : Window
    {
        private DatabaseService _databaseService;
        private readonly SemaphoreSlim _throttler;
        public bool IsLogged { get; private set; }
        public User CurrentUser { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            IsLogged = false;
            CurrentUser = null;
            _databaseService = new DatabaseService();
            _databaseService.InitializeDatabase();
            _throttler = new SemaphoreSlim(3, 3);

            SearchTextBox.KeyDown += SearchTextBox_KeyDown;
            UpdateUserButton();
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
            }
            else
            {
                UserProfileButton.Content = "Вход/Регистрация";
                UserProfileButton.ToolTip = "Войти или зарегистрироваться";
                UserProfileButton.FontWeight = FontWeights.Normal;
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

                if (SearchTextBox.Text == "Введите название..." || string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    MessageBox.Show("Введите название фильма");
                    return;
                }

                string searchTitle = SearchTextBox.Text.Trim();
                Console.WriteLine($"🔍 Поиск: '{searchTitle}'");

                // 1. ВСЕГДА ищем сначала в базе данных
                int userId = CurrentUser?.Id ?? 0;
                var moviesFromDb = _databaseService.GetMoviesFromDatabase(searchTitle, userId);

                // Фильтруем фильмы без постера
                moviesFromDb = moviesFromDb
                    .Where(m => !string.IsNullOrEmpty(m.Poster) && m.Poster != "null")
                    .ToList();

                Console.WriteLine($"📁 Найдено в базе: {moviesFromDb.Count} фильмов (с постерами)");

                List<Movie> finalMovies = new List<Movie>();

                if (moviesFromDb.Count >= 8)
                {
                    // СОРТИРУЕМ ОТ НАИБОЛЕЕ ПОПУЛЯРНОГО К НАИМЕНЕЕ
                    finalMovies = moviesFromDb
                        .OrderByDescending(m => m.VoteCount) // По количеству голосов (самый популярный показатель)
                        .ThenByDescending(m => m.Rating)     // Затем по рейтингу
                        .Take(8)
                        .ToList();

                    Console.WriteLine($"✅ Используем только фильмы из базы: {finalMovies.Count}");
                }
                else if (moviesFromDb.Count > 0)
                {
                    // Добавляем фильмы из базы (уже отсортированные по популярности)
                    finalMovies.AddRange(moviesFromDb
                        .OrderByDescending(m => m.VoteCount)
                        .ThenByDescending(m => m.Rating)
                        .ToList());

                    // Ищем недостающие фильмы в TMDB
                    TmdbParser parser = new TmdbParser();
                    var onlineMovies = await parser.SearchAllMovies(searchTitle, null);

                    if (onlineMovies.Any())
                    {
                        // Фильтруем те фильмы, которых нет в базе
                        var newMovies = onlineMovies
                            .Where(onlineMovie => !moviesFromDb.Any(dbMovie =>
                                string.Equals(dbMovie.Title, onlineMovie.Title, StringComparison.OrdinalIgnoreCase) &&
                                dbMovie.Year == onlineMovie.Year))
                            .ToList();

                        Console.WriteLine($"🌐 Найдено новых в TMDB: {newMovies.Count} фильмов");

                        // СОРТИРУЕМ НОВЫЕ ФИЛЬМЫ ОТ НАИБОЛЕЕ ПОПУЛЯРНОГО
                        var sortedNewMovies = newMovies
                            .OrderByDescending(m => m.VoteCount)
                            .ThenByDescending(m => m.Rating)
                            .Take(8 - moviesFromDb.Count) // Берем только недостающее количество
                            .ToList();

                        // Сохраняем новые фильмы в базу
                        foreach (var movie in sortedNewMovies)
                        {
                            if (!_databaseService.MovieExists(movie.Slug))
                            {
                                _databaseService.AddMovie(movie);
                            }
                        }

                        // Добавляем новые фильмы к результатам (они уже отсортированы)
                        finalMovies.AddRange(sortedNewMovies);
                    }
                }
                else
                {
                    // Ищем все в TMDB
                    TmdbParser parser = new TmdbParser();
                    var onlineMovies = await parser.SearchAllMovies(searchTitle, null);

                    if (onlineMovies.Any())
                    {
                        // СОРТИРУЕМ ОТ НАИБОЛЕЕ ПОПУЛЯРНОГО К НАИМЕНЕЕ
                        var sortedMovies = onlineMovies
                            .OrderByDescending(m => m.VoteCount)
                            .ThenByDescending(m => m.Rating)
                            .Take(8)
                            .ToList();

                        // Сохраняем все найденные фильмы в базу
                        foreach (var movie in sortedMovies)
                        {
                            if (!_databaseService.MovieExists(movie.Slug))
                            {
                                _databaseService.AddMovie(movie);
                            }
                        }

                        finalMovies.AddRange(sortedMovies);
                        Console.WriteLine($"🌐 Используем только фильмы из TMDB: {finalMovies.Count}");
                    }
                }

                // СОРТИРУЕМ ИТОГОВЫЙ СПИСОК (на всякий случай)
                finalMovies = finalMovies
                    .OrderByDescending(m => m.VoteCount)
                    .ThenByDescending(m => m.Rating)
                    .ToList();

                // Проверяем для всех фильмов, просмотрены ли они текущим пользователем
                foreach (var movie in finalMovies)
                {
                    if (userId > 0)
                    {
                        movie.IsWatched = _databaseService.IsMovieWatched(userId, movie.Slug);
                        movie.InWatchList = _databaseService.IsInWatchList(userId, movie.Slug);
                    }
                }

                // Показываем результаты
                if (!finalMovies.Any())
                {
                    MessageBox.Show("Фильмы не найдены.");
                    return;
                }

                Console.WriteLine($"🎬 Итоговый список: {finalMovies.Count} фильмов, отсортирован по популярности");
                DisplayMovies(finalMovies);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
                Console.WriteLine($"❌ Ошибка поиска: {ex}");
            }
            finally
            {
                SetProgressStatus(false);
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
            SearchProgressBar.IsIndeterminate = isProgress;
            SearchButton.IsEnabled = !isProgress;
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
                    // Получаем обновленные данные пользователя из базы
                    var updatedUser = _databaseService.GetUserByLogin(CurrentUser.Login);

                    if (updatedUser != null)
                    {
                        // Обновляем текущего пользователя
                        CurrentUser = updatedUser;

                        // Обновляем кнопку профиля
                        UpdateUserButton();

                        // Перерисовываем фильмы, чтобы обновить цвета
                        if (!string.IsNullOrWhiteSpace(SearchTextBox.Text) &&
                            SearchTextBox.Text != "Введите название...")
                        {
                            // Повторяем поиск, чтобы обновить отображение фильмов
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
    }
}