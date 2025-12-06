using Amazon.Translate;
using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using MovieRecV5.Models;
using MovieRecV5.Services;

namespace MovieRecV5.ViewModels
{
    public partial class MovieInfo : Page
    {
        private Movie _movie;
        private bool _isTranslated;
        private string _originalDescription;
        private int currentRating = 0;
        private int tempRating = 0;
        private List<Button> starButtons = new List<Button>();
        private DatabaseService _databaseService;
        private int _currentUserId;
        private bool _isWatched = false;
        private bool _isInWatchList = false;

        public MovieInfo(Movie movie, int userId = 0)
        {
            InitializeComponent();
            _movie = movie;
            _currentUserId = userId;
            _databaseService = new DatabaseService();
            ShowMovieInfo(_movie);
            _isTranslated = false;
            _originalDescription = _movie.Description;
            InitializeRatingStars();
            LoadUserRating();
            LoadWatchedStatus(); // Загружаем статус просмотра
            LoadWatchListStatus();
        }

        public MovieInfo() : this(new Movie()) { }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void ShowMovieInfo(Movie movie)
        {
            var posterService = new MoviePosterService();

            // Устанавливаем значения напрямую в элементы
            MovieTitle.Text = movie.Title;
            MovieDescription.Text = movie.Description;
            MovieYear.Text = movie.Year.ToString();
            MovieVoteCount.Text = $"{movie.FormatVoteCount(movie.VoteCount)} votes";
            MovieRating.Text = $"Rating: {movie.Rating:F1}";

            // Устанавливаем постер
            if (!string.IsNullOrEmpty(movie.Poster))
            {
                MoviePoster.Source = posterService.Base64ToBitmapImage(movie.Poster);
            }

            // Заполняем жанры
            if (movie.Genres != null && movie.Genres.Count > 0)
            {
                GenresList.ItemsSource = movie.Genres;
            }
        }

        private void InitializeRatingStars()
        {
            // Создаем 10 звезд
            var starValues = Enumerable.Range(1, 10).ToList();
            RatingStars.ItemsSource = starValues;
        }

        private void LoadUserRating()
        {
            if (_currentUserId > 0)
            {
                var userRating = _databaseService.GetUserRating(_currentUserId, _movie.Slug);
                if (userRating.HasValue)
                {
                    currentRating = userRating.Value;
                    tempRating = currentRating;
                    UpdateStarsAppearance();
                    UpdateRatingText();
                    SubmitRatingButton.IsEnabled = false; // Оценка уже сохранена
                }
            }
        }

        private void StarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int rating)
            {
                currentRating = rating;
                tempRating = rating;
                UpdateStarsAppearance();
                UpdateRatingText();
                SubmitRatingButton.IsEnabled = true;
            }
        }

        private void StarButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Button button && button.Tag is int rating)
            {
                tempRating = rating;
                UpdateStarsAppearance();
            }
        }

        private void StarButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            tempRating = currentRating;
            UpdateStarsAppearance();
        }

        private void UpdateStarsAppearance()
        {
            if (RatingStars.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                RatingStars.UpdateLayout();
            }

            starButtons.Clear();
            for (int i = 0; i < RatingStars.Items.Count; i++)
            {
                var container = RatingStars.ItemContainerGenerator.ContainerFromIndex(i);
                if (container != null)
                {
                    var contentPresenter = container as ContentPresenter;
                    if (contentPresenter != null)
                    {
                        var button = FindVisualChild<Button>(contentPresenter);
                        if (button != null)
                        {
                            starButtons.Add(button);
                        }
                    }
                }
            }

            for (int i = 0; i < starButtons.Count; i++)
            {
                if (i < tempRating)
                {
                    starButtons[i].Foreground = Brushes.Gold;
                    starButtons[i].Content = "★";
                }
                else
                {
                    starButtons[i].Foreground = Brushes.LightGray;
                    starButtons[i].Content = "★";
                }
            }
        }

        private void UpdateRatingText()
        {
            SelectedRatingText.Text = $"{currentRating}/10";
        }

        private void SubmitRatingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                MessageBox.Show("Для оценки фильма необходимо войти в систему", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _databaseService.SaveUserRating(_currentUserId, _movie.Slug, currentRating);

                _databaseService.UpdateMovieRating(_movie.Slug, currentRating);

                RefreshMovieRating();

                SubmitRatingButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения оценки: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshMovieRating()
        {
            var movies = _databaseService.SearchMoviesInDatabase(_movie.Title);
            var updatedMovie = movies.FirstOrDefault(m => m.Slug == _movie.Slug);

            if (updatedMovie != null)
            {
                _movie = updatedMovie;
                MovieVoteCount.Text = $"{_movie.FormatVoteCount(_movie.VoteCount)} votes";
                MovieRating.Text = $"Rating: {_movie.Rating:F1}";
            }
        }
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                else
                {
                    var descendant = FindVisualChild<T>(child);
                    if (descendant != null)
                        return descendant;
                }
            }
            return null;
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MovieDescription.Text))
                    return;

                TranslateButton.IsEnabled = false;

                if (_isTranslated)
                {
                    MovieDescription.Text = _originalDescription;
                    TranslateButton.Content = "🌐 Перевести описание";
                    _isTranslated = false;
                }
                else
                {
                    TranslateButton.Content = "⏳ Перевод...";
                    var translateService = new TranslateService();
                    string translatedText = await translateService.TranslateTextAsync(MovieDescription.Text);

                    if (!string.IsNullOrEmpty(translatedText) && translatedText != MovieDescription.Text)
                    {
                        MovieDescription.Text = translatedText;
                        TranslateButton.Content = "🔁 Оригинал";
                        _isTranslated = true;
                    }
                    else
                    {
                        TranslateButton.Content = "🌐 Перевести описание";
                        MessageBox.Show("Перевод не удался", "Информация",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка перевода: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                TranslateButton.Content = "🌐 Перевести описание";
            }
            finally
            {
                TranslateButton.IsEnabled = true;
            }
        }

        private void RatingStars_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStarsAppearance();
        }

        private void StarButton_Loaded(object sender, RoutedEventArgs e)
        {
            // Этот метод может быть пустым, но он должен существовать если объявлен в XAML
        }

        private void LoadWatchedStatus()
        {
            if (_currentUserId > 0)
            {
                _isWatched = _databaseService.IsMovieWatched(_currentUserId, _movie.Slug);
                UpdateWatchedButton();
            }
            else
            {
                WatchedButton.IsEnabled = false;
                WatchedButton.ToolTip = "Для отметки фильма необходимо войти в систему";
            }
        }

        private void UpdateWatchedButton()
        {
            if (_isWatched)
            {
                WatchedButton.Content = "Просмотрено ✓";
                WatchedButton.Background = Brushes.LightGreen;
                WatchedStatusText.Text = "Фильм отмечен как просмотренный";
            }
            else
            {
                WatchedButton.Content = "Отметить как просмотренный";
                WatchedButton.Background = Brushes.LightBlue;
                WatchedStatusText.Text = "";
            }

            UpdateWatchListButton();
        }

        private void WatchedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                MessageBox.Show("Для отметки фильма необходимо войти в систему", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_isWatched)
                {
                    _databaseService.UnmarkMovieAsWatched(_currentUserId, _movie.Slug);
                    _isWatched = false;
                }
                else
                {
                    // Добавляем отметку
                    _databaseService.MarkMovieAsWatched(_currentUserId, _movie.Slug);
                    _isWatched = true;
                }

                UpdateWatchedButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWatchListStatus()
        {
            if (_currentUserId > 0)
            {
                _isInWatchList = _databaseService.IsInWatchList(_currentUserId, _movie.Slug);
                UpdateWatchListButton();
            }
            else
            {
                WatchListButton.IsEnabled = false;
                WatchListButton.ToolTip = "Для добавления в список 'Хочу посмотреть' необходимо войти в систему";
            }
        }

        private void UpdateWatchListButton()
        {
            if (_isWatched && _isInWatchList)
            {
                WatchListButton.Content = "Хочу пересмотреть ✓";
                WatchListButton.Background = Brushes.LightCoral;
                WatchListStatusText.Text = "Хотите пересмотреть этот фильм";
                WatchListStatusText.Foreground = Brushes.Red;
            }
            else if (_isInWatchList)
            {
                WatchListButton.Content = "В списке 'Хочу посмотреть' ✓";
                WatchListButton.Background = Brushes.LightYellow; 
                WatchListStatusText.Text = "Фильм добавлен в список 'Хочу посмотреть'";
                WatchListStatusText.Foreground = Brushes.Orange;
            }
            else
            {
                WatchListButton.Content = "Хочу посмотреть";
                WatchListButton.Background = Brushes.LightYellow;
                WatchListStatusText.Text = "";
            }
        }

        private void WatchListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                MessageBox.Show("Для добавления в список 'Хочу посмотреть' необходимо войти в систему", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_isInWatchList)
                {
                    // Удаляем из WatchList
                    _databaseService.RemoveFromWatchList(_currentUserId, _movie.Slug);
                    _isInWatchList = false;
                }
                else
                {
                    // Добавляем в WatchList
                    _databaseService.AddToWatchList(_currentUserId, _movie.Slug);
                    _isInWatchList = true;
                }

                UpdateWatchListButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


    }
}