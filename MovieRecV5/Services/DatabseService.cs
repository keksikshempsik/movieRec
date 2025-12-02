using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using MovieRecV5.Models;

namespace MovieRecV5.Services
{
    public class DatabaseService
    {
        private string _databasePath;
        private readonly HttpClient _httpClient;

        public DatabaseService()
        {
            _databasePath = Path.Combine(Directory.GetCurrentDirectory(), "movies.db");
            _httpClient = new HttpClient();
        }

        public void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Movies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Slug TEXT NOT NULL,
                    Year INTEGER,
                    Description TEXT,
                    PosterUrl TEXT,
                    LetterBoxdUrl TEXT,
                    Poster TEXT,
                    Genres TEXT,
                    VoteCount INTEGER,
                    Rating FLOAT
                )";

                createTableCommand.ExecuteNonQuery();

                createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Login TEXT NOT NULL UNIQUE,
                    Email TEXT NOT NULL UNIQUE,
                    Password TEXT NOT NULL)";

                createTableCommand.ExecuteNonQuery();

                // Новая таблица для пользовательских оценок
                createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS UserRatings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    MovieSlug TEXT NOT NULL,
                    Rating INTEGER NOT NULL CHECK (Rating >= 1 AND Rating <= 10),
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users (Id),
                    UNIQUE(UserId, MovieSlug)
                )";

                createTableCommand.ExecuteNonQuery();

                createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS WatchedMovies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    MovieSlug TEXT NOT NULL,
                    WatchedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users (Id),
                    UNIQUE(UserId, MovieSlug)
                )";
                createTableCommand.ExecuteNonQuery();

                createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS WatchList (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    MovieSlug TEXT NOT NULL,
                    AddedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users (Id),
                    UNIQUE(UserId, MovieSlug)
                )";
                createTableCommand.ExecuteNonQuery();
            }
        }

        // Методы для работы с WatchList
        public void AddToWatchList(int userId, string movieSlug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            INSERT OR IGNORE INTO WatchList (UserId, MovieSlug)
            VALUES ($userId, $movieSlug)";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);

                command.ExecuteNonQuery();
            }
        }

        public void RemoveFromWatchList(int userId, string movieSlug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            DELETE FROM WatchList 
            WHERE UserId = $userId AND MovieSlug = $movieSlug";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);

                command.ExecuteNonQuery();
            }
        }

        public bool IsInWatchList(int userId, string movieSlug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT COUNT(*) FROM WatchList 
            WHERE UserId = $userId AND MovieSlug = $movieSlug";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);

                var count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        public int GetWatchListCount(int userId)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT COUNT(*) FROM WatchList 
            WHERE UserId = $userId";

                command.Parameters.AddWithValue("$userId", userId);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public List<Movie> GetWatchListMovies(int userId)
        {
            var movies = new List<Movie>();

            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT m.* FROM Movies m
            INNER JOIN WatchList wl ON m.Slug = wl.MovieSlug
            WHERE wl.UserId = $userId
            ORDER BY wl.AddedAt DESC";

                command.Parameters.AddWithValue("$userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var movie = CreateMovieFromReader(reader, userId);
                        // Устанавливаем статусы для фильмов из WatchList
                        movie.IsWatched = IsMovieWatched(userId, movie.Slug);
                        movies.Add(movie);
                    }
                }
            }
            return movies;
        }

        // МЕТОДЫ ДЛЯ РЕЙТИНГОВ
        public void SaveUserRating(int userId, string movieSlug, int rating)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO UserRatings (UserId, MovieSlug, Rating)
                    VALUES ($userId, $movieSlug, $rating)";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);
                command.Parameters.AddWithValue("$rating", rating);

                command.ExecuteNonQuery();
            }
        }

        public int? GetUserRating(int userId, string movieSlug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Rating FROM UserRatings 
                    WHERE UserId = $userId AND MovieSlug = $movieSlug";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);

                var result = command.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : (int?)null;
            }
        }

        public void UpdateMovieRating(string movieSlug, int userRating)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                // Получаем текущие данные фильма
                var getCommand = connection.CreateCommand();
                getCommand.CommandText = "SELECT VoteCount, Rating FROM Movies WHERE Slug = $slug";
                getCommand.Parameters.AddWithValue("$slug", movieSlug);

                using (var reader = getCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int currentVoteCount = reader["VoteCount"] != DBNull.Value ? Convert.ToInt32(reader["VoteCount"]) : 0;
                        float currentRating = reader["Rating"] != DBNull.Value ? Convert.ToSingle(reader["Rating"]) : 0f;

                        // Обновляем рейтинг
                        int newVoteCount = currentVoteCount + 1;
                        float newRating = ((currentRating * currentVoteCount) + userRating) / newVoteCount;

                        // Обновляем запись в базе
                        var updateCommand = connection.CreateCommand();
                        updateCommand.CommandText = @"
                            UPDATE Movies 
                            SET VoteCount = $voteCount, Rating = $rating 
                            WHERE Slug = $slug";

                        updateCommand.Parameters.AddWithValue("$voteCount", newVoteCount);
                        updateCommand.Parameters.AddWithValue("$rating", newRating);
                        updateCommand.Parameters.AddWithValue("$slug", movieSlug);

                        updateCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        // ФИЛЬМЫ
        public void AddMovie(Movie movie)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var addMovieCommand = connection.CreateCommand();
                addMovieCommand.CommandText = @"
                    INSERT INTO Movies (Title, Slug, Year, Description, PosterUrl, LetterBoxdUrl, Poster, Genres, VoteCount, Rating)
                    VALUES ($title, $slug, $year, $description, $posterUrl, $letterboxdUrl, $poster, $genres, $voteCount, $rating)";

                addMovieCommand.Parameters.AddWithValue("$title", movie.Title ?? "");
                addMovieCommand.Parameters.AddWithValue("$slug", movie.Slug ?? "");
                addMovieCommand.Parameters.AddWithValue("$year", movie.Year);
                addMovieCommand.Parameters.AddWithValue("$description", movie.Description ?? "");
                addMovieCommand.Parameters.AddWithValue("$posterUrl", movie.PosterUrl ?? "");
                addMovieCommand.Parameters.AddWithValue("$letterboxdUrl", movie.LetterBoxdUrl ?? "");
                addMovieCommand.Parameters.AddWithValue("$poster", movie.Poster ?? "");
                addMovieCommand.Parameters.AddWithValue("$genres", JsonSerializer.Serialize(movie.Genres ?? new List<string>()));
                addMovieCommand.Parameters.AddWithValue("$voteCount", movie.VoteCount);
                addMovieCommand.Parameters.AddWithValue("$rating", movie.Rating);

                addMovieCommand.ExecuteNonQuery();
            }
        }

        public bool MovieExists(string slug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Movies WHERE Slug = $slug";
                command.Parameters.AddWithValue("$slug", slug);

                var count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        public List<Movie> GetMoviesFromDatabase(string searchTitle, int userId = 0)
        {
            var movies = new List<Movie>();

            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT * FROM Movies 
            WHERE Slug LIKE $searchPattern 
            OR Title LIKE $titlePattern
            ORDER BY Year DESC";

                var baseSlug = ConvertToSlug(searchTitle);
                command.Parameters.AddWithValue("$searchPattern", $"{baseSlug}%");
                command.Parameters.AddWithValue("$titlePattern", $"%{searchTitle}%");

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        movies.Add(CreateMovieFromReader(reader, userId));
                    }
                }
            }
            return movies;
        }

        public List<Movie> SearchMoviesInDatabase(string searchTerm, int userId = 0)
        {
            var movies = new List<Movie>();

            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT * FROM Movies 
            WHERE Title LIKE $searchTerm 
            OR Slug LIKE $searchTerm
            OR Genres LIKE $searchTerm
            ORDER BY 
                CASE 
                    WHEN Title = $exactTitle THEN 1
                    WHEN Title LIKE $startsWith THEN 2
                    ELSE 3
                END";

                command.Parameters.AddWithValue("$searchTerm", $"%{searchTerm}%");
                command.Parameters.AddWithValue("$exactTitle", searchTerm);
                command.Parameters.AddWithValue("$startsWith", $"{searchTerm}%");

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        movies.Add(CreateMovieFromReader(reader, userId));
                    }
                }
            }
            return movies;
        }

        private Movie CreateMovieFromReader(SQLiteDataReader reader, int userId = 0)
        {
            var movie = new Movie
            {
                Title = reader["Title"]?.ToString() ?? "",
                Slug = reader["Slug"]?.ToString() ?? "",
                Year = reader["Year"] != DBNull.Value ? Convert.ToInt32(reader["Year"]) : 0,
                Description = reader["Description"]?.ToString() ?? "",
                PosterUrl = reader["PosterUrl"]?.ToString() ?? "",
                LetterBoxdUrl = reader["LetterBoxdUrl"]?.ToString() ?? "",
                Poster = reader["Poster"]?.ToString() ?? "",
                VoteCount = reader["VoteCount"] != DBNull.Value ? Convert.ToInt32(reader["VoteCount"]) : 0,
                Rating = reader["Rating"] != DBNull.Value ? Convert.ToSingle(reader["Rating"]) : 0f
            };

            string genresJson = reader["Genres"]?.ToString();
            if (!string.IsNullOrEmpty(genresJson))
            {
                try
                {
                    movie.Genres = JsonSerializer.Deserialize<List<string>>(genresJson)
                        ?? new List<string>();
                }
                catch
                {
                    movie.Genres = new List<string>();
                }
            }
            else
            {
                movie.Genres = new List<string>();
            }

            // Проверяем, просмотрен ли фильм пользователем (если userId > 0)
            if (userId > 0)
            {
                movie.IsWatched = IsMovieWatched(userId, movie.Slug);
                movie.InWatchList = IsInWatchList(userId, movie.Slug); // Новый статус
            }

            return movie;
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
                using (var jsonDoc = JsonDocument.Parse(response))
                {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TMDB search error: {ex.Message}");
            }

            return ConvertToSlug(title);
        }

        private string ConvertToSlug(string title)
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

        public List<Movie> SearchAllMovieVariants(string searchTitle, int userId = 0)
        {
            var movies = new List<Movie>();

            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT * FROM Movies 
            WHERE Title LIKE $searchPattern 
            OR Slug LIKE $slugPattern
            ORDER BY Year DESC, Title";

                command.Parameters.AddWithValue("$searchPattern", $"%{searchTitle}%");
                command.Parameters.AddWithValue("$slugPattern", $"%{searchTitle.Replace(" ", "-")}%");

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        movies.Add(CreateMovieFromReader(reader, userId));
                    }
                }
            }
            return movies;
        }

        // ПОЛЬЗОВАТЕЛИ
        public bool AddUser(User user)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = "INSERT INTO Users (Login, Email, Password) VALUES ($login, $email, $password)";
                    command.Parameters.AddWithValue("$login", user.Login);
                    command.Parameters.AddWithValue("$email", user.Email);
                    command.Parameters.AddWithValue("$password", user.Password);

                    return command.ExecuteNonQuery() > 0;
                }
            }
            catch (SQLiteException ex) when (ex.Message.Contains("UNIQUE constraint failed"))
            {
                return false;
            }
        }

        public bool UserExistsByLogin(string login)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Users WHERE Login = $login";
                command.Parameters.AddWithValue("$login", login);

                var count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        public User GetUserByLogin(string login)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Users WHERE Login = $login";
                command.Parameters.AddWithValue("$login", login);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new User
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Login = reader["Login"]?.ToString() ?? "",
                            Email = reader["Email"]?.ToString() ?? "",
                            Password = reader["Password"]?.ToString() ?? ""
                        };
                    }
                }
            }
            return null;
        }

        public User FindUser(string login, string password)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Users WHERE Login = $login AND Password = $password";
                command.Parameters.AddWithValue("$login", login);
                command.Parameters.AddWithValue("$password", password);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new User
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Login = reader["Login"]?.ToString() ?? "",
                            Email = reader["Email"]?.ToString() ?? "",
                            Password = reader["Password"]?.ToString() ?? ""
                        };
                    }
                }
            }
            return null;
        }

        // Методы для работы с просмотренными фильмами
        public void MarkMovieAsWatched(int userId, string movieSlug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT OR IGNORE INTO WatchedMovies (UserId, MovieSlug)
                VALUES ($userId, $movieSlug)";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);

                command.ExecuteNonQuery();
            }
        }

        public void UnmarkMovieAsWatched(int userId, string movieSlug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                DELETE FROM WatchedMovies 
                WHERE UserId = $userId AND MovieSlug = $movieSlug";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);

                command.ExecuteNonQuery();
            }
        }

        public bool IsMovieWatched(int userId, string movieSlug)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT COUNT(*) FROM WatchedMovies 
                WHERE UserId = $userId AND MovieSlug = $movieSlug";

                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$movieSlug", movieSlug);

                var count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        public int GetWatchedMoviesCount(int userId)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT COUNT(*) FROM WatchedMovies 
                WHERE UserId = $userId";

                command.Parameters.AddWithValue("$userId", userId);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public List<Movie> GetWatchedMovies(int userId)
        {
            var movies = new List<Movie>();

            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT m.* FROM Movies m
            INNER JOIN WatchedMovies wm ON m.Slug = wm.MovieSlug
            WHERE wm.UserId = $userId
            ORDER BY wm.WatchedAt DESC";

                command.Parameters.AddWithValue("$userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var movie = CreateMovieFromReader(reader, userId);
                        movie.IsWatched = true; // Всегда true для этого метода
                        movies.Add(movie);
                    }
                }
            }
            return movies;
        }

        public int GetUserRatingsCount(int userId)
        {
            using (var connection = new SQLiteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM UserRatings WHERE UserId = $userId";
                command.Parameters.AddWithValue("$userId", userId);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}