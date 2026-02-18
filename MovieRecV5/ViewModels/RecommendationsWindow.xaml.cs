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
        private TmdbParser _tmdbParser;
        private List<Movie> _recommendedMovies;

        public RecommendationsWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            _databaseService = new PostgresDatabaseService();
            _tmdbParser = new TmdbParser();

            Loaded += async (s, e) => await LoadRecommendationsAsync();
        }

        private async Task LoadRecommendationsAsync()
        {
            try
            {
                LoadingProgressBar.Visibility = Visibility.Visible;
                RecommendationsInfoText.Text = "Анализируем ваши предпочтения...";
                StatusText.Text = "Загрузка...";

                _recommendedMovies = await GetRecommendationsAsync();

                // Обновляем интерфейс
                LoadingProgressBar.Visibility = Visibility.Collapsed;

                if (_recommendedMovies.Any())
                {
                    RecommendationsInfoText.Text = $"На основе ваших предпочтений найдено {_recommendedMovies.Count} рекомендаций";
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
                var watchListMovies = _databaseService.GetWatchListMovies(_currentUser.Id);
                var favoriteMovies = _databaseService.GetFavoritesMovies(_currentUser.Id);

                // Получаем списки slug для фильтрации
                var watchedMovieSlugs = watchedMovies.Select(m => m.Slug).ToHashSet();
                var watchListMovieSlugs = watchListMovies.Select(m => m.Slug).ToHashSet();
                var favoriteMovieSlugs = favoriteMovies.Select(m => m.Slug).ToHashSet();

                // Объединяем все исключаемые фильмы (просмотренные)
                var excludedSlugs = new HashSet<string>(watchedMovieSlugs);

                Console.WriteLine($"📊 Статистика пользователя:");
                Console.WriteLine($"   Оценок: {userRatings.Count}");
                Console.WriteLine($"   Просмотрено: {watchedMovies.Count}");
                Console.WriteLine($"   В watchlist: {watchListMovies.Count}");
                Console.WriteLine($"   В избранном: {favoriteMovies.Count}");

                // 2. Собираем все фильмы, которые нравятся пользователю
                var likedMovies = new List<Movie>();
                likedMovies.AddRange(userRatings.Select(r => r.Movie));
                likedMovies.AddRange(watchListMovies);
                likedMovies.AddRange(favoriteMovies);

                likedMovies = likedMovies
                    .GroupBy(m => m.Id)
                    .Select(g => g.First())
                    .ToList();

                Console.WriteLine($"   Уникальных понравившихся фильмов: {likedMovies.Count}");

                // 3. Если нет никаких данных - показываем популярные
                if (!likedMovies.Any())
                {
                    Console.WriteLine("⚠️ Нет данных пользователя, показываем популярные фильмы");
                    var popularMovies = await GetPopularMoviesFromTMDB(30);
                    return FilterOutExcludedMovies(popularMovies, excludedSlugs, 30);
                }

                // 4. Анализируем предпочтения
                var genrePreferences = AnalyzeGenrePreferences(likedMovies, userRatings, watchListMovieSlugs, favoriteMovieSlugs);
                var yearPreferences = CalculateYearPreferences(likedMovies, userRatings, watchListMovieSlugs, favoriteMovieSlugs);

                // ПРАВИЛЬНОЕ ПРЕОБРАЗОВАНИЕ - берем только ключи
                var topGenres = genrePreferences
                    .OrderByDescending(g => g.Value)
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList();

                var topYears = yearPreferences
                    .OrderByDescending(y => y.Value)
                    .Take(3)
                    .Select(y => y.Key)
                    .ToList();

                Console.WriteLine($"   Топ жанров: {string.Join(", ", topGenres)}");
                Console.WriteLine($"   Топ годов: {string.Join(", ", topYears)}");

                // 5. Ищем в БД
                recommendations = await SearchInDatabase(
                    topGenres,
                    topYears,
                    excludedSlugs,
                    30
                );

                Console.WriteLine($"   Найдено в БД: {recommendations.Count}");

                // 6. Если мало, ищем похожие на понравившиеся фильмы
                if (recommendations.Count < 15 && likedMovies.Any())
                {
                    Console.WriteLine("🔍 Мало результатов, ищем похожие фильмы...");

                    var similarMovies = await FindSimilarMoviesBatch(likedMovies.Take(5).ToList(), excludedSlugs, 15);

                    foreach (var movie in similarMovies)
                    {
                        if (!recommendations.Any(r => r.Id == movie.Id))
                        {
                            recommendations.Add(movie);
                        }
                    }

                    Console.WriteLine($"   Добавлено похожих: {similarMovies.Count}");
                }

                // 7. Если все еще мало, ищем в TMDB
                if (recommendations.Count < 20)
                {
                    Console.WriteLine("🌐 Мало результатов, ищем в TMDB...");

                    var tmdbMovies = await SearchInTMDB(topGenres, 20 - recommendations.Count);

                    foreach (var movie in tmdbMovies)
                    {
                        if (!recommendations.Any(r => r.Id == movie.Id) && !excludedSlugs.Contains(movie.Slug))
                        {
                            // Сохраняем в БД
                            if (!_databaseService.MovieExists(movie.Slug))
                            {
                                _databaseService.AddMovie(movie);
                            }
                            recommendations.Add(movie);
                        }
                    }
                }

                // 8. Сортируем по релевантности
                recommendations = recommendations
                    .Where(m => !excludedSlugs.Contains(m.Slug))
                    .GroupBy(m => m.Id)
                    .Select(g => g.First())
                    .OrderByDescending(m => CalculateRelevanceScore(m, genrePreferences, yearPreferences, watchListMovieSlugs, favoriteMovieSlugs))
                    .ThenByDescending(m => m.Rating)
                    .ThenByDescending(m => m.VoteCount)
                    .Take(30)
                    .ToList();

                Console.WriteLine($"🎬 Итоговых рекомендаций: {recommendations.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка получения рекомендаций: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            return recommendations;
        }

        // ===== МЕТОДЫ АНАЛИЗА =====

        private Dictionary<string, double> AnalyzeGenrePreferences(
            List<Movie> likedMovies,
            List<(Movie Movie, int Rating)> userRatings,
            HashSet<string> watchListSlugs,
            HashSet<string> favoriteSlugs)
        {
            var genreScores = new Dictionary<string, double>();

            foreach (var movie in likedMovies)
            {
                double baseWeight = 1.0;

                // Вес зависит от источника
                if (userRatings.Any(r => r.Movie.Id == movie.Id))
                {
                    var rating = userRatings.First(r => r.Movie.Id == movie.Id).Rating;
                    baseWeight = rating / 5.0; // 1-10 -> 0.2-2.0
                }
                else if (favoriteSlugs.Contains(movie.Slug))
                {
                    baseWeight = 1.5; // Избранное
                }
                else if (watchListSlugs.Contains(movie.Slug))
                {
                    baseWeight = 1.2; // Watchlist
                }

                foreach (var genre in movie.Genres)
                {
                    if (genreScores.ContainsKey(genre))
                        genreScores[genre] += baseWeight;
                    else
                        genreScores[genre] = baseWeight;
                }
            }

            return genreScores;
        }

        private Dictionary<int, double> CalculateYearPreferences(
            List<Movie> likedMovies,
            List<(Movie Movie, int Rating)> userRatings,
            HashSet<string> watchListSlugs,
            HashSet<string> favoriteSlugs)
        {
            var yearScores = new Dictionary<int, double>();

            foreach (var movie in likedMovies.Where(m => m.Year > 0))
            {
                double baseWeight = 1.0;

                if (userRatings.Any(r => r.Movie.Id == movie.Id))
                {
                    var rating = userRatings.First(r => r.Movie.Id == movie.Id).Rating;
                    baseWeight = rating / 5.0;
                }
                else if (favoriteSlugs.Contains(movie.Slug))
                {
                    baseWeight = 1.5;
                }
                else if (watchListSlugs.Contains(movie.Slug))
                {
                    baseWeight = 1.2;
                }

                if (yearScores.ContainsKey(movie.Year))
                    yearScores[movie.Year] += baseWeight;
                else
                    yearScores[movie.Year] = baseWeight;
            }

            return yearScores;
        }

        private double CalculateRelevanceScore(
            Movie movie,
            Dictionary<string, double> genrePreferences,
            Dictionary<int, double> yearPreferences,
            HashSet<string> watchListSlugs,
            HashSet<string> favoriteSlugs)
        {
            double score = movie.Rating * 0.3 + Math.Log10(movie.VoteCount) * 0.2;

            // Бонус за совпадение жанров
            foreach (var genre in movie.Genres)
            {
                if (genrePreferences.ContainsKey(genre))
                {
                    score += genrePreferences[genre] * 0.5;
                }
            }

            // Бонус за совпадение года
            if (yearPreferences.ContainsKey(movie.Year))
            {
                score += yearPreferences[movie.Year] * 0.3;
            }
            // Близкие годы тоже дают бонус
            else
            {
                var closestYear = yearPreferences.Keys
                    .OrderBy(y => Math.Abs(y - movie.Year))
                    .FirstOrDefault();

                if (closestYear != 0 && Math.Abs(closestYear - movie.Year) <= 5)
                {
                    score += yearPreferences[closestYear] * 0.15;
                }
            }

            // Бонус за наличие в списках
            if (favoriteSlugs.Contains(movie.Slug))
                score += 3.0;
            else if (watchListSlugs.Contains(movie.Slug))
                score += 2.0;

            return score;
        }

        // ===== МЕТОДЫ ПОИСКА =====

        private async Task<List<Movie>> SearchInDatabase(
            List<string> genres,
            List<int> years,
            HashSet<string> excludedSlugs,
            int limit)
        {
            var movies = new List<Movie>();

            try
            {
                using (var connection = new Npgsql.NpgsqlConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT * FROM movies 
                        WHERE vote_count >= 50
                          AND rating >= 5.0
                          AND poster IS NOT NULL 
                          AND poster != 'null'";

                    if (excludedSlugs.Any())
                    {
                        query += " AND slug NOT IN (SELECT UNNEST(@excludedSlugs::text[]))";
                    }

                    if (genres.Any())
                    {
                        query += " AND (";
                        for (int i = 0; i < genres.Count; i++)
                        {
                            query += $" genres::text LIKE @genre{i} ";
                            if (i < genres.Count - 1) query += " OR ";
                        }
                        query += " )";
                    }

                    if (years.Any())
                    {
                        query += " AND (";
                        for (int i = 0; i < years.Count; i++)
                        {
                            query += $" year BETWEEN @year{i}Min AND @year{i}Max ";
                            if (i < years.Count - 1) query += " OR ";
                        }
                        query += " )";
                    }

                    query += " ORDER BY rating DESC, vote_count DESC LIMIT @limit";

                    var command = new Npgsql.NpgsqlCommand(query, connection);

                    if (excludedSlugs.Any())
                    {
                        var excludedArray = excludedSlugs.ToArray();
                        command.Parameters.AddWithValue("@excludedSlugs",
                            NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text,
                            excludedArray);
                    }

                    for (int i = 0; i < genres.Count; i++)
                    {
                        command.Parameters.AddWithValue($"@genre{i}", $"%{genres[i]}%");
                    }

                    for (int i = 0; i < years.Count; i++)
                    {
                        command.Parameters.AddWithValue($"@year{i}Min", years[i] - 3);
                        command.Parameters.AddWithValue($"@year{i}Max", years[i] + 3);
                    }

                    command.Parameters.AddWithValue("@limit", limit);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var movie = CreateMovieFromReader(reader);
                            movies.Add(movie);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка поиска в БД: {ex.Message}");
            }

            return movies;
        }

        private async Task<List<Movie>> FindSimilarMoviesBatch(
            List<Movie> sourceMovies,
            HashSet<string> excludedSlugs,
            int limit)
        {
            var similar = new List<Movie>();

            foreach (var sourceMovie in sourceMovies)
            {
                if (similar.Count >= limit) break;

                try
                {
                    using (var connection = new Npgsql.NpgsqlConnection(_databaseService.GetConnectionString()))
                    {
                        await connection.OpenAsync();

                        string query = @"
                            SELECT * FROM movies 
                            WHERE vote_count >= 50 
                              AND rating >= 5.0
                              AND slug != @sourceSlug";

                        if (excludedSlugs.Any())
                        {
                            query += " AND slug NOT IN (SELECT UNNEST(@excludedSlugs::text[]))";
                        }

                        if (sourceMovie.Genres.Any())
                        {
                            query += " AND (";
                            for (int i = 0; i < sourceMovie.Genres.Count; i++)
                            {
                                query += $" genres::text LIKE @genre{i} ";
                                if (i < sourceMovie.Genres.Count - 1) query += " OR ";
                            }
                            query += " )";
                        }

                        if (sourceMovie.Year > 0)
                        {
                            query += " AND year BETWEEN @minYear AND @maxYear";
                        }

                        query += " ORDER BY rating DESC, vote_count DESC LIMIT 5";

                        var command = new Npgsql.NpgsqlCommand(query, connection);

                        command.Parameters.AddWithValue("@sourceSlug", sourceMovie.Slug);

                        if (excludedSlugs.Any())
                        {
                            var excludedArray = excludedSlugs.ToArray();
                            command.Parameters.AddWithValue("@excludedSlugs",
                                NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text,
                                excludedArray);
                        }

                        for (int i = 0; i < sourceMovie.Genres.Count; i++)
                        {
                            command.Parameters.AddWithValue($"@genre{i}", $"%{sourceMovie.Genres[i]}%");
                        }

                        if (sourceMovie.Year > 0)
                        {
                            command.Parameters.AddWithValue("@minYear", sourceMovie.Year - 5);
                            command.Parameters.AddWithValue("@maxYear", sourceMovie.Year + 5);
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var movie = CreateMovieFromReader(reader);
                                if (!similar.Any(m => m.Id == movie.Id) && !excludedSlugs.Contains(movie.Slug))
                                {
                                    similar.Add(movie);
                                    if (similar.Count >= limit) break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка поиска похожих на {sourceMovie.Title}: {ex.Message}");
                }
            }

            return similar;
        }

        private async Task<List<Movie>> SearchInTMDB(List<string> genres, int limit)
        {
            var movies = new List<Movie>();

            try
            {
                // Получаем популярные фильмы
                var popular = await _tmdbParser.GetPopularMoviesAsync(1, 50, limit * 2);

                // Фильтруем по жанрам (приблизительно)
                foreach (var movie in popular)
                {
                    if (movies.Count >= limit) break;

                    // Проверяем пересечение жанров
                    if (movie.Genres.Intersect(genres).Any())
                    {
                        movies.Add(movie);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка поиска в TMDB: {ex.Message}");
            }

            return movies;
        }

        private async Task<List<Movie>> GetPopularMoviesFromTMDB(int count)
        {
            try
            {
                return await _tmdbParser.GetPopularMoviesAsync(1, 50, count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка получения популярных из TMDB: {ex.Message}");
                return new List<Movie>();
            }
        }

        // ===== СУЩЕСТВУЮЩИЕ МЕТОДЫ =====

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
                        var movie = CreateMovieFromReader(reader);
                        int userRating = Convert.ToInt32(reader["rating"]);
                        ratings.Add((movie, userRating));
                    }
                }
            }

            return ratings;
        }

        private Movie CreateMovieFromReader(Npgsql.NpgsqlDataReader reader)
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

            return movie;
        }

        private string GetNoRecommendationsMessage()
        {
            var ratingsCount = _databaseService.GetUserRatingsCount(_currentUser.Id);
            var watchListCount = _databaseService.GetWatchListCount(_currentUser.Id);
            var favoritesCount = _databaseService.GetFavoritesCount(_currentUser.Id);

            if (ratingsCount == 0 && watchListCount == 0 && favoritesCount == 0)
            {
                return "Добавьте фильмы в избранное, в список 'Хочу посмотреть' или оцените несколько фильмов, чтобы получить рекомендации";
            }
            else
            {
                return "Пока не удалось найти подходящие рекомендации. Попробуйте добавить больше фильмов в избранное или оценить разные жанры.";
            }
        }

        private List<Movie> FilterOutExcludedMovies(List<Movie> movies, HashSet<string> excludedSlugs, int limit)
        {
            return movies
                .Where(m => !excludedSlugs.Contains(m.Slug))
                .Take(limit)
                .ToList();
        }

        private void DisplayMovies(List<Movie> movies)
        {
            MoviesPanel.Children.Clear();

            foreach (var movie in movies.Take(30))
            {
                var movieButton = MovieCardHelper.CreateMovieCard(
                    movie,
                    _currentUser.Id,
                    _databaseService,
                    ShowMovieDetails
                );
                MoviesPanel.Children.Add(movieButton);
            }
        }

        private Button CreateMovieButton(Movie movie)
        {
            bool isInWatchList = _databaseService.IsInWatchList(_currentUser.Id, movie.Slug);
            bool isFavorite = _databaseService.IsInFavorites(_currentUser.Id, movie.Slug);

            var button = new Button
            {
                Margin = new Thickness(10),
                Padding = new Thickness(0),
                Background = Brushes.White,
                BorderBrush = Brushes.DodgerBlue,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                Width = 160,
                Height = 300
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

            // Бейдж
            var badge = new Border
            {
                Background = Brushes.DodgerBlue,
                Padding = new Thickness(5, 2, 5, 2),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };

            string badgeText = "🌟 Рекомендация";
            if (isInWatchList && isFavorite)
                badgeText = "❤️📋 В списках";
            else if (isInWatchList)
                badgeText = "📋 В watchlist";
            else if (isFavorite)
                badgeText = "❤️ В избранном";

            var badgeTextBlock = new TextBlock
            {
                Text = badgeText,
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            badge.Child = badgeTextBlock;
            stackPanel.Children.Add(badge);

            // Информация
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