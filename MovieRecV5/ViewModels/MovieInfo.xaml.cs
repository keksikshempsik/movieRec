using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        private bool _isFavorite = false;

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
            LoadFavoriteStatus();

            // Привязываем обработчики событий для кнопок
            WatchedButton.Click += WatchedButton_Click;
            WatchListButton.Click += WatchListButton_Click;
            FavoriteButton.Click += FavoriteButton_Click;
            SubmitRatingButton.Click += SubmitRatingButton_Click;
            BackButton.Click += BackButton_Click;

            LoadReviews();
            InitializeReviewPanel();

            ReviewTextBox.TextChanged += ReviewTextBox_TextChanged;
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
                try
                {
                    MoviePoster.Source = posterService.Base64ToBitmapImage(movie.Poster);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки постера: {ex.Message}");
                }
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

            // Подписываемся на событие загрузки элементов
            RatingStars.Loaded += (s, e) => UpdateStarsAppearance();
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

        private void LoadWatchListStatus()
        {
            if (_currentUserId > 0)
            {
                _isInWatchList = _databaseService.IsInWatchList(_currentUserId, _movie.Slug);

                // Если фильм просмотрен, он не должен быть в watchlist (кроме случая "хочу пересмотреть")
                if (_isWatched && _isInWatchList)
                {
                    // Оставляем в watchlist для возможности "хочу пересмотреть"
                    // Ничего не делаем
                }

                UpdateWatchListButton();
            }
            else
            {
                WatchListButton.IsEnabled = false;
                WatchListButton.ToolTip = "Для добавления в список необходимо войти в систему";
            }
        }

        private void LoadFavoriteStatus()
        {
            if (_currentUserId > 0)
            {
                _isFavorite = _databaseService.IsInFavorites(_currentUserId, _movie.Slug);
                UpdateFavoriteButton();
            }
            else
            {
                FavoriteButton.IsEnabled = false;
                FavoriteButton.ToolTip = "Для добавления в избранное необходимо войти в систему";
            }
        }

        private void UpdateWatchedButton()
        {
            if (_isWatched)
            {
                WatchedButton.Content = "✓ Просмотрено";
                WatchedButton.Background = new SolidColorBrush(Color.FromRgb(6, 182, 212)); // #06B6D4
                WatchedStatusText.Text = "Фильм отмечен как просмотренный";
                WatchedStatusText.Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212));
            }
            else
            {
                WatchedButton.Content = "Отметить как просмотренный";
                WatchedButton.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // #3B82F6
                WatchedStatusText.Text = "";
            }
        }

        private void UpdateWatchListButton()
        {
            if (_isWatched && _isInWatchList)
            {
                WatchListButton.Content = "📋 Хочу пересмотреть";
                WatchListButton.Background = new SolidColorBrush(Color.FromRgb(230, 126, 126)); // LightCoral
                WatchListStatusText.Text = "Хотите пересмотреть этот фильм";
                WatchListStatusText.Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 126));
            }
            else if (_isInWatchList)
            {
                WatchListButton.Content = "📋 В списке";
                WatchListButton.Background = new SolidColorBrush(Color.FromRgb(253, 203, 110)); // #FDCB6E
                WatchListStatusText.Text = "Фильм добавлен в список";
                WatchListStatusText.Foreground = new SolidColorBrush(Color.FromRgb(253, 203, 110));
            }
            else
            {
                WatchListButton.Content = "📋 Хочу посмотреть";
                WatchListButton.Background = new SolidColorBrush(Color.FromRgb(253, 203, 110)); // #FDCB6E
                WatchListStatusText.Text = "";
            }
        }

        private void UpdateFavoriteButton()
        {
            if (_isFavorite)
            {
                FavoriteButton.Content = "❤️ В избранном";
                FavoriteButton.Background = new SolidColorBrush(Color.FromRgb(225, 112, 85)); // #E17055
                FavoriteStatusText.Text = "Фильм в избранном";
                FavoriteStatusText.Foreground = new SolidColorBrush(Color.FromRgb(225, 112, 85));
            }
            else
            {
                FavoriteButton.Content = "❤️ В избранное";
                FavoriteButton.Background = new SolidColorBrush(Color.FromRgb(225, 112, 85)); // #E17055
                FavoriteStatusText.Text = "";
            }
        }

        private void WatchedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                ShowStatusMessage("Для отметки фильма необходимо войти в систему", true);
                return;
            }

            try
            {
                if (_isWatched)
                {
                    // Снимаем отметку о просмотре
                    _databaseService.UnmarkMovieAsWatched(_currentUserId, _movie.Slug);
                    _isWatched = false;

                    // Если была оценка, сбрасываем её (опционально)
                    if (currentRating > 0)
                    {
                        // Здесь можно добавить логику сброса оценки
                    }

                    ShowStatusMessage("✓ Отметка о просмотре снята", false);
                }
                else
                {
                    // Отмечаем как просмотренный
                    _databaseService.MarkMovieAsWatched(_currentUserId, _movie.Slug);
                    _isWatched = true;

                    ShowStatusMessage("✓ Фильм отмечен как просмотренный", false);
                }

                UpdateWatchedButton();
                UpdateWatchListButton(); // Обновляем, так как статус watchlist мог измениться
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"✗ Ошибка: {ex.Message}", true);
                Console.WriteLine($"Ошибка в WatchedButton_Click: {ex}");
            }
        }

        private void WatchListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                ShowStatusMessage("Для добавления в список необходимо войти в систему", true);
                return;
            }

            try
            {
                if (_isInWatchList)
                {
                    // Удаляем из списка
                    _databaseService.RemoveFromWatchList(_currentUserId, _movie.Slug);
                    _isInWatchList = false;
                    ShowStatusMessage("✓ Фильм удален из списка", false);
                }
                else
                {
                    // Добавляем в список
                    _databaseService.AddToWatchList(_currentUserId, _movie.Slug);
                    _isInWatchList = true;
                    ShowStatusMessage("✓ Фильм добавлен в список", false);
                }

                UpdateWatchListButton();
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"✗ Ошибка: {ex.Message}", true);
                Console.WriteLine($"Ошибка в WatchListButton_Click: {ex}");
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                ShowStatusMessage("Для добавления в избранное необходимо войти в систему", true);
                return;
            }

            try
            {
                if (_isFavorite)
                {
                    // Удаляем из избранного
                    _databaseService.RemoveFromFavorites(_currentUserId, _movie.Slug);
                    _isFavorite = false;
                    ShowStatusMessage("✓ Фильм удален из избранного", false);
                }
                else
                {
                    // Добавляем в избранное
                    _databaseService.AddToFavorites(_currentUserId, _movie.Slug);
                    _isFavorite = true;
                    ShowStatusMessage("✓ Фильм добавлен в избранное", false);
                }

                UpdateFavoriteButton();
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"✗ Ошибка: {ex.Message}", true);
                Console.WriteLine($"Ошибка в FavoriteButton_Click: {ex}");
            }
        }

        private void StarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                ShowStatusMessage("Для оценки необходимо войти в систему", true);
                return;
            }

            if (sender is Button button && button.Tag is int rating)
            {
                currentRating = rating;
                tempRating = rating;
                UpdateStarsAppearance();
                UpdateRatingText();
                SubmitRatingButton.IsEnabled = true;
            }
        }

        private void StarButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_currentUserId <= 0) return;

            if (sender is Button button && button.Tag is int rating)
            {
                tempRating = rating;
                UpdateStarsAppearance();
            }
        }

        private void StarButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_currentUserId <= 0) return;

            tempRating = currentRating;
            UpdateStarsAppearance();
        }

        private void UpdateStarsAppearance()
        {
            // Находим все кнопки звезд
            starButtons.Clear();
            for (int i = 0; i < 10; i++)
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

                            // Обновляем обработчики событий
                            button.Click -= StarButton_Click;
                            button.Click += StarButton_Click;
                            button.MouseEnter -= StarButton_MouseEnter;
                            button.MouseEnter += StarButton_MouseEnter;
                            button.MouseLeave -= StarButton_MouseLeave;
                            button.MouseLeave += StarButton_MouseLeave;
                        }
                    }
                }
            }

            // Обновляем цвет звезд
            for (int i = 0; i < starButtons.Count; i++)
            {
                if (i < tempRating)
                {
                    starButtons[i].Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // Gold
                    starButtons[i].Content = "★";
                }
                else
                {
                    starButtons[i].Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94A3B8
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
                ShowStatusMessage("Для оценки необходимо войти в систему", true);
                return;
            }

            if (currentRating == 0)
            {
                ShowStatusMessage("Выберите оценку", true);
                return;
            }

            try
            {
                // Сохраняем оценку
                _databaseService.SaveUserRating(_currentUserId, _movie.Slug, currentRating);

                // Обновляем рейтинг фильма
                _databaseService.UpdateMovieRating(_movie.Slug, currentRating);

                // Если фильм не был отмечен как просмотренный, отмечаем его
                if (!_isWatched)
                {
                    _databaseService.MarkMovieAsWatched(_currentUserId, _movie.Slug);
                    _isWatched = true;
                    UpdateWatchedButton();
                }

                // Если фильм был в watchlist, предлагаем оставить для пересмотра или удаляем
                if (_isInWatchList)
                {
                    // Оставляем решение за пользователем - можно оставить уведомление
                    // _isInWatchList = false;
                    // UpdateWatchListButton();
                }

                SubmitRatingButton.IsEnabled = false;
                RefreshMovieRating();

                ShowStatusMessage("✓ Оценка сохранена", false);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"✗ Ошибка: {ex.Message}", true);
                Console.WriteLine($"Ошибка в SubmitRatingButton_Click: {ex}");
            }
        }

        private void RefreshMovieRating()
        {
            try
            {
                // Обновляем информацию о фильме из базы данных
                var movies = _databaseService.SearchMoviesInDatabase(_movie.Title, _currentUserId);
                var updatedMovie = movies.FirstOrDefault(m => m.Slug == _movie.Slug);

                if (updatedMovie != null)
                {
                    _movie = updatedMovie;
                    MovieVoteCount.Text = $"{_movie.FormatVoteCount(_movie.VoteCount)} votes";
                    MovieRating.Text = $"Rating: {_movie.Rating:F1}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления рейтинга: {ex}");
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        // ===== МЕТОДЫ ДЛЯ ОТЗЫВОВ =====

        private void InitializeReviewPanel()
        {
            if (_currentUserId <= 0)
            {
                ReviewLoginPrompt.Visibility = Visibility.Visible;
                ReviewTextBox.Visibility = Visibility.Collapsed;
                SaveReviewButton.Visibility = Visibility.Collapsed;
                DeleteReviewButton.Visibility = Visibility.Collapsed;
                CancelEditReviewButton.Visibility = Visibility.Collapsed;
                ReviewCharCount.Visibility = Visibility.Collapsed;
            }
            else
            {
                ReviewLoginPrompt.Visibility = Visibility.Collapsed;
                ReviewTextBox.Visibility = Visibility.Visible;
                SaveReviewButton.Visibility = Visibility.Visible;
                ReviewCharCount.Visibility = Visibility.Visible;

                _currentUserReview = _databaseService.GetUserReview(_currentUserId, _movie.Slug);

                if (_currentUserReview != null)
                {
                    ReviewTextBox.Text = _currentUserReview.ReviewText;
                    SaveReviewButton.Content = "Обновить отзыв";
                    DeleteReviewButton.Visibility = Visibility.Visible;
                    SaveReviewButton.IsEnabled = true;
                }
                else
                {
                    ReviewTextBox.Text = "";
                    SaveReviewButton.Content = "Опубликовать отзыв";
                    DeleteReviewButton.Visibility = Visibility.Collapsed;
                    SaveReviewButton.IsEnabled = false;
                }

                UpdateCharCount();
            }
        }

        private void ReviewTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharCount();
            SaveReviewButton.IsEnabled = !string.IsNullOrWhiteSpace(ReviewTextBox.Text);
        }

        private void UpdateCharCount()
        {
            int count = ReviewTextBox.Text.Length;
            ReviewCharCount.Text = $"{count}/1000";
            ReviewCharCount.Foreground = count >= 1000 ? Brushes.Red : Brushes.Gray;
        }

        private void SaveReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserId <= 0)
            {
                ShowStatusMessage("Чтобы оставить отзыв, необходимо войти в систему", true);
                return;
            }

            string reviewText = ReviewTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(reviewText))
            {
                ShowStatusMessage("Введите текст отзыва", true);
                return;
            }

            if (reviewText.Length > 1000)
            {
                ShowStatusMessage("Отзыв не может быть длиннее 1000 символов", true);
                return;
            }

            try
            {
                _databaseService.SaveReview(_currentUserId, _movie.Slug, reviewText);
                _currentUserReview = _databaseService.GetUserReview(_currentUserId, _movie.Slug);
                SaveReviewButton.Content = "Обновить отзыв";
                DeleteReviewButton.Visibility = Visibility.Visible;
                CancelEditReviewButton.Visibility = Visibility.Collapsed;
                LoadReviews();
                ShowStatusMessage("✓ Отзыв сохранен", false);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"✗ Ошибка: {ex.Message}", true);
                Console.WriteLine($"Ошибка в SaveReviewButton_Click: {ex}");
            }
        }

        private void DeleteReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserReview == null) return;

            var result = MessageBox.Show("Вы уверены, что хотите удалить отзыв?",
                                         "Подтверждение",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                bool deleted = _databaseService.DeleteReview(_currentUserReview.Id, _currentUserId);

                if (deleted)
                {
                    ReviewTextBox.Text = "";
                    SaveReviewButton.Content = "Опубликовать отзыв";
                    DeleteReviewButton.Visibility = Visibility.Collapsed;
                    CancelEditReviewButton.Visibility = Visibility.Collapsed;
                    SaveReviewButton.IsEnabled = false;
                    _currentUserReview = null;
                    LoadReviews();
                    ShowStatusMessage("✓ Отзыв удален", false);
                }
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"✗ Ошибка: {ex.Message}", true);
                Console.WriteLine($"Ошибка в DeleteReviewButton_Click: {ex}");
            }
        }

        private void CancelEditReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserReview != null)
            {
                ReviewTextBox.Text = _currentUserReview.ReviewText;
            }
            else
            {
                ReviewTextBox.Text = "";
            }

            CancelEditReviewButton.Visibility = Visibility.Collapsed;
            SaveReviewButton.Content = _currentUserReview != null ? "Обновить отзыв" : "Опубликовать отзыв";
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
                Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(8)
            };

            var stackPanel = new StackPanel();

            // Шапка с информацией о пользователе
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

            // Аватар (инициалы)
            var avatarBorder = new Border
            {
                Width = 28,
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 8, 0)
            };

            var avatarText = new TextBlock
            {
                Text = GetInitials(review.UserDisplayName),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarBorder.Child = avatarText;
            headerPanel.Children.Add(avatarBorder);

            // Имя и дата
            var nameDatePanel = new StackPanel();

            var nameText = new TextBlock
            {
                Text = review.UserDisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = Brushes.White
            };
            nameDatePanel.Children.Add(nameText);

            var dateText = new TextBlock
            {
                Text = FormatReviewDate(review.UpdatedAt),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            nameDatePanel.Children.Add(dateText);

            headerPanel.Children.Add(nameDatePanel);
            stackPanel.Children.Add(headerPanel);

            // Текст отзыва
            var reviewText = new TextBlock
            {
                Text = review.ReviewText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 8)
            };
            stackPanel.Children.Add(reviewText);

            // Кнопка редактирования (только для своего отзыва)
            if (review.CanEdit)
            {
                var editButton = new Button
                {
                    Content = "✏️ Редактировать",
                    FontSize = 11,
                    Height = 28,
                    Width = 100,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Tag = review
                };

                // Упрощенный шаблон для кнопки
                editButton.Template = CreateSimpleButtonTemplate();
                editButton.Click += EditReviewButton_Click;

                stackPanel.Children.Add(editButton);
            }

            border.Child = stackPanel;
            return border;
        }

        private ControlTemplate CreateSimpleButtonTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            factory.SetValue(Border.PaddingProperty, new Thickness(8, 3, 8, 3));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            factory.AppendChild(contentFactory);

            return new ControlTemplate(typeof(Button)) { VisualTree = factory };
        }

        private void EditReviewButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var review = button?.Tag as Review;

            if (review != null)
            {
                ReviewTextBox.Text = review.ReviewText;
                SaveReviewButton.Content = "Обновить отзыв";
                DeleteReviewButton.Visibility = Visibility.Visible;
                CancelEditReviewButton.Visibility = Visibility.Visible;
                SaveReviewButton.IsEnabled = true;
                _isEditingReview = true;
                ReviewTextBox.Focus();
            }
        }

        private string GetInitials(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "??";
            var parts = displayName.Split(' ');
            if (parts.Length >= 2) return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            return displayName.Length >= 2 ? displayName.Substring(0, 2).ToUpper() : displayName.ToUpper();
        }

        private string FormatReviewDate(DateTime date)
        {
            var now = DateTime.Now;
            var diff = now - date;

            if (diff.TotalMinutes < 1) return "только что";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} мин. назад";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} ч. назад";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} дн. назад";
            return date.ToString("dd.MM.yyyy");
        }

        private void ShowStatusMessage(string message, bool isError)
        {
            var statusBar = new Border
            {
                Background = isError ? new SolidColorBrush(Color.FromRgb(214, 48, 49)) : new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 0),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 11
            };

            statusBar.Child = textBlock;
            ReviewsListPanel.Children.Insert(0, statusBar);

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) =>
            {
                ReviewsListPanel.Children.Remove(statusBar);
                timer.Stop();
            };
            timer.Start();
        }
    }
}