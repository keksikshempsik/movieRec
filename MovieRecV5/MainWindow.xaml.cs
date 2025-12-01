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

        // Метод для обновления текста кнопки
        public void UpdateUserButton()
        {
            if (IsLogged && CurrentUser != null)
            {
                UserProfileButton.Content = "Профиль";
                UserProfileButton.ToolTip = $"Войти в профиль ({CurrentUser.Login})";
            }
            else
            {
                UserProfileButton.Content = "Вход/Регистрация";
                UserProfileButton.ToolTip = "Войти или зарегистрироваться";
            }
        }

        // Метод для входа пользователя
        public void LoginUser(User user)
        {
            CurrentUser = user;
            IsLogged = true;
            UpdateUserButton();
        }

        // Метод для выхода пользователя
        public void LogoutUser()
        {
            CurrentUser = null;
            IsLogged = false;
            UpdateUserButton();
        }

        // ОБНОВЛЕННЫЙ ОБРАБОТЧИК КНОПКИ - ТЕПЕРЬ СРАЗУ ОТКРЫВАЕТ ПРОФИЛЬ ПРИ ВХОДЕ
        private void UserProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsLogged && CurrentUser != null)
            {
                // Показываем окно профиля
                var profileWindow = new UserProfileWindow(CurrentUser, this)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                profileWindow.ShowDialog();
            }
            else
            {
                // Показываем окно входа/регистрации
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
                LetterboxdParser parser = new LetterboxdParser();

                if (SearchTextBox.Text == "Введите название..." || string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    MessageBox.Show("Введите название фильма");
                    return;
                }

                string searchTitle = SearchTextBox.Text.Trim();
                Console.WriteLine($"🔍 Поиск: '{searchTitle}'");

                // Передаем ID текущего пользователя при поиске в базе
                int userId = CurrentUser?.Id ?? 0;
                var moviesFromDb = _databaseService.GetMoviesFromDatabase(searchTitle, userId);

                if (moviesFromDb.Count >= 4)
                {
                    Console.WriteLine($"📁 Найдено в базе: {moviesFromDb.Count} фильмов");
                    var sortedMovies = moviesFromDb
                        .OrderByDescending(m => m.VoteCount)
                        .Take(8)
                        .ToList();
                    DisplayMovies(sortedMovies);
                    return;
                }

                List<Movie> movies = new List<Movie>(moviesFromDb);

                // 2. Ограниченный онлайн-поиск
                var baseSlug = parser.ConvertToSlug(searchTitle);
                var possibleSlugs = GenerateSlugsWithYears(baseSlug).Take(15);
                Console.WriteLine($"🔄 Поиск {possibleSlugs.Count()} slugs");

                var onlineTasks = new List<Task<Movie>>();

                foreach (var slug in possibleSlugs)
                {
                    if (movies.Any(m => m.Slug == slug)) continue;

                    onlineTasks.Add(TryGetMovieWithThrottle(slug, parser));
                }

                var onlineResults = await Task.WhenAll(onlineTasks);
                var newMovies = onlineResults.Where(m => m != null).ToList();

                // Сохраняем найденные фильмы в базу
                foreach (var movie in newMovies)
                {
                    if (!_databaseService.MovieExists(movie.Slug))
                    {
                        _databaseService.AddMovie(movie);
                    }
                }

                movies.AddRange(newMovies);

                // 3. Проверяем для всех фильмов, просмотрены ли они текущим пользователем
                foreach (var movie in movies)
                {
                    if (userId > 0)
                    {
                        movie.IsWatched = _databaseService.IsMovieWatched(userId, movie.Slug);
                    }
                }

                // 4. Показываем результаты
                if (!movies.Any())
                {
                    MessageBox.Show("Фильмы не найдены.");
                    return;
                }

                var finalMovies = movies
                    .GroupBy(m => m.Slug)
                    .Select(g => g.First())
                    .OrderByDescending(m => m.VoteCount)
                    .ThenByDescending(m => m.Year)
                    .Take(8)
                    .ToList();

                Console.WriteLine($"🎬 Найдено: {finalMovies.Count} фильмов");
                DisplayMovies(finalMovies);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                SetProgressStatus(false);
            }
        }

        private async Task<Movie> TryGetMovieWithThrottle(string slug, LetterboxdParser parser)
        {
            await _throttler.WaitAsync();
            try
            {
                Console.WriteLine($"🌐 Парсим: {slug}");
                return await parser.TryParseMovie(slug);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка {slug}: {ex.Message}");
                return null;
            }
            finally
            {
                _throttler.Release();
            }
        }

        private List<string> GenerateSlugsWithYears(string baseSlug)
        {
            var slugs = new List<string> { baseSlug };

            var keyYears = new[] {
                2025, 2024, 2023, 2022, 2021, 
                2019, 2018, 2017, 2016, 2015,
                2012, 2010, 2008, 2005, 2000,
                1999, 1998, 1995, 1990, 2020,
                1980, 1982, 1970, 1960, 1950
            };

            foreach (var year in keyYears)
            {
                slugs.Add($"{baseSlug}-{year}");
            }

            return slugs.Distinct().ToList();
        }

        private void DisplayMovies(List<Movie> movies)
        {
            MoviesPanel.Children.Clear(); // Изменил с MoviesGrid на MoviesPanel

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

            foreach (var movie in movies.Take(8))
            {
                var movieButton = CreateMovieButton(movie);
                MoviesPanel.Children.Add(movieButton);
            }
        }

        private Button CreateMovieButton(Movie movie)
        {
            var button = new Button
            {
                Margin = new Thickness(10),
                Padding = new Thickness(0),
                Background = movie.IsWatched ? Brushes.LightGreen : Brushes.White,
                BorderBrush = movie.IsWatched ? Brushes.Green : Brushes.LightGray,
                BorderThickness = new Thickness(movie.IsWatched ? 2 : 1),
                Cursor = Cursors.Hand,
                Width = 160,
                Height = 280
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Контейнер для постера
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

            // Иконка просмотра (если фильм просмотрен)
            if (movie.IsWatched)
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

            // Текстовая информация
            var textContainer = new StackPanel
            {
                Margin = new Thickness(5, 8, 5, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 140
            };

            // Название и год
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

            // Рейтинг
            var ratingText = new TextBlock
            {
                Text = $"★ {movie.Rating:F1}/10",
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Foreground = Brushes.Gold,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 3, 0, 0)
            };
            textContainer.Children.Add(ratingText);

            // Жанры
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
    }
}