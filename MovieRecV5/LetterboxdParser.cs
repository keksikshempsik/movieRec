using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Text.Json;
using MovieRecV5;

namespace MovieRecV5
{
    public class LetterboxdParser
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;

        public LetterboxdParser()
        {
            var handler = new HttpClientHandler()
            {
                UseCookies = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(15); // Уменьшили таймаут
            _random = new Random();
        }

        // НОВЫЙ МЕТОД: Поиск всех фильмов по названию
        public async Task<List<Movie>> SearchAllMovies(string searchTitle)
        {
            var movies = new List<Movie>();

            try
            {
                // Сначала ищем на странице поиска Letterboxd
                var searchResults = await GetSearchResults(searchTitle);

                foreach (var filmSlug in searchResults)
                {
                    await Task.Delay(_random.Next(1000, 3000));
                    var movie = await TryParseMovie(filmSlug);
                    if (movie != null)
                    {
                        movies.Add(movie);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске фильмов: {ex.Message}");
            }

            return movies;
        }

        // НОВЫЙ МЕТОД: Получение результатов поиска с Letterboxd
        private async Task<List<string>> GetSearchResults(string searchTitle)
        {
            var filmSlugs = new List<string>();

            try
            {
                var searchUrl = $"https://letterboxd.com/search/films/{WebUtility.UrlEncode(searchTitle)}/";
                Console.WriteLine($"Ищем по URL: {searchUrl}");

                var html = await _httpClient.GetStringAsync(searchUrl);
                Console.WriteLine($"Получено HTML: {html.Length} символов");

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Ищем все ссылки на фильмы в результатах поиска
                var filmLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/film/')]");

                Console.WriteLine($"Найдено ссылок: {filmLinks?.Count ?? 0}");

                if (filmLinks != null)
                {
                    foreach (var link in filmLinks)
                    {
                        var href = link.GetAttributeValue("href", "");
                        Console.WriteLine($"Найдена ссылка: {href}");

                        if (href.Contains("/film/"))
                        {
                            var slug = href.Split(new[] { "/film/" }, StringSplitOptions.RemoveEmptyEntries)
                                          .Last()
                                          .Trim('/');

                            if (!string.IsNullOrEmpty(slug) && !filmSlugs.Contains(slug))
                            {
                                filmSlugs.Add(slug);
                                Console.WriteLine($"Добавлен slug: {slug}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении результатов поиска: {ex.Message}");
                MessageBox.Show($"Ошибка при получении результатов поиска: {ex.Message}");
            }

            return filmSlugs.Take(10).ToList();
        }

        public async Task<Movie> TryParseMovie(string filmSlug)
        {
            try
            {
                await Task.Delay(_random.Next(1000, 3000));

                var html = await GetMoviePage(filmSlug);

                if (html == null) return null;

                return await ParseMovieFromHtml(html, filmSlug);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetMoviePage(string filmSlug)
        {
            var url = $"https://letterboxd.com/film/{filmSlug}/";

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            return null;
        }

        public async Task<Movie> ParseMovieFromHtml(string html, string filmSlug)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = doc.DocumentNode.SelectSingleNode("//span[@class='name js-widont prettify']")?.InnerText;

            var yearText = doc.DocumentNode.SelectSingleNode("//span[@class='releasedate']//a")?.InnerText;
            int year = 0;
            if (!string.IsNullOrEmpty(yearText) && int.TryParse(yearText, out int parsedYear))
            {
                year = parsedYear;
            }

            var description = doc.DocumentNode.SelectSingleNode("//div[@class='truncate']//p")?.InnerText;

            var genres = new List<string>();

            var genresDiv = doc.DocumentNode.SelectSingleNode("//h3[span/text()='Genres']/following-sibling::div[@class='text-sluglist capitalize']");

            if (genresDiv != null)
            {
                var genreNodes = genresDiv.SelectNodes(".//a[@class='text-slug']");
                if (genreNodes != null)
                {
                    foreach (var node in genreNodes)
                    {
                        string genreName = node.InnerText.Trim();
                        if (!string.IsNullOrEmpty(genreName))
                            genres.Add(genreName);
                    }
                }
            }

            string poster = null;
            if (!string.IsNullOrEmpty(title))
            {
                poster = await GetPosterFromTMDB(title, year);
            }

            MoviePosterService posterService = new MoviePosterService();
            string poster64 = null;
            if (!string.IsNullOrEmpty(poster))
            {
                poster64 = await posterService.DownloadPosterAsBase64(poster);
            }

            var voteCount = await GetVoteCountFromTMDB(title, year);

            return new Movie
            {
                Title = title ?? filmSlug.Replace("-", " "),
                Slug = filmSlug,
                Year = year,
                Description = CleanDescription(description),
                PosterUrl = poster,
                LetterBoxdUrl = $"https://letterboxd.com/film/{filmSlug}/",
                Poster = poster64,
                Genres = genres,
                VoteCount = voteCount
            };
        }

        private async Task<string> GetPosterFromTMDB(string title, int year)
        {
            try
            {
                string apiKey = "2270bb1505a8b2cd2f6e409310da706c";
                string searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={WebUtility.UrlEncode(title)}&year={year}";

                var response = await _httpClient.GetStringAsync(searchUrl);

                if (response.Contains("\"poster_path\""))
                {
                    var start = response.IndexOf("\"poster_path\"") + 15;
                    var end = response.IndexOf("\"", start);
                    if (start > 14 && end > start)
                    {
                        var posterPath = response.Substring(start, end - start);
                        if (!string.IsNullOrEmpty(posterPath) && posterPath != "null")
                        {
                            return $"https://image.tmdb.org/t/p/w500{posterPath}";
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"TMDB error: {ex.Message}");
                return null;
            }
        }

        private async Task<int> GetVoteCountFromTMDB(string title, int year)
        {
            try
            {
                string apiKey = "2270bb1505a8b2cd2f6e409310da706c";
                string searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={WebUtility.UrlEncode(title)}&year={year}";

                var response = await _httpClient.GetStringAsync(searchUrl);

                // Используем JsonDocument для парсинга JSON
                using (var jsonDoc = JsonDocument.Parse(response))
                {
                    var results = jsonDoc.RootElement.GetProperty("results");

                    if (results.GetArrayLength() > 0)
                    {
                        var firstResult = results[0];

                        // Получаем vote_count из первого результата
                        if (firstResult.TryGetProperty("vote_count", out var voteCountElement) &&
                            voteCountElement.ValueKind == JsonValueKind.Number)
                        {
                            return voteCountElement.GetInt32();
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"TMDB error: {ex.Message}");
                return 0;
            }
        }

        private string CleanDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return description;

            string result = description
                .Replace("&quot;", "\"")
                .Replace("&#039;", "'")
                .Replace("&apos;", "'")
                .Replace("&amp;", "&")
                .Replace("&nbsp;", " ")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");

            return result;
        }

        public string ConvertToSlug(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            // Простая конвертация в slug
            var slug = title.ToLower()
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

        // НОВЫЙ МЕТОД: Создание регулярного выражения для поиска вариантов
        public string CreateSearchPattern(string baseTitle)
        {
            var baseSlug = ConvertToSlug(baseTitle);

            // Создаем паттерн для поиска всех вариантов: the-avengers, the-avengers-1950, the-avengers-2012 и т.д.
            var pattern = $"^{Regex.Escape(baseSlug)}(-\\d{{4}})?$";

            return pattern;
        }

        public async Task<string> FindExactSlug(string title, int? year = null)
        {
            try
            {
                string apiKey = "2270bb1505a8b2cd2f6e409310da706c";
                string searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={WebUtility.UrlEncode(title)}";

                if (year.HasValue)
                    searchUrl += $"&year={year}";

                var response = await _httpClient.GetStringAsync(searchUrl);
                var jsonDoc = JsonDocument.Parse(response);

                var results = jsonDoc.RootElement.GetProperty("results");
                if (results.GetArrayLength() > 0)
                {
                    var firstResult = results[0];
                    var movieTitle = firstResult.GetProperty("title").GetString();
                    var releaseDate = firstResult.GetProperty("release_date").GetString();

                    if (!string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 4)
                    {
                        var movieYear = releaseDate.Substring(0, 4);
                        return ConvertToSlug($"{movieTitle} {movieYear}");
                    }
                    return ConvertToSlug(movieTitle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TMDB search error: {ex.Message}");
            }

            return ConvertToSlug(title);
        }

        // В классе LetterboxdParser добавьте быстрый метод
        public async Task<Movie> TryParseMovieFast(string filmSlug)
        {
            try
            {
                var html = await GetMoviePage(filmSlug);
                if (html == null) return null;

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Быстрый парсинг только основных полей
                var title = doc.DocumentNode.SelectSingleNode("//span[@class='name js-widont prettify']")?.InnerText;
                if (string.IsNullOrEmpty(title)) return null;

                var yearText = doc.DocumentNode.SelectSingleNode("//span[@class='releasedate']//a")?.InnerText;
                int year = 0;
                int.TryParse(yearText, out year);

                return new Movie
                {
                    Title = title,
                    Slug = filmSlug,
                    Year = year,
                    LetterBoxdUrl = $"https://letterboxd.com/film/{filmSlug}/"
                };
            }
            catch
            {
                return null;
            }
        }
    }
}