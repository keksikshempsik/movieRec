using System.Windows;
using System.Windows.Controls;

namespace MovieRecV5
{
    public partial class MovieInfo : Page
    {
        private Movie _movie;

        // Добавляем конструктор с параметром
        public MovieInfo(Movie movie)
        {
            InitializeComponent();
            _movie = movie;
            DataContext = _movie; // Устанавливаем DataContext
            ShowMovieInfo(_movie);
        }

        public MovieInfo() : this(new Movie()) { } // Пустой конструктор для дизайнера

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void ShowMovieInfo(Movie movie)
        {
            var posterService = new MoviePosterService();
            MovieTitle.Text = movie.Title;
            MovieDescription.Text = movie.Description;
            MoviePoster.Source = posterService.Base64ToBitmapImage(movie.Poster);
        }
    }
}