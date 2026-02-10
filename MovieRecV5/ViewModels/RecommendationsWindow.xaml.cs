using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MovieRecV5.ViewModels
{
    public partial class RecommendationsWindow : Window
    {
        private User _currentUser;
        private PostgresDatabaseService _databaseService;
        private List<Movie> _recommendedMovies;

        public RecommendationsWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            _databaseService = new PostgresDatabaseService();

            // Загружаем рекомендации при открытии окна
            Loaded += async (s, e) => await LoadRecommendationsAsync();
        }

        private async Task LoadRecommendationsAsync()
        {
            try
            {
                // Показываем прогресс
                LoadingProgressBar.Visibility = Visibility.Visible;
                RecommendationsInfoText.Text = "Анализируем ваши предпочтения...";
                StatusText.Text = "Загрузка...";

                // Получаем рекомендации
                _recommendedMovies = await GetRecommendationsAsync();

                // Обновляем интерфейс
                LoadingProgressBar.Visibility = Visibility.Collapsed;

                if (_recommendedMovies.Any())
                {
                    RecommendationsInfoText.Text = $"На основе ваших оценок найдено {_recommendedMovies.Count} рекомендаций";
                    DisplayMovies(_recommendedMovies);
                    NoRecommendationsGrid.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RecommendationsInfoText.Text = "Не удалось найти рекомендации";
                    NoRecommendationsText.Text = GetNoRecommendationsMessage();
                    MoviesPanel.Children.Clear();
                    NoRecommendationsGrid.Visibility = Visibility.Visible;
                }

                StatusText.Text = $"Готово. Найдено {_recommendedMovies.Count} рекомендаций";
            }
            catch (Exception ex)
            {
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                RecommendationsInfoText.Text = "Ошибка при загрузке рекомендаций";
                StatusText.Text = $"Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка загрузки рекомендаций: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<List<Movie>> GetRecommendationsAsync()
        {
            var recommendations = new List<Movie>();

            try
            {
                // 1. Получаем данные пользователя
                var userRatings = await GetUserRatingsAsync();
                var watchedMovies = _databaseService.GetWatchedMovies(_currentUser.Id);

                if (userRatings.Count < 3)
                {
                    // Мало оценок - показываем популярные фильмы
                    return await GetPopularMoviesAsync(20);
                }

                // 2. Анализируем предпочтения
                var favoriteGenres = GetFavoriteGenres(userRatings);
                var favoriteYears = GetFavoriteYears(userRatings);
                var avgRating = userRatings.Average(r => r.Rating);

                // 3. Ищем рекомендации в базе
                recommendations = await FindRecommendedMoviesAsync(favoriteGenres, favoriteYears, avgRating, watchedMovies);

                // 4. Если мало рекомендаций - добавляем популярные
                if (recommendations.Count < 10)
                {
                    var popularMovies = await GetPopularMoviesAsync(15);
                    recommendations.AddRange(popularMovies
                        .Where(p => !recommendations.Any(r => r.Id == p.Id)
                                 && !watchedMovies.Any(w => w.Id == p.Id)));
                }

                // Убираем дубликаты и уже просмотренные
                recommendations = recommendations
                    .Where(m => !watchedMovies.Any(w => w.Id == m.Id))
                    .GroupBy(m => m.Id)
                    .Select(g => g.First())
                    .Take(30) // Ограничиваем количество
                    .ToList();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения рекомендаций: {ex.Message}");
            }

            return recommendations;
        }

        private async Task<List<Movie>> GetPopularMoviesAsync(int count)
        {
            var parser = new TmdbParser();
            return await parser.GetPopularMovies(1);
        }

        private async Task<List<(Movie Movie, int Rating)>> GetUserRatingsAsync()
        {
            var ratings = new List<(Movie, int)>();

            using (var connection = new Npgsql.NpgsqlConnection(_databaseService.GetConnectionString()))
            {
                await connection.OpenAsync();

                var command = new Npgsql.NpgsqlCommand(@"
                    SELECT m.*, ur.rating 
                    FROM user_ratings ur
                    JOIN movies m ON ur.movie_slug = m.slug
                    WHERE ur.user_id = @userId
                    ORDER BY ur.rating DESC", connection);

                command.Parameters.AddWithValue("@userId", _currentUser.Id);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var movie = new Movie
                        {
                            Id = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
                            Title = reader["title"]?.ToString() ?? "",
                            Year = reader["year"] != DBNull.Value ? Convert.ToInt32(reader["year"]) : 0,
                            Rating = reader["rating"] != DBNull.Value ? Convert.ToSingle(reader["rating"]) : 0f,
                            Genres = new List<string>()
                        };

                        string genresJson = reader["genres"]?.ToString();
                        if (!string.IsNullOrEmpty(genresJson))
                        {
                            try
                            {
                                movie.Genres = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(genresJson)
                                            ?? new List<string>();
                            }
                            catch { }
                        }

                        int userRating = Convert.ToInt32(reader["rating"]);
                        ratings.Add((movie, userRating));
                    }
                }
            }

            return ratings;
        }

        private List<string> GetFavoriteGenres(List<(Movie Movie, int Rating)> userRatings)
        {
            var genreCounts = new Dictionary<string, int>();

            foreach (var (movie, rating) in userRatings)
            {
                foreach (var genre in movie.Genres)
                {
                    if (genreCounts.ContainsKey(genre))
                        genreCounts[genre] += rating; // Вес по оценке
                    else
                        genreCounts[genre] = rating;
                }
            }

            return genreCounts
                .OrderByDescending(g => g.Value)
                .Take(3)
                .Select(g => g.Key)
                .ToList();
        }

        private List<int> GetFavoriteYears(List<(Movie Movie, int Rating)> userRatings)
        {
            var yearCounts = new Dictionary<int, int>();

            foreach (var (movie, rating) in userRatings)
            {
                if (movie.Year > 0)
                {
                    if (yearCounts.ContainsKey(movie.Year))
                        yearCounts[movie.Year] += rating;
                    else
                        yearCounts[movie.Year] = rating;
                }
            }

            return yearCounts
                .OrderByDescending(y => y.Value)
                .Take(3)
                .Select(y => y.Key)
                .ToList();
        }

        private async Task<List<Movie>> FindRecommendedMoviesAsync(
            List<string> favoriteGenres,
            List<int> favoriteYears,
            double avgRating,
            List<Movie> watchedMovies)
        {
            var recommendations = new List<Movie>();

            try
            {
                using (var connection = new Npgsql.NpgsqlConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    // Строим запрос на основе предпочтений
                    string query = @"
                        SELECT * FROM movies 
                        WHERE vote_count >= 100 
                          AND rating >= @minRating";

                    var command = new Npgsql.NpgsqlCommand(query, connection);
                    command.Parameters.AddWithValue("@minRating", Math.Max(6.0, avgRating - 1));

                    // Добавляем условия по жанрам если есть
                    if (favoriteGenres.Any())
                    {
                        query += " AND (";
                        for (int i = 0; i < favoriteGenres.Count; i++)
                        {
                            query += $" genres::text LIKE @genre{i} ";
                            if (i < favoriteGenres.Count - 1) query += " OR ";
                            command.Parameters.AddWithValue($"@genre{i}", $"%{favoriteGenres[i]}%");
                        }
                        query += " )";
                    }

                    // Добавляем условия по годам если есть
                    if (favoriteYears.Any())
                    {
                        query += " AND year IN (";
                        for (int i = 0; i < favoriteYears.Count; i++)
                        {
                            query += $"@year{i}";
                            if (i < favoriteYears.Count - 1) query += ", ";
                            command.Parameters.AddWithValue($"@year{i}", favoriteYears[i]);
                        }
                        query += ")";
                    }

                    query += " ORDER BY rating DESC, vote_count DESC LIMIT 50";
                    command.CommandText = query;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var movie = new Movie
                            {
                                Id = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
                                Title = reader["title"]?.ToString() ?? "",
                                Slug = reader["slug"]?.ToString() ?? "",
                                Year = reader["year"] != DBNull.Value ? Convert.ToInt32(reader["year"]) : 0,
                                Description = reader["description"]?.ToString() ?? "",
                                Poster = reader["poster"]?.ToString() ?? "",
                                VoteCount = reader["vote_count"] != DBNull.Value ? Convert.ToInt32(reader["vote_count"]) : 0,
                                Rating = reader["rating"] != DBNull.Value ? Convert.ToSingle(reader["rating"]) : 0f,
                                Genres = new List<string>()
                            };

                            string genresJson = reader["genres"]?.ToString();
                            if (!string.IsNullOrEmpty(genresJson))
                            {
                                try
                                {
                                    movie.Genres = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(genresJson)
                                                ?? new List<string>();
                                }
                                catch { }
                            }

                            // Проверяем, что фильм еще не просмотрен
                            if (!watchedMovies.Any(w => w.Id == movie.Id))
                            {
                                recommendations.Add(movie);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка поиска рекомендаций: {ex.Message}");
            }

            return recommendations;
        }

        private string GetNoRecommendationsMessage()
        {
            var watchedCount = _databaseService.GetWatchedMoviesCount(_currentUser.Id);
            var ratingsCount = _databaseService.GetUserRatingsCount(_currentUser.Id);

            if (ratingsCount < 3)
            {
                return "Оцените хотя бы 3 фильма, чтобы получить персональные рекомендации";
            }
            else if (watchedCount < 5)
            {
                return "Просмотрите больше фильмов для улучшения рекомендаций";
            }
            else
            {
                return "Попробуйте оценить фильмы разных жанров";
            }
        }

        private void DisplayMovies(List<Movie> movies)
        {
            MoviesPanel.Children.Clear();

            foreach (var movie in movies.Take(30)) // Ограничиваем показ
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
                Background = Brushes.White,
                BorderBrush = Brushes.DodgerBlue,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                Width = 160,
                Height = 280,
                ToolTip = $"Рекомендация\n{movie.Title}\n★ {movie.Rating:F1}/10"
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Постер
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

            // Бейдж "Рекомендация"
            var badge = new Border
            {
                Background = Brushes.DodgerBlue,
                Padding = new Thickness(5, 2, 5, 2),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var badgeText = new TextBlock
            {
                Text = "🌟 Рекомендация",
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            badge.Child = badgeText;
            stackPanel.Children.Add(badge);

            // Информация
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

            // Обработчик клика
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
            var movieInfoPage = new MovieInfo(movie, _currentUser.Id);

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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadRecommendationsAsync();
        }

        private void RateMoviesButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}