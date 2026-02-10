

-- Таблица фильмов
CREATE TABLE IF NOT EXISTS movies (
    id SERIAL PRIMARY KEY,
    title TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE,
    year INTEGER,
    description TEXT,
    poster_url TEXT,
    letterboxd_url TEXT,
    poster TEXT,
    genres TEXT, -- Храним как JSON в TEXT поле
    vote_count INTEGER DEFAULT 0,
    rating FLOAT DEFAULT 0.0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Таблица пользователей
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    login TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    password TEXT NOT NULL,
    avatar_url TEXT DEFAULT 'default',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Таблица пользовательских оценок
CREATE TABLE IF NOT EXISTS user_ratings (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    movie_slug TEXT NOT NULL,
    rating INTEGER NOT NULL CHECK (rating >= 1 AND rating <= 10),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, movie_slug)
);

-- Таблица просмотренных фильмов
CREATE TABLE IF NOT EXISTS watched_movies (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    movie_slug TEXT NOT NULL,
    watched_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, movie_slug)
);

-- Таблица списка "Хочу посмотреть" (WatchList)
CREATE TABLE IF NOT EXISTS watch_list (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    movie_slug TEXT NOT NULL,
    added_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, movie_slug)
);

-- Создание индексов для ускорения поиска
CREATE INDEX IF NOT EXISTS idx_movies_title ON movies(title);
CREATE INDEX IF NOT EXISTS idx_movies_slug ON movies(slug);
CREATE INDEX IF NOT EXISTS idx_movies_year ON movies(year);
CREATE INDEX IF NOT EXISTS idx_movies_rating ON movies(rating DESC);
CREATE INDEX IF NOT EXISTS idx_movies_vote_count ON movies(vote_count DESC);

CREATE INDEX IF NOT EXISTS idx_user_ratings_user_id ON user_ratings(user_id);
CREATE INDEX IF NOT EXISTS idx_user_ratings_movie_slug ON user_ratings(movie_slug);
CREATE INDEX IF NOT EXISTS idx_user_ratings_rating ON user_ratings(rating);

CREATE INDEX IF NOT EXISTS idx_watched_movies_user_id ON watched_movies(user_id);
CREATE INDEX IF NOT EXISTS idx_watched_movies_movie_slug ON watched_movies(movie_slug);

CREATE INDEX IF NOT EXISTS idx_watch_list_user_id ON watch_list(user_id);
CREATE INDEX IF NOT EXISTS idx_watch_list_movie_slug ON watch_list(movie_slug);

-- Создание пользователя по умолчанию (если нужно)
-- Пароль: qweqwe (будет хеширован в приложении)
INSERT INTO users (login, display_name, email, password, avatar_url) 
VALUES ('qwe', 'Демо пользователь', 'demo@movierec.local', '', 'default')
ON CONFLICT (login) DO NOTHING;