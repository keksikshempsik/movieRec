using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MovieRecV5.ViewModels
{
    public partial class MovieInfo : Page
    {
        private Movie _movie;
        private int currentRating = 0;
        private int tempRating = 0;
        private List<Button> starButtons = new List<Button>();
        private PostgresDatabaseService _databaseService;
        private int _currentUserId;
        private bool _isWatched = false;
        private bool _isInWatchList = false;

        // ===== НОВЫЕ ПОЛЯ ДЛЯ ОТЗЫВОВ =====
        private Review _currentUserReview;
        private bool _isEditingReview = false;

        public MovieInfo(Movie movie, int userId = 0)
        {
            InitializeComponent();
            _movie = movie;
            _currentUserId = userId;
            _databaseService = new PostgresDatabaseService();

            ShowMovieInfo(_movie);
            InitializeRatingStars();
            LoadUserRating();
            LoadWatchedStatus();
            LoadWatchListStatus();

            // ===== ЗАГРУЗКА ОТЗЫВОВ =====
            LoadReviews();
            InitializeReviewPanel();

            // Подписываемся на изменение текста отзыва
            ReviewTextBox.TextChanged += ReviewTextBox_TextChanged;
        }

        public MovieInfo() : this(new Movie()) { }

        // ===== МЕТОДЫ ДЛЯ ОТЗЫВОВ =====

        private void InitializeReviewPanel()
        {
            if (_currentUserId <= 0)
            {
                // Пользователь не авторизован
                ReviewLoginPrompt.Visibility = Visibility.Visible;
                ReviewTextBox.Visibility = Visibility.Collapsed;
                SaveReviewButton.Visibility = Visibility.Collapsed;
                DeleteReviewButton.Visibility = Visibility.Collapsed;
                CancelEditReviewButton.Visibility = Visibility.Collapsed;
                ReviewCharCount.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Пользователь авторизован
                ReviewLoginPrompt.Visibility = Visibility.Collapsed;
                ReviewTextBox.Visibility = Visibility.Visible;
                SaveReviewButton.Visibility = Visibility.Visible;
                ReviewCharCount.Visibility = Visibility.Visible;

                // Проверяем, есть ли уже отзыв
                _currentUserReview = _databaseService.GetUserReview(_currentUserId, _movie.Slug);

                if (_currentUserReview != null)
                {
                    // Уже есть отзыв - показываем в режиме просмотра
                    ReviewTextBox.Text = _currentUserReview.ReviewText;
                    SaveReviewButton.Content = "Обновить отзыв";
                    DeleteReviewButton.Visibility = Visibility.Visible;
                    _isEditingReview = false;
                }
                else
                {
                    // Нет отзыва - показываем плейсхолдер
                    ReviewTextBox.Text = "Напишите ваш отзыв...";
                    ReviewTextBox.Foreground = Brushes.Gray;
                    SaveReviewButton.Content = "Опубликовать отзыв";
                    DeleteReviewButton.Visibility = Visibility.Collapsed;
                    SaveReviewButton.IsEnabled = false;
                    _isEditingReview = false;
                }

                UpdateCharCount();
            }
        }

        private void ReviewTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ReviewTextBox.Text == "Напишите ваш отзыв..." && ReviewTextBox.Foreground == Brushes.Gray)
            {
                ReviewTextBox.Text = "";
                ReviewTextBox.Foreground = Brushes.Black;
            }
        }

        private void ReviewTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ReviewTextBox.Text))
            {
                ReviewTextBox.Text = "Напишите ваш отзыв...";
                ReviewTextBox.Foreground = Brushes.Gray;
                SaveReviewButton.IsEnabled = false;
            }
            else
            {
                SaveReviewButton.IsEnabled = true;
            }
        }

        private void ReviewTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharCount();

            // Включаем/выключаем кнопку сохранения
            if (ReviewTextBox.Text.Length > 0 &&
                ReviewTextBox.Text != "Напишите ваш отзыв..." &&
                !string.IsNullOrWhiteSpace(ReviewTextBox.Text))
            {
                SaveReviewButton.IsEnabled = true;
            }
            else
            {
                SaveReviewButton.IsEnabled = false;
            }
        }

        private void UpdateCharCount()
        {
            int count = ReviewTextBox.Text.Length;
            if (ReviewTextBox.Text == "Напишите ваш отзыв..." && ReviewTextBox.Foreground == Brushes.Gray)
            {
                count = 0;
            }

            ReviewCharCount.Text = $"{count}/1000";

            if (count >= 1000)
            {
                ReviewCharCount.Foreground = Brushes.Red;
            }
            else
            {
                ReviewCharCount.Foreground = Brushes.Gray;
            }
        }

        private async void SaveReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                MessageBox.Show("Чтобы оставить отзыв, необходимо войти в систему",
                              "Требуется авторизация",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string reviewText = ReviewTextBox.Text.Trim();

            if (reviewText == "Напишите ваш отзыв..." || string.IsNullOrWhiteSpace(reviewText))
            {
                MessageBox.Show("Введите текст отзыва", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (reviewText.Length > 1000)
            {
                MessageBox.Show("Отзыв не может быть длиннее 1000 символов", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Сохраняем отзыв
                _databaseService.SaveReview(_currentUserId, _movie.Slug, reviewText);

                // Обновляем интерфейс
                _currentUserReview = _databaseService.GetUserReview(_currentUserId, _movie.Slug);
                SaveReviewButton.Content = "Обновить отзыв";
                DeleteReviewButton.Visibility = Visibility.Visible;

                // Перезагружаем список отзывов
                LoadReviews();

                MessageBox.Show("Отзыв успешно сохранен!", "Успех",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отзыва: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserReview == null) return;

            var result = MessageBox.Show("Вы уверены, что хотите удалить ваш отзыв?",
                                        "Подтверждение удаления",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool deleted = _databaseService.DeleteReview(_currentUserReview.Id, _currentUserId);

                    if (deleted)
                    {
                        // Сбрасываем интерфейс
                        ReviewTextBox.Text = "Напишите ваш отзыв...";
                        ReviewTextBox.Foreground = Brushes.Gray;
                        SaveReviewButton.Content = "Опубликовать отзыв";
                        DeleteReviewButton.Visibility = Visibility.Collapsed;
                        CancelEditReviewButton.Visibility = Visibility.Collapsed;
                        SaveReviewButton.IsEnabled = false;
                        _currentUserReview = null;

                        // Перезагружаем список отзывов
                        LoadReviews();

                        MessageBox.Show("Отзыв удален", "Успех",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении отзыва: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CancelEditReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserReview != null)
            {
                // Возвращаем исходный текст
                ReviewTextBox.Text = _currentUserReview.ReviewText;
                ReviewTextBox.Foreground = Brushes.Black;
            }
            else
            {
                // Возвращаем плейсхолдер
                ReviewTextBox.Text = "Напишите ваш отзыв...";
                ReviewTextBox.Foreground = Brushes.Gray;
            }

            CancelEditReviewButton.Visibility = Visibility.Collapsed;
            _isEditingReview = false;
        }

        private void LoadReviews()
        {
            ReviewsListPanel.Children.Clear();

            var reviews = _databaseService.GetMovieReviews(_movie.Slug, _currentUserId);

            if (!reviews.Any())
            {
                NoReviewsText.Visibility = Visibility.Visible;
                return;
            }

            NoReviewsText.Visibility = Visibility.Collapsed;

            foreach (var review in reviews)
            {
                ReviewsListPanel.Children.Add(CreateReviewControl(review));
            }
        }

        private UIElement CreateReviewControl(Review review)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(5)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Шапка с информацией о пользователе
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

            // Аватар (инициалы)
            var avatarBorder = new Border
            {
                Width = 25,
                Height = 25,
                Background = Brushes.LightGray,
                CornerRadius = new CornerRadius(12.5),
                Margin = new Thickness(0, 0, 5, 0)
            };

            var avatarText = new TextBlock
            {
                Text = GetInitials(review.UserDisplayName),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarBorder.Child = avatarText;
            headerStack.Children.Add(avatarBorder);

            // Имя пользователя и дата
            var nameDateStack = new StackPanel { Orientation = Orientation.Vertical };

            var nameText = new TextBlock
            {
                Text = review.UserDisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            nameDateStack.Children.Add(nameText);

            var dateText = new TextBlock
            {
                Text = FormatReviewDate(review.UpdatedAt),
                FontSize = 9,
                Foreground = Brushes.Gray
            };
            nameDateStack.Children.Add(dateText);

            headerStack.Children.Add(nameDateStack);

            // Если отзыв редактировался, показываем метку
            if ((review.UpdatedAt - review.CreatedAt).TotalMinutes > 1)
            {
                var editedLabel = new TextBlock
                {
                    Text = " (ред.)",
                    FontSize = 9,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(5, 0, 0, 0)
                };
                headerStack.Children.Add(editedLabel);
            }

            Grid.SetRow(headerStack, 0);
            grid.Children.Add(headerStack);

            // Текст отзыва
            var reviewText = new TextBlock
            {
                Text = review.ReviewText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 5)
            };
            Grid.SetRow(reviewText, 1);
            grid.Children.Add(reviewText);

            // Кнопка редактирования (только для своего отзыва)
            if (review.CanEdit)
            {
                var editButton = new Button
                {
                    Content = "✏️ Редактировать",
                    FontSize = 10,
                    Height = 20,
                    Width = 80,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 0, 0),
                    Tag = review
                };
                editButton.Click += EditReviewButton_Click;

                Grid.SetRow(editButton, 2);
                grid.Children.Add(editButton);
            }

            border.Child = grid;
            return border;
        }

        private void EditReviewButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var review = button?.Tag as Review;

            if (review != null)
            {
                // Загружаем отзыв в редактор
                ReviewTextBox.Text = review.ReviewText;
                ReviewTextBox.Foreground = Brushes.Black;
                SaveReviewButton.Content = "Обновить отзыв";
                DeleteReviewButton.Visibility = Visibility.Visible;
                CancelEditReviewButton.Visibility = Visibility.Visible;
                SaveReviewButton.IsEnabled = true;
                _isEditingReview = true;

                // Прокручиваем к редактору
                ReviewTextBox.Focus();
                ReviewTextBox.CaretIndex = ReviewTextBox.Text.Length;
            }
        }

        private string GetInitials(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "??";

            var parts = displayName.Split(' ');
            if (parts.Length >= 2)
            {
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            }

            return displayName.Length >= 2 ? displayName.Substring(0, 2).ToUpper() : displayName.ToUpper();
        }

        private string FormatReviewDate(DateTime date)
        {
            var now = DateTime.Now;
            var diff = now - date;

            if (diff.TotalMinutes < 1)
                return "только что";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} мин. назад";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} ч. назад";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} дн. назад";

            return date.ToString("dd.MM.yyyy");
        }

        // ===== СУЩЕСТВУЮЩИЕ МЕТОДЫ =====
        // (все остальные методы остаются без изменений)
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void ShowMovieInfo(Movie movie)
        {
            var posterService = new MoviePosterService();

            MovieTitle.Text = movie.Title;
            MovieDescription.Text = movie.Description;
            MovieYear.Text = movie.Year.ToString();
            MovieVoteCount.Text = $"{movie.FormatVoteCount(movie.VoteCount)} votes";
            MovieRating.Text = $"Rating: {movie.Rating:F1}";

            if (!string.IsNullOrEmpty(movie.Poster))
            {
                MoviePoster.Source = posterService.Base64ToBitmapImage(movie.Poster);
            }

            if (movie.Genres != null && movie.Genres.Count > 0)
            {
                GenresList.ItemsSource = movie.Genres;
            }
        }

        private void InitializeRatingStars()
        {
            var starValues = Enumerable.Range(1, 10).ToList();
            RatingStars.ItemsSource = starValues;

            foreach (var item in RatingStars.Items)
            {
                var container = RatingStars.ItemContainerGenerator.ContainerFromItem(item);
                if (container is ContentPresenter contentPresenter)
                {
                    var button = FindVisualChild<Button>(contentPresenter);
                    if (button != null)
                    {
                        button.MouseDoubleClick += StarButton_DoubleClick;
                    }
                }
            }
        }

        private void StarButton_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (currentRating > 0 && _isWatched)
            {
                var result = MessageBox.Show("Сбросить оценку? Это также снимет отметку о просмотре.",
                                           "Подтверждение",
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _databaseService.UnmarkMovieAsWatched(_currentUserId, _movie.Slug);

                    _isWatched = false;
                    currentRating = 0;
                    tempRating = 0;

                    UpdateWatchedButton();
                    UpdateStarsAppearance();
                    UpdateRatingText();
                    SubmitRatingButton.IsEnabled = true;
                }
            }
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
                    SubmitRatingButton.IsEnabled = false;

                    if (!_isWatched)
                    {
                        _databaseService.MarkMovieAsWatched(_currentUserId, _movie.Slug);
                        _isWatched = true;
                        UpdateWatchedButton();
                    }
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

                if (!_isWatched)
                {
                    _databaseService.MarkMovieAsWatched(_currentUserId, _movie.Slug);
                    _isWatched = true;
                }

                if (_isInWatchList)
                {
                    _isInWatchList = false;
                }

                UpdateWatchedButton();
                UpdateWatchListButton();

                RefreshMovieRating();

                SubmitRatingButton.IsEnabled = false;
                UpdateStarsAppearance();

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

        private void RatingStars_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStarsAppearance();
        }

        private void StarButton_Loaded(object sender, RoutedEventArgs e)
        {

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

                    currentRating = 0;
                    tempRating = 0;
                    UpdateStarsAppearance();
                    UpdateRatingText();
                    SubmitRatingButton.IsEnabled = true;
                }
                else
                {
                    _databaseService.MarkMovieAsWatched(_currentUserId, _movie.Slug);
                    _isWatched = true;

                    _isInWatchList = false;
                }

                UpdateWatchedButton();
                UpdateWatchListButton();
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

                if (_isWatched && _isInWatchList)
                {
                    _databaseService.RemoveFromWatchList(_currentUserId, _movie.Slug);
                    _isInWatchList = false;
                }

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
            else if (_isWatched)
            {
                WatchListButton.Content = "Хочу пересмотреть";
                WatchListButton.Background = Brushes.LightBlue;
                WatchListStatusText.Text = "Добавить для повторного просмотра";
                WatchListStatusText.Foreground = Brushes.Blue;
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
                    _databaseService.RemoveFromWatchList(_currentUserId, _movie.Slug);
                    _isInWatchList = false;
                }
                else
                {
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