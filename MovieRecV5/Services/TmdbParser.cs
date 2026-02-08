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
                // Ищем фильмы в TMDB
                var searchResults = await SearchMoviesInTMDB(searchTitle, year);

                // Обрабатываем только те фильмы, у которых есть постер
                foreach (var movieData in searchResults)
                {
                    // Проверяем наличие постера
                    var posterPath = movieData.GetProperty("poster_path").GetString();
                    if (string.IsNullOrEmpty(posterPath) || posterPath == "null")
                    {
                        continue;
                    }

                    await Task.Delay(_random.Next(500, 1500));
                    var movie = await ParseMovieFromTmdbData(movieData);
                    if (movie != null)
                    {
                        movies.Add(movie);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при поиске фильмов в TMDB: {ex.Message}");
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

                Console.WriteLine($"🔍 Поиск в TMDB: {searchTitle}");

                var response = await _httpClient.GetStringAsync(searchUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var results = jsonDoc.RootElement.GetProperty("results");
                var moviesList = new List<JsonElement>();

                if (results.GetArrayLength() > 0)
                {
                    foreach (var result in results.EnumerateArray())
                    {
                        // Проверяем основные обязательные поля
                        if (result.TryGetProperty("title", out var titleElement) &&
                            titleElement.ValueKind != JsonValueKind.Null &&
                            !string.IsNullOrEmpty(titleElement.GetString()))
                        {
                            moviesList.Add(result);
                        }
                    }
                }

                Console.WriteLine($"📊 Результатов из TMDB: {moviesList.Count}");
                return moviesList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при поиске в TMDB: {ex.Message}");
                return new List<JsonElement>();
            }
        }
        public async Task<Movie> ParseMovieFromTmdbData(JsonElement movieData)
        {
            try
            {
                var title = movieData.GetProperty("title").GetString();
                var overview = movieData.GetProperty("overview").GetString();
                var releaseDate = movieData.GetProperty("release_date").GetString();
                var voteAverage = movieData.GetProperty("vote_average").GetSingle();
                var voteCount = movieData.GetProperty("vote_count").GetInt32();
                var tmdbId = movieData.GetProperty("id").GetInt32();

                var posterPath = movieData.GetProperty("poster_path").GetString();
                if (string.IsNullOrEmpty(posterPath) || posterPath == "null")
                {
                    return null; 
                }

                int year = 0;
                if (!string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 4)
                {
                    int.TryParse(releaseDate.Substring(0, 4), out year);
                }

                var slug = $"tmdb-{tmdbId}-{ConvertToSlug(title)}-{year}";

                string posterUrl = $"https://image.tmdb.org/t/p/w500{posterPath}";

                string posterBase64 = null;
                try
                {
                    var posterService = new MoviePosterService();
                    posterBase64 = await posterService.DownloadPosterAsBase64(posterUrl);

                    if (string.IsNullOrEmpty(posterBase64))
                    {
                        Console.WriteLine($"❌ Не удалось загрузить постер для '{title}'");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка загрузки постера для '{title}': {ex.Message}");
                    return null;
                }

                var genres = await GetMovieGenres(tmdbId);

                var movie = new Movie
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

                Console.WriteLine($"✅ Добавлен фильм: {title} ({year})");
                return movie;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

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
                Console.WriteLine($"⚠️ Ошибка при получении жанров: {ex.Message}");
            }

            return genres;
        }

        public async Task<Movie> GetMovieByTmdbId(int tmdbId)
        {
            try
            {
                var movieUrl = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_apiKey}&language=ru-RU&append_to_response=credits";
                var response = await _httpClient.GetStringAsync(movieUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var title = jsonDoc.RootElement.GetProperty("title").GetString();
                var overview = jsonDoc.RootElement.GetProperty("overview").GetString();
                var releaseDate = jsonDoc.RootElement.GetProperty("release_date").GetString();
                var voteAverage = jsonDoc.RootElement.GetProperty("vote_average").GetSingle();
                var voteCount = jsonDoc.RootElement.GetProperty("vote_count").GetInt32();
                var posterPath = jsonDoc.RootElement.GetProperty("poster_path").GetString();

                if (string.IsNullOrEmpty(posterPath) || posterPath == "null")
                {
                    Console.WriteLine($"❌ Фильм '{title}' без постера - пропускаем");
                    return null;
                }

                int year = 0;
                if (!string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 4)
                {
                    int.TryParse(releaseDate.Substring(0, 4), out year);
                }

                var slug = $"tmdb-{tmdbId}-{ConvertToSlug(title)}-{year}";
                string posterUrl = $"https://image.tmdb.org/t/p/w500{posterPath}";

                string posterBase64 = null;
                try
                {
                    var posterService = new MoviePosterService();
                    posterBase64 = await posterService.DownloadPosterAsBase64(posterUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка загрузки постера: {ex.Message}");
                    return null;
                }

                var genres = new List<string>();
                if (jsonDoc.RootElement.TryGetProperty("genres", out var genresElement))
                {
                    foreach (var genre in genresElement.EnumerateArray())
                    {
                        var genreName = genre.GetProperty("name").GetString();
                        if (!string.IsNullOrEmpty(genreName))
                            genres.Add(genreName);
                    }
                }

                var movie = new Movie
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

                Console.WriteLine($"✅ Получен фильм по ID: {title} ({year})");
                return movie;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при получении фильма по ID: {ex.Message}");
                return null;
            }
        }

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
                    var posterPath = movieData.GetProperty("poster_path").GetString();
                    if (string.IsNullOrEmpty(posterPath) || posterPath == "null")
                        continue;

                    var movie = await ParseMovieFromTmdbData(movieData);
                    if (movie != null)
                        movies.Add(movie);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при получении популярных фильмов: {ex.Message}");
            }

            return movies;
        }

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
                .Replace(":", "-")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("!", "")
                .Replace("?", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("&", "and")
                .Replace("--", "-")
                .Replace("---", "-")
                .Trim('-');

            return slug;
        }

        private string CleanDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return "Описание отсутствует";

            return description
                .Replace("&quot;", "\"")
                .Replace("&#039;", "'")
                .Replace("&apos;", "'")
                .Replace("&amp;", "&")
                .Replace("&nbsp;", " ")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");
        }

        public async Task<List<Movie>> SearchMoviesFast(string searchTitle, int maxResults = 40, int minVotes = 100)
        {
            try
            {
                var movies = new List<Movie>();
                int totalPages = 1;
                int currentPage = 1;

                var searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={WebUtility.UrlEncode(searchTitle)}&language=ru-RU&page={currentPage}";
                var response = await _httpClient.GetStringAsync(searchUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var totalResults = jsonDoc.RootElement.GetProperty("total_results").GetInt32();
                totalPages = jsonDoc.RootElement.GetProperty("total_pages").GetInt32();

                Console.WriteLine($"📊 Всего результатов в TMDB: {totalResults}, страниц: {totalPages}");

                var results = jsonDoc.RootElement.GetProperty("results");
                movies.AddRange(ProcessSearchResults(results, minVotes));

                while (movies.Count < maxResults && currentPage < totalPages && currentPage < 3)
                {
                    currentPage++;
                    searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={WebUtility.UrlEncode(searchTitle)}&language=ru-RU&page={currentPage}";
                    response = await _httpClient.GetStringAsync(searchUrl);
                    jsonDoc = JsonDocument.Parse(response);
                    results = jsonDoc.RootElement.GetProperty("results");
                    movies.AddRange(ProcessSearchResults(results, minVotes));

                    await Task.Delay(200);
                }

                movies = movies
                    .OrderByDescending(m => m.VoteCount)
                    .ThenByDescending(m => m.Rating)
                    .Take(maxResults)
                    .ToList();

                Console.WriteLine($"🔍 Быстрый поиск: {movies.Count} фильмов с постером (минимум {minVotes} оценок)");
                return movies;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при быстром поиске: {ex.Message}");
                return new List<Movie>();
            }
        }

        private List<Movie> ProcessSearchResults(JsonElement results, int minVotes = 100)
        {
            var movies = new List<Movie>();

            foreach (var result in results.EnumerateArray())
            {
                var posterPath = result.GetProperty("poster_path").GetString();
                if (string.IsNullOrEmpty(posterPath) || posterPath == "null")
                    continue;

                var title = result.GetProperty("title").GetString();
                var releaseDate = result.GetProperty("release_date").GetString();
                var tmdbId = result.GetProperty("id").GetInt32();
                var voteAverage = result.GetProperty("vote_average").GetSingle();
                var voteCount = result.GetProperty("vote_count").GetInt32();

                if (voteCount < minVotes)
                    continue;

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
                    Rating = voteAverage,
                    VoteCount = voteCount,
                    LetterBoxdUrl = $"https://www.themoviedb.org/movie/{tmdbId}"
                });
            }

            return movies;
        }
        public async Task<List<Movie>> GetMoviesWithPosterOnly(string searchTitle)
        {
            try
            {
                var searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={WebUtility.UrlEncode(searchTitle)}&language=ru-RU";
                var response = await _httpClient.GetStringAsync(searchUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var movies = new List<Movie>();
                var results = jsonDoc.RootElement.GetProperty("results");

                foreach (var result in results.EnumerateArray())
                {
                    var posterPath = result.GetProperty("poster_path").GetString();
                    if (string.IsNullOrEmpty(posterPath) || posterPath == "null")
                    {
                        continue; 
                    }

                    var movie = await ParseMovieFromTmdbData(result);
                    if (movie != null)
                    {
                        movies.Add(movie);
                    }
                }

                return movies;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске фильмов с постером: {ex.Message}");
                return new List<Movie>();
            }
        }


    }
}