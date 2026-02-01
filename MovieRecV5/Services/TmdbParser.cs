using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using MovieRecV5.Models;

namespace MovieRecV5.Services
{
    public class TmdbParser
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;
        private readonly string _apiKey = "2270bb1505a8b2cd2f6e409310da706c";

        public TmdbParser()
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _random = new Random();
        }

        // Основной метод для поиска всех фильмов по названию
        public async Task<List<Movie>> SearchAllMovies(string searchTitle, int? year = null)
        {
            var movies = new List<Movie>();

            try
            {
                var searchResults = await SearchMoviesInTMDB(searchTitle, year);

                foreach (var movieData in searchResults)
                {
                    await Task.Delay(_random.Next(500, 1500)); // Меньшая задержка для TMDB
                    var movie = await ParseMovieFromTmdbData(movieData);
                    if (movie != null)
                    {
                        movies.Add(movie);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске фильмов в TMDB: {ex.Message}");
            }

            return movies;
        }

        // Поиск фильмов в TMDB API
        private async Task<List<JsonElement>> SearchMoviesInTMDB(string searchTitle, int? year = null)
        {
            try
            {
                var searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={WebUtility.UrlEncode(searchTitle)}&language=ru-RU";

                if (year.HasValue)
                    searchUrl += $"&year={year}";

                Console.WriteLine($"Ищем в TMDB: {searchUrl}");

                var response = await _httpClient.GetStringAsync(searchUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var results = jsonDoc.RootElement.GetProperty("results");
                var moviesList = new List<JsonElement>();

                if (results.GetArrayLength() > 0)
                {
                    foreach (var result in results.EnumerateArray())
                    {
                        moviesList.Add(result);
                    }
                }

                return moviesList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске в TMDB: {ex.Message}");
                return new List<JsonElement>();
            }
        }

        // Парсинг данных из JSON TMDB в объект Movie
        public async Task<Movie> ParseMovieFromTmdbData(JsonElement movieData)
        {
            try
            {
                // Получаем основные данные
                var title = movieData.GetProperty("title").GetString();
                var originalTitle = movieData.GetProperty("original_title").GetString();
                var overview = movieData.GetProperty("overview").GetString();
                var releaseDate = movieData.GetProperty("release_date").GetString();
                var voteAverage = movieData.GetProperty("vote_average").GetSingle();
                var voteCount = movieData.GetProperty("vote_count").GetInt32();
                var tmdbId = movieData.GetProperty("id").GetInt32();

                // Получаем год из даты релиза
                int year = 0;
                if (!string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 4)
                {
                    int.TryParse(releaseDate.Substring(0, 4), out year);
                }

                // Создаем slug (используем ID TMDB для уникальности)
                var slug = $"tmdb-{tmdbId}-{ConvertToSlug(title)}-{year}";

                // Получаем постер
                string posterUrl = null;
                string posterBase64 = null;
                var posterPath = movieData.GetProperty("poster_path").GetString();
                if (!string.IsNullOrEmpty(posterPath) && posterPath != "null")
                {
                    posterUrl = $"https://image.tmdb.org/t/p/w500{posterPath}";

                    // Загружаем постер в base64
                    var posterService = new MoviePosterService();
                    posterBase64 = await posterService.DownloadPosterAsBase64(posterUrl);
                }

                // Получаем жанры (нужен дополнительный запрос для детальной информации)
                var genres = await GetMovieGenres(tmdbId);

                return new Movie
                {
                    Id = tmdbId,
                    Title = title,
                    Slug = slug,
                    Year = year,
                    Description = CleanDescription(overview),
                    PosterUrl = posterUrl,
                    LetterBoxdUrl = $"https://www.themoviedb.org/movie/{tmdbId}",
                    Poster = posterBase64,
                    Genres = genres,
                    VoteCount = voteCount,
                    Rating = voteAverage
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при парсинге данных TMDB: {ex.Message}");
                return null;
            }
        }

        // Получение жанров фильма
        private async Task<List<string>> GetMovieGenres(int tmdbId)
        {
            var genres = new List<string>();

            try
            {
                var movieUrl = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_apiKey}&language=ru-RU";
                var response = await _httpClient.GetStringAsync(movieUrl);
                var jsonDoc = JsonDocument.Parse(response);

                if (jsonDoc.RootElement.TryGetProperty("genres", out var genresElement))
                {
                    foreach (var genre in genresElement.EnumerateArray())
                    {
                        var genreName = genre.GetProperty("name").GetString();
                        if (!string.IsNullOrEmpty(genreName))
                            genres.Add(genreName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении жанров: {ex.Message}");
            }

            return genres;
        }

        // Получение фильма по TMDB ID
        public async Task<Movie> GetMovieByTmdbId(int tmdbId)
        {
            try
            {
                var movieUrl = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_apiKey}&language=ru-RU";
                var response = await _httpClient.GetStringAsync(movieUrl);
                var jsonDoc = JsonDocument.Parse(response);

                return await ParseMovieFromTmdbData(jsonDoc.RootElement);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении фильма по ID: {ex.Message}");
                return null;
            }
        }

        // Получение популярных фильмов
        public async Task<List<Movie>> GetPopularMovies(int page = 1)
        {
            var movies = new List<Movie>();

            try
            {
                var popularUrl = $"https://api.themoviedb.org/3/movie/popular?api_key={_apiKey}&language=ru-RU&page={page}";
                var response = await _httpClient.GetStringAsync(popularUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var results = jsonDoc.RootElement.GetProperty("results");

                foreach (var movieData in results.EnumerateArray())
                {
                    var movie = await ParseMovieFromTmdbData(movieData);
                    if (movie != null)
                        movies.Add(movie);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении популярных фильмов: {ex.Message}");
            }

            return movies;
        }

        // Конвертация в slug
        public string ConvertToSlug(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            var cleanTitle = title.Trim();
            var articles = new[] { "the ", "a ", "an " };
            foreach (var article in articles)
            {
                if (cleanTitle.ToLower().StartsWith(article))
                {
                    cleanTitle = cleanTitle.Substring(article.Length);
                    break;
                }
            }

            var slug = cleanTitle.ToLower()
                .Replace(" ", "-")
                .Replace(":", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("!", "")
                .Replace("?", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("&", "and")
                .Replace("--", "-")
                .Trim('-');

            return slug;
        }

        // Очистка описания
        private string CleanDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return description;

            return description
                .Replace("&quot;", "\"")
                .Replace("&#039;", "'")
                .Replace("&apos;", "'")
                .Replace("&amp;", "&")
                .Replace("&nbsp;", " ")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");
        }

        // Метод для быстрого поиска
        public async Task<List<Movie>> SearchMoviesFast(string searchTitle)
        {
            try
            {
                var searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={WebUtility.UrlEncode(searchTitle)}&language=ru-RU";
                var response = await _httpClient.GetStringAsync(searchUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var movies = new List<Movie>();
                var results = jsonDoc.RootElement.GetProperty("results");

                foreach (var result in results.EnumerateArray().Take(5)) // Берем только первые 5 результатов
                {
                    var title = result.GetProperty("title").GetString();
                    var releaseDate = result.GetProperty("release_date").GetString();
                    var tmdbId = result.GetProperty("id").GetInt32();

                    int year = 0;
                    if (!string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 4)
                    {
                        int.TryParse(releaseDate.Substring(0, 4), out year);
                    }

                    var slug = $"tmdb-{tmdbId}-{ConvertToSlug(title)}-{year}";

                    movies.Add(new Movie
                    {
                        Id = tmdbId,
                        Title = title,
                        Slug = slug,
                        Year = year,
                        LetterBoxdUrl = $"https://www.themoviedb.org/movie/{tmdbId}"
                    });
                }

                return movies;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при быстром поиске: {ex.Message}");
                return new List<Movie>();
            }
        }
    }
}