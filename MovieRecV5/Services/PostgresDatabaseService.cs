using MovieRecV5.Models;
using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MovieRecV5.Services
{
    public class PostgresDatabaseService
    {
        private string _connectionString;
        private readonly HttpClient _httpClient;
        private string _databasePath = "";

        public PostgresDatabaseService()
        {
            try
            {
                _connectionString = ConfigurationManager.ConnectionStrings["PostgreSQLConnection"].ConnectionString;

                _httpClient = new HttpClient();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ КРИТИЧЕСКАЯ ОШИБКА в конструкторе PostgresDatabaseService:");
                Console.WriteLine($"Сообщение: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        public string GetDatabasePath()
        {
            return _databasePath; 
        }

        public void InitializeDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string[] createTables = {
                // Таблица пользователей
                @"CREATE TABLE IF NOT EXISTS users (
                    id SERIAL PRIMARY KEY,
                    login TEXT NOT NULL UNIQUE,
                    display_name TEXT NOT NULL,
                    email TEXT NOT NULL UNIQUE,
                    password TEXT NOT NULL,
                    avatar_url TEXT DEFAULT 'default',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )",
                
                // Таблица фильмов
                @"CREATE TABLE IF NOT EXISTS movies (
                    id SERIAL PRIMARY KEY,
                    title TEXT NOT NULL,
                    slug TEXT NOT NULL UNIQUE,
                    year INTEGER,
                    description TEXT,
                    poster_url TEXT,
                    letterboxd_url TEXT,
                    poster TEXT,
                    genres TEXT,
                    vote_count INTEGER DEFAULT 0,
                    rating FLOAT DEFAULT 0.0,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )",
                
                // Таблица оценок
                @"CREATE TABLE IF NOT EXISTS user_ratings (
                    id SERIAL PRIMARY KEY,
                    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    movie_slug TEXT NOT NULL,
                    rating INTEGER NOT NULL CHECK (rating >= 1 AND rating <= 10),
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(user_id, movie_slug)
                )",
                
                // Таблица просмотренных фильмов
                @"CREATE TABLE IF NOT EXISTS watched_movies (
                    id SERIAL PRIMARY KEY,
                    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    movie_slug TEXT NOT NULL,
                    watched_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(user_id, movie_slug)
                )",
                
                // Таблица watch list
                @"CREATE TABLE IF NOT EXISTS watch_list (
                    id SERIAL PRIMARY KEY,
                    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    movie_slug TEXT NOT NULL,
                    added_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(user_id, movie_slug)
                )"
            };

                    for (int i = 0; i < createTables.Length; i++)
                    {
                        using (var command = new NpgsqlCommand(createTables[i], connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }

                    AddDefaultUserIfNotExists();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА инициализации базы данных: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Внутренняя ошибка: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // 1. МЕТОДЫ ДЛЯ ФИЛЬМОВ
        public void AddMovie(Movie movie)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
                    INSERT INTO movies (title, slug, year, description, poster_url, 
                                      letterboxd_url, poster, genres, vote_count, rating)
                    VALUES (@title, @slug, @year, @description, @posterUrl, 
                           @letterboxdUrl, @poster, @genres, @voteCount, @rating)
                    ON CONFLICT (slug) DO NOTHING", connection);

                command.Parameters.AddWithValue("@title", movie.Title ?? "");
                command.Parameters.AddWithValue("@slug", movie.Slug ?? "");
                command.Parameters.AddWithValue("@year", movie.Year);
                command.Parameters.AddWithValue("@description", movie.Description ?? "");
                command.Parameters.AddWithValue("@posterUrl", movie.PosterUrl ?? "");
                command.Parameters.AddWithValue("@letterboxdUrl", movie.LetterBoxdUrl ?? "");
                command.Parameters.AddWithValue("@poster", movie.Poster ?? "");
                command.Parameters.AddWithValue("@genres", JsonConvert.SerializeObject(movie.Genres ?? new List<string>()));
                command.Parameters.AddWithValue("@voteCount", movie.VoteCount);
                command.Parameters.AddWithValue("@rating", movie.Rating);

                command.ExecuteNonQuery();
            }
        }

        public bool MovieExists(string slug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand("SELECT COUNT(*) FROM movies WHERE slug = @slug", connection);
                command.Parameters.AddWithValue("@slug", slug);

                var count = Convert.ToInt64(command.ExecuteScalar());
                return count > 0;
            }
        }

        public List<Movie> GetMoviesFromDatabase(string searchTitle, int userId = 0)
        {
            var movies = new List<Movie>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
                    SELECT * FROM movies 
                    WHERE LOWER(title) LIKE @searchPattern 
                       OR LOWER(slug) LIKE @slugPattern
                       OR genres::text LIKE @searchPattern
                    ORDER BY vote_count DESC, rating DESC", connection);

                var searchTerm = searchTitle.ToLower();
                var slugPattern = $"%{searchTerm.Replace(" ", "-")}%";

                command.Parameters.AddWithValue("@searchPattern", $"%{searchTerm}%");
                command.Parameters.AddWithValue("@slugPattern", slugPattern);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var movie = CreateMovieFromReader(reader, userId);
                        if (!string.IsNullOrEmpty(movie.Poster) && movie.Poster != "null")
                        {
                            movies.Add(movie);
                        }
                    }
                }
            }
            return movies;
        }

        // 2. МЕТОДЫ ДЛЯ ПОЛЬЗОВАТЕЛЕЙ
        public bool AddUser(User user)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    var command = new NpgsqlCommand(@"
                INSERT INTO users (login, display_name, email, password, avatar_url)
                VALUES (@login, @displayName, @email, @password, @avatarUrl)
                ON CONFLICT (login) DO NOTHING", connection);

                    command.Parameters.AddWithValue("@login", user.Login);
                    command.Parameters.AddWithValue("@displayName",
                        string.IsNullOrEmpty(user.DisplayName) ? user.Login : user.DisplayName);
                    command.Parameters.AddWithValue("@email", user.Email);
                    command.Parameters.AddWithValue("@password", user.Password);
                    command.Parameters.AddWithValue("@avatarUrl",
                        string.IsNullOrEmpty(user.AvatarUrl) ? "default" : user.AvatarUrl);

                    return command.ExecuteNonQuery() > 0;
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // unique violation
            {
                return false;
            }
        }

        public User GetUserByLogin(string login)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand("SELECT * FROM users WHERE login = @login", connection);
                command.Parameters.AddWithValue("@login", login);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new User
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Login = reader["login"]?.ToString() ?? "",
                            DisplayName = reader["display_name"]?.ToString() ?? "", // ПРАВИЛЬНО
                            Email = reader["email"]?.ToString() ?? "",
                            Password = reader["password"]?.ToString() ?? "",
                            AvatarUrl = reader["avatar_url"]?.ToString() ?? "default" // ПРАВИЛЬНО
                        };
                    }
                }
            }
            return null;
        }

        public bool UserExistsByLogin(string login)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE login = @login", connection);
                command.Parameters.AddWithValue("@login", login);

                var count = Convert.ToInt64(command.ExecuteScalar());
                return count > 0;
            }
        }

        // 3. МЕТОДЫ ДЛЯ РЕЙТИНГОВ
        public void SaveUserRating(int userId, string movieSlug, int rating)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
                    INSERT INTO user_ratings (user_id, movie_slug, rating)
                    VALUES (@userId, @movieSlug, @rating)
                    ON CONFLICT (user_id, movie_slug) 
                    DO UPDATE SET rating = @rating, created_at = CURRENT_TIMESTAMP", connection);

                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@movieSlug", movieSlug);
                command.Parameters.AddWithValue("@rating", rating);

                command.ExecuteNonQuery();
            }
        }

        public int? GetUserRating(int userId, string movieSlug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(
                    "SELECT rating FROM user_ratings WHERE user_id = @userId AND movie_slug = @movieSlug",
                    connection);
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@movieSlug", movieSlug);

                var result = command.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : (int?)null;
            }
        }

        // 4. МЕТОДЫ ДЛЯ ПРОСМОТРЕННЫХ ФИЛЬМОВ
        public void MarkMovieAsWatched(int userId, string movieSlug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var command = new NpgsqlCommand(@"
                    INSERT INTO watched_movies (user_id, movie_slug)  -- watched_movies с underscore!
                    VALUES (@userId, @movieSlug)
                    ON CONFLICT (user_id, movie_slug) DO NOTHING", connection);
                        command.Parameters.AddWithValue("@userId", userId);
                        command.Parameters.AddWithValue("@movieSlug", movieSlug);
                        command.ExecuteNonQuery();

                        // Удаляем из WatchList если там был
                        command = new NpgsqlCommand(@"
                    DELETE FROM watch_list 
                    WHERE user_id = @userId AND movie_slug = @movieSlug", connection);
                        command.Parameters.AddWithValue("@userId", userId);
                        command.Parameters.AddWithValue("@movieSlug", movieSlug);
                        command.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool IsMovieWatched(int userId, string movieSlug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM watched_movies WHERE user_id = @userId AND movie_slug = @movieSlug",  // watched_movies с underscore!
                    connection);
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@movieSlug", movieSlug);

                var count = Convert.ToInt64(command.ExecuteScalar());
                return count > 0;
            }
        }

        // 5. МЕТОДЫ ДЛЯ WATCH LIST
        public void AddToWatchList(int userId, string movieSlug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
                    INSERT INTO watch_list (user_id, movie_slug)
                    VALUES (@userId, @movieSlug)
                    ON CONFLICT (user_id, movie_slug) DO NOTHING", connection);
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@movieSlug", movieSlug);

                command.ExecuteNonQuery();
            }
        }

        public bool IsInWatchList(int userId, string movieSlug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM watch_list WHERE user_id = @userId AND movie_slug = @movieSlug",
                    connection);
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@movieSlug", movieSlug);

                var count = Convert.ToInt64(command.ExecuteScalar());
                return count > 0;
            }
        }

        // 6. ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ

        private void AddDefaultUserIfNotExists()
        {
            try
            {
                if (!UserExistsByLogin("qwe"))
                {
                    var defaultUser = new User
                    {
                        Login = "qwe",
                        DisplayName = "Демо пользователь",
                        Email = "demo@movierec.local",
                        Password = User.HashPassword("qweqwe"),
                        AvatarUrl = "default"
                    };

                    bool added = AddUser(defaultUser);
                }
                else
                {
                    Console.WriteLine("✅ Пользователь qwe уже существует");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при добавлении пользователя по умолчанию: {ex.Message}");
            }
        }

        // ВОТЧЛИСТ

        public void RemoveFromWatchList(int userId, string movieSlug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            DELETE FROM watch_list
            WHERE user_id = @userId AND movie_slug = @movieSlug";

                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@movieSlug", movieSlug);

                command.ExecuteNonQuery();
            }
        }

        public int GetWatchListCount(int userId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT COUNT(*) FROM watch_list
            WHERE user_id = @userId";

                command.Parameters.AddWithValue("@userId", userId);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public List<Movie> GetWatchListMovies(int userId)
        {
            var movies = new List<Movie>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
            SELECT m.* FROM movies m
            INNER JOIN watch_list wl ON m.slug = wl.movie_slug
            WHERE wl.user_id = @userId
            ORDER BY wl.added_at DESC", connection);

                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var movie = CreateMovieFromReader(reader, userId);
                        movie.IsWatched = IsMovieWatched(userId, movie.Slug);
                        movies.Add(movie);
                    }
                }
            }
            return movies;
        }

        // РЕЙТИНГИ

        public void UpdateMovieRating(string movieSlug, int userRating)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                // 1. Сначала получаем текущие данные
                int currentVoteCount = 0;
                float currentRating = 0f;

                using (var getCommand = new NpgsqlCommand(
                    "SELECT vote_count, rating FROM movies WHERE slug = @slug",
                    connection))
                {
                    getCommand.Parameters.AddWithValue("@slug", movieSlug);

                    using (var reader = getCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            currentVoteCount = reader["vote_count"] != DBNull.Value ?
                                Convert.ToInt32(reader["vote_count"]) : 0;
                            currentRating = reader["rating"] != DBNull.Value ?
                                Convert.ToSingle(reader["rating"]) : 0f;
                        }
                    }
                }

                // 2. Закрыли reader, теперь можем выполнить update
                int newVoteCount = currentVoteCount + 1;
                float newRating = ((currentRating * currentVoteCount) + userRating) / newVoteCount;

                using (var updateCommand = new NpgsqlCommand(@"
            UPDATE movies 
            SET vote_count = @voteCount, rating = @rating 
            WHERE slug = @slug", connection))
                {
                    updateCommand.Parameters.AddWithValue("@voteCount", newVoteCount);
                    updateCommand.Parameters.AddWithValue("@rating", newRating);
                    updateCommand.Parameters.AddWithValue("@slug", movieSlug);

                    updateCommand.ExecuteNonQuery();
                }
            }
        }

        // ФИЛЬМЫ

        public List<Movie> SearchMoviesInDatabase(string searchTerm, int userId = 0, int minVotes = 100)
        {
            var movies = new List<Movie>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
            SELECT * FROM movies 
            WHERE (LOWER(title) LIKE @searchTerm 
               OR LOWER(slug) LIKE @slugPattern
               OR genres::text LIKE @searchTerm)
            AND vote_count >= @minVotes
            ORDER BY vote_count DESC, rating DESC, year DESC
            LIMIT 50", connection);

                var searchTermLower = searchTerm.ToLower();
                var slugPattern = $"%{searchTermLower.Replace(" ", "-")}%";

                command.Parameters.AddWithValue("@searchTerm", $"%{searchTermLower}%");
                command.Parameters.AddWithValue("@slugPattern", slugPattern);
                command.Parameters.AddWithValue("@minVotes", minVotes);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var movie = CreateMovieFromReader(reader, userId);

                        if (!string.IsNullOrEmpty(movie.Poster) && movie.Poster != "null")
                        {
                            movies.Add(movie);
                        }
                    }
                }
            }
            return movies;
        }

        private Movie CreateMovieFromReader(NpgsqlDataReader reader, int userId = 0)
        {
            var movie = new Movie
            {
                Id = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0,
                Title = reader["title"]?.ToString() ?? "",
                Slug = reader["slug"]?.ToString() ?? "",
                Year = reader["year"] != DBNull.Value ? Convert.ToInt32(reader["year"]) : 0,
                Description = reader["description"]?.ToString() ?? "",
                PosterUrl = reader["poster_url"]?.ToString() ?? "",
                LetterBoxdUrl = reader["letterboxd_url"]?.ToString() ?? "",
                Poster = reader["poster"]?.ToString() ?? "",
                VoteCount = reader["vote_count"] != DBNull.Value ? Convert.ToInt32(reader["vote_count"]) : 0,
                Rating = reader["rating"] != DBNull.Value ? Convert.ToSingle(reader["rating"]) : 0f
            };

            string genresJson = reader["genres"]?.ToString();
            if (!string.IsNullOrEmpty(genresJson))
            {
                try
                {
                    // Используем Newtonsoft.Json вместо JsonSerializer
                    movie.Genres = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(genresJson)
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

            if (userId > 0)
            {
                movie.IsWatched = IsMovieWatched(userId, movie.Slug);
                movie.InWatchList = IsInWatchList(userId, movie.Slug);
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

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
            SELECT * FROM movies 
            WHERE title LIKE @searchPattern 
            OR slug LIKE @slugPattern
            ORDER BY year DESC, title", connection);

                command.Parameters.AddWithValue("@searchPattern", $"%{searchTitle}%");
                command.Parameters.AddWithValue("@slugPattern", $"%{searchTitle.Replace(" ", "-")}%");

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

        public bool MovieExistsByTitleAndYear(string title, int year)
        {
            using (var connection = new NpgsqlConnection(_connectionString)) 
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
        SELECT COUNT(*) FROM movies 
        WHERE LOWER(title) = @title AND year = @year";

                command.Parameters.AddWithValue("@title", title.ToLower());
                command.Parameters.AddWithValue("@year", year);

                var count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        public Movie GetMovieByTmdbId(int tmdbId, int userId = 0)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
            SELECT * FROM movies 
            WHERE id = @tmdbId", connection);

                command.Parameters.AddWithValue("@tmdbId", tmdbId);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return CreateMovieFromReader(reader, userId);
                    }
                }
            }
            return null;
        }

        // ПОЛЬЗОВАТЕЛИ

        public User FindUser(string login, string password)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    var command = new NpgsqlCommand(
                        "SELECT * FROM users WHERE login = @login",
                        connection);

                    command.Parameters.AddWithValue("@login", login);

                    using (var reader = command.ExecuteReader())
                    {

                        if (reader.Read())
                        {
                            int id = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : 0;
                            string dbLogin = reader["login"]?.ToString() ?? "";
                            string displayName = reader["display_name"]?.ToString() ?? "";
                            string email = reader["email"]?.ToString() ?? "";
                            string storedPassword = reader["password"]?.ToString() ?? "";
                            string avatarUrl = reader["avatar_url"]?.ToString() ?? "default";

                            string hashedInputPassword = User.HashPassword(password);

                            if (storedPassword == hashedInputPassword)
                            {
                                return new User
                                {
                                    Id = id,
                                    Login = dbLogin,
                                    DisplayName = displayName,
                                    Email = email,
                                    Password = storedPassword,
                                    AvatarUrl = avatarUrl
                                };
                            }
                            else
                            {
                                Console.WriteLine("❌ Хэши НЕ совпадают");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Пользователь не найден в базе данных");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ИСКЛЮЧЕНИЕ в FindUser: {ex.GetType().Name}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Внутреннее исключение: {ex.InnerException.Message}");
                }

                throw;
            }
            finally
            {
                Console.WriteLine($"=== КОНЕЦ ОТЛАДКИ FindUser ===\n");
            }

            return null;
        }

        public User FindUserByLogin(string login)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(
                    "SELECT * FROM users WHERE login = @login",
                    connection);
                command.Parameters.AddWithValue("@login", login);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new User
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Login = reader["login"]?.ToString() ?? "",
                            DisplayName = reader["display_name"]?.ToString() ?? "",
                            Email = reader["email"]?.ToString() ?? "",
                            Password = reader["password"]?.ToString() ?? "",
                            AvatarUrl = reader["avatar_url"]?.ToString() ?? "default"
                        };
                    }
                }
            }
            return null;
        }

        public bool UpdateUserProfile(int userId, string displayName, string email, string avatarUrl)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    var command = new NpgsqlCommand(@"
                UPDATE users 
                SET display_name = @displayName, 
                    email = @email, 
                    avatar_url = @avatarUrl
                WHERE id = @userId", connection);

                    command.Parameters.AddWithValue("@displayName", displayName);
                    command.Parameters.AddWithValue("@email", email);
                    command.Parameters.AddWithValue("@avatarUrl", avatarUrl);
                    command.Parameters.AddWithValue("@userId", userId);

                    return command.ExecuteNonQuery() > 0;
                }
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"Error updating user: {ex.Message}");
                Console.WriteLine($"SQL State: {ex.SqlState}");
                Console.WriteLine($"Detail: {ex.Detail}");
                return false;
            }
        }


        // ПРОСМОТРЕННЫЕ ФИЛЬМЫ
        public void UnmarkMovieAsWatched(int userId, string movieSlug)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                DELETE FROM watched_movies
                WHERE user_id = @userId AND movie_slug = @movieSlug";

                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@movieSlug", movieSlug);

                command.ExecuteNonQuery();
            }
        }

        public int GetWatchedMoviesCount(int userId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT COUNT(*) FROM watched_movies
                WHERE user_id = @userId";

                command.Parameters.AddWithValue("@userId", userId);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public List<Movie> GetWatchedMovies(int userId)
        {
            var movies = new List<Movie>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(@"
            SELECT m.* FROM movies m
            INNER JOIN watched_movies wm ON m.slug = wm.movie_slug  -- watched_movies с underscore!
            WHERE wm.user_id = @userId
            ORDER BY wm.watched_at DESC", connection);

                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var movie = CreateMovieFromReader(reader, userId);
                        movie.IsWatched = true;
                        movies.Add(movie);
                    }
                }
            }
            return movies;
        }

        public int GetUserRatingsCount(int userId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM user_ratings WHERE user_id = @userId",
                    connection);

                command.Parameters.AddWithValue("@userId", userId);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        // СТАТИСТИКА

        public User.UserStats GetUserStats(int userId)
        {
            var stats = new User.UserStats();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // 1. Распределение по жанрам
                    using (var command = new NpgsqlCommand(@"
                SELECT m.genres 
                FROM movies m
                INNER JOIN watched_movies wm ON m.slug = wm.movie_slug
                WHERE wm.user_id = @userId", connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string genresJson = reader["genres"]?.ToString();
                                if (!string.IsNullOrEmpty(genresJson))
                                {
                                    try
                                    {
                                        var genres = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(genresJson);
                                        if (genres != null && genres.Count > 0)
                                        {
                                            var firstGenre = genres[0];
                                            if (stats.GenreDistribution.ContainsKey(firstGenre))
                                                stats.GenreDistribution[firstGenre]++;
                                            else
                                                stats.GenreDistribution[firstGenre] = 1;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Ошибка десериализации жанров: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }

                    // 2. Распределение по годам
                    using (var command = new NpgsqlCommand(@"
                SELECT m.year, COUNT(*) as count
                FROM movies m
                INNER JOIN watched_movies wm ON m.slug = wm.movie_slug
                WHERE wm.user_id = @userId
                GROUP BY m.year
                ORDER BY count DESC", connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int year = reader["year"] != DBNull.Value ? Convert.ToInt32(reader["year"]) : 0;
                                int count = Convert.ToInt32(reader["count"]);
                                stats.YearDistribution[year] = count;
                            }
                        }
                    }

                    // 3. Распределение по оценкам
                    using (var command = new NpgsqlCommand(@"
                SELECT ur.rating, COUNT(*) as count
                FROM user_ratings ur
                INNER JOIN watched_movies wm ON ur.user_id = wm.user_id AND ur.movie_slug = wm.movie_slug
                WHERE ur.user_id = @userId
                GROUP BY ur.rating
                ORDER BY ur.rating", connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int rating = Convert.ToInt32(reader["rating"]);
                                int count = Convert.ToInt32(reader["count"]);
                                stats.RatingDistribution[rating] = count;
                            }
                        }
                    }

                    // 4. Timeline оценок
                    using (var command = new NpgsqlCommand(@"
                SELECT ur.rating, ur.created_at
                FROM user_ratings ur
                INNER JOIN watched_movies wm ON ur.user_id = wm.user_id AND ur.movie_slug = wm.movie_slug
                WHERE ur.user_id = @userId
                ORDER BY ur.created_at", connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["created_at"] != DBNull.Value)
                                {
                                    var point = new User.RatingDatePoint
                                    {
                                        Rating = Convert.ToInt32(reader["rating"]),
                                        Date = Convert.ToDateTime(reader["created_at"])
                                    };
                                    stats.RatingTimeline.Add(point);
                                }
                            }
                        }
                    }
                }

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка получения статистики: {ex.Message}");
                if (ex is PostgresException pgEx)
                {
                    Console.WriteLine($"Код ошибки PostgreSQL: {pgEx.SqlState}");
                    Console.WriteLine($"Сообщение: {pgEx.MessageText}");
                    Console.WriteLine($"Детали: {pgEx.Detail}");
                }
                throw;
            }
        }

        public bool TestConnection()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    return connection.State == ConnectionState.Open;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        public bool UpdateUserPassword(int userId, string newPassword)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    var command = new NpgsqlCommand(
                        "UPDATE users SET password = @password WHERE id = @userId",
                        connection);

                    // УБЕДИТЕСЬ, что пароль хэшируется здесь
                    string hashedPassword = User.HashPassword(newPassword);
                    Console.WriteLine($"Обновление пароля для userId={userId}");
                    Console.WriteLine($"Хэшированный пароль: {hashedPassword}");

                    command.Parameters.AddWithValue("@password", hashedPassword);  // Должен быть хэш
                    command.Parameters.AddWithValue("@userId", userId);

                    int rowsAffected = command.ExecuteNonQuery();
                    Console.WriteLine($"Строк обновлено: {rowsAffected}");

                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating password: {ex.Message}");
                return false;
            }
        }

        //==============

        public bool EmailExists(string email)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var command = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM users WHERE email = @email",
                    connection);
                command.Parameters.AddWithValue("@email", email);

                var count = Convert.ToInt64(command.ExecuteScalar());
                return count > 0;
            }
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}