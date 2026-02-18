using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace MovieRecV5.ViewModels
{
    public static class MovieCardHelper
    {
        public static Button CreateMovieCard(Movie movie, int userId, PostgresDatabaseService dbService, Action<Movie> onClick)
        {
            bool isInWatchList = dbService.IsInWatchList(userId, movie.Slug);
            bool isWatched = dbService.IsMovieWatched(userId, movie.Slug);
            bool isFavorite = dbService.IsInFavorites(userId, movie.Slug);

            movie.IsWatched = isWatched;
            movie.InWatchList = isInWatchList;
            movie.IsFavorite = isFavorite;

            // Определяем цвет фона в зависимости от статусов
            Brush cardBackground;
            if (isWatched && isInWatchList)
                cardBackground = new SolidColorBrush(Color.FromRgb(230, 126, 126)); // LightCoral
            else if (isFavorite)
                cardBackground = new SolidColorBrush(Color.FromRgb(255, 200, 220)); // LightPink
            else if (isInWatchList)
                cardBackground = new SolidColorBrush(Color.FromRgb(255, 235, 156)); // LightYellow
            else if (isWatched)
                cardBackground = new SolidColorBrush(Color.FromRgb(162, 222, 162)); // LightGreen
            else
                cardBackground = new SolidColorBrush(Color.FromRgb(45, 45, 45)); // #2D2D2D

            var button = new Button
            {
                Style = (Style)Application.Current.FindResource("MovieCardStyle"),
                Background = cardBackground,
                ToolTip = $"{movie.Title}\n★ {movie.Rating:F1}/10 • {movie.FormatVoteCount(movie.VoteCount)} оценок"
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), // #1E1E1E
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10)
            };

            var stackPanel = new StackPanel();

            // Постер с эффектом тени
            var posterBorder = new Border
            {
                Width = 160,
                Height = 220,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)), // #2D2D2D
                Margin = new Thickness(0, 0, 0, 10)
            };

            var posterImage = CreatePosterImage(movie);
            posterImage.Width = 160;
            posterImage.Height = 220;
            posterBorder.Child = posterImage;

            posterBorder.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 10,
                Opacity = 0.3
            };

            stackPanel.Children.Add(posterBorder);

            // Иконки статусов
            var statusPanel = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            if (isWatched)
                statusPanel.Children.Add(CreateStatusBadge("✓", "#00B894"));
            if (isInWatchList)
                statusPanel.Children.Add(CreateStatusBadge("📋", "#FDCB6E"));
            if (isFavorite)
                statusPanel.Children.Add(CreateStatusBadge("❤️", "#E17055"));

            if (statusPanel.Children.Count > 0)
                stackPanel.Children.Add(statusPanel);

            // Название
            var titleText = new TextBlock
            {
                Text = movie.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxHeight = 40,
                Margin = new Thickness(0, 5, 0, 3)
            };
            stackPanel.Children.Add(titleText);

            // Рейтинг
            var ratingPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            ratingPanel.Children.Add(new TextBlock
            {
                Text = "★",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                FontSize = 14,
                Margin = new Thickness(0, 0, 3, 0)
            });

            ratingPanel.Children.Add(new TextBlock
            {
                Text = $"{movie.Rating:F1}/10",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                FontSize = 12,
                FontWeight = FontWeights.Bold
            });

            ratingPanel.Children.Add(new TextBlock
            {
                Text = $" ({movie.FormatVoteCount(movie.VoteCount)})",
                Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)), // #B0B0B0
                FontSize = 10,
                Margin = new Thickness(3, 0, 0, 0)
            });

            stackPanel.Children.Add(ratingPanel);

            // Жанры (первые два)
            if (movie.Genres != null && movie.Genres.Count > 0)
            {
                var genresText = new TextBlock
                {
                    Text = string.Join(", ", movie.Genres.Take(2)),
                    Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)), // #B0B0B0
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 30,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                stackPanel.Children.Add(genresText);
            }

            border.Child = stackPanel;
            button.Content = border;
            button.Click += (s, e) => onClick(movie);

            return button;
        }

        private static Border CreateStatusBadge(string icon, string color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(2)
            };

            border.Child = new TextBlock
            {
                Text = icon,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            return border;
        }

        private static Image CreatePosterImage(Movie movie)
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(movie.Poster) && movie.Poster != "null")
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
                catch { }
            }

            image.Source = CreatePlaceholderImage();
            return image;
        }

        private static ImageSource CreatePlaceholderImage()
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                    new Pen(new SolidColorBrush(Color.FromRgb(90, 90, 90)), 1),
                    new Rect(0, 0, 140, 200));

                var text = new FormattedText(
                    "Нет изображения",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    new SolidColorBrush(Color.FromRgb(176, 176, 176)),
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
    }
}