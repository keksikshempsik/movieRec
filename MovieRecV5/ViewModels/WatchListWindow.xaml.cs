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
    public partial class WatchListWindow : Window
    {
        private User _currentUser;
        private DatabaseService _databaseService;
        private List<Movie> _watchListMovies;

        public WatchListWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            _databaseService = new DatabaseService();
            LoadWatchList();
        }

        private void LoadWatchList()
        {
            // Получаем фильмы из WatchList
            _watchListMovies = _databaseService.GetWatchListMovies(_currentUser.Id);

            if (_watchListMovies.Count == 0)
            {
                MoviesPanel.Children.Clear();
                NoMoviesGrid.Visibility = Visibility.Visible;
                return;
            }

            NoMoviesGrid.Visibility = Visibility.Collapsed;
            DisplayMovies(_watchListMovies);
        }

        private void DisplayMovies(List<Movie> movies)
        {
            MoviesPanel.Children.Clear();

            foreach (var movie in movies)
            {
                var movieButton = CreateMovieButton(movie);
                MoviesPanel.Children.Add(movieButton);
            }
        }

        private Button CreateMovieButton(Movie movie)
        {
            // Определяем цвет в зависимости от статусов
            Brush backgroundColor;
            Brush borderColor;

            if (movie.IsWatched)
            {
                backgroundColor = Brushes.LightCoral; // Красный - просмотрен и хочет пересмотреть
                borderColor = Brushes.Red;
            }
            else
            {
                backgroundColor = Brushes.LightYellow; // Желтый - в WatchList
                borderColor = Brushes.Orange;
            }

            var button = new Button
            {
                Margin = new Thickness(10),
                Padding = new Thickness(0),
                Background = backgroundColor,
                BorderBrush = borderColor,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                Width = 160,
                Height = 280
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Контейнер для постера
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

            // Статус фильма
            var statusText = new TextBlock
            {
                Text = movie.IsWatched ? "✓ Просмотрено (хочет пересмотреть)" : "Хочу посмотреть",
                FontSize = 10,
                Foreground = movie.IsWatched ? Brushes.Red : Brushes.Orange,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                FontWeight = FontWeights.Bold
            };
            stackPanel.Children.Add(statusText);

            // Текстовая информация
            var textContainer = new StackPanel
            {
                Margin = new Thickness(5, 8, 5, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 140
            };

            // Название и год
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

            // Рейтинг
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

            // Жанры
            if (movie.Genres != null && movie.Genres.Count > 0)
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
                catch
                {
                    // Заглушка
                }
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

            // Обновляем список после закрытия окна с информацией о фильме
            LoadWatchList();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void FindMoviesButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}