using Amazon.Translate;
using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using MovieRecV5.Models;
using MovieRecV5.Services;

namespace MovieRecV5.ViewModels
{
    public partial class MovieInfo : Page
    {
        private Movie _movie;
        private bool _isTranslated;
        private string _originalDescription;

        public MovieInfo(Movie movie)
        {
            InitializeComponent();
            _movie = movie;
            ShowMovieInfo(_movie);
            _isTranslated = false;
            _originalDescription = _movie.Description;
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
            MovieRating.Text = $"Rating: {Convert.ToString(movie.Rating)}";

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

        // Временный метод для рейтинга - замените на ваш
        private string GetRating(Movie movie)
        {
            // Заглушка - верните реальный рейтинг из ваших данных
            return "7.5";
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
                    // Возвращаем оригинальный текст
                    MovieDescription.Text = _originalDescription;
                    TranslateButton.Content = "🌐 Перевести описание";
                    _isTranslated = false;
                }
                else
                {
                    // Переводим текст
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
    }
}