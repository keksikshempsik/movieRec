using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;

namespace MovieRecV5
{
    public partial class MainWindow : Window
    {
        private DatabaseService _databaseService;
        private readonly SemaphoreSlim _throttler;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _databaseService.InitializeDatabase();
            _throttler = new SemaphoreSlim(3, 3);

            SearchTextBox.KeyDown += SearchTextBox_KeyDown;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetProgressStatus(true);
                LetterboxdParser parser = new LetterboxdParser();

                if (SearchTextBox.Text == "Введите название..." || string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    MessageBox.Show("Введите название фильма");
                    return;
                }

                string searchTitle = SearchTextBox.Text.Trim();
                Console.WriteLine($"🔍 Поиск: '{searchTitle}'");

                // 1. Быстрый поиск в базе
                var baseSlug = parser.ConvertToSlug(searchTitle);
                var moviesFromDb = _databaseService.GetMoviesFromDatabase(searchTitle);

                if (moviesFromDb.Count >= 4)
                {
                    Console.WriteLine($"📁 Найдено в базе: {moviesFromDb.Count} фильмов");
                    DisplayMovies(moviesFromDb.Take(8).ToList());
                    return;
                }

                List<Movie> movies = new List<Movie>(moviesFromDb);

                // 2. Ограниченный онлайн-поиск
                var possibleSlugs = GenerateSlugsWithYears(baseSlug).Take(15);
                Console.WriteLine($"🔄 Поиск {possibleSlugs.Count()} slugs");

                var onlineTasks = new List<Task<Movie>>();

                foreach (var slug in possibleSlugs)
                {
                    if (movies.Any(m => m.Slug == slug)) continue;

                    onlineTasks.Add(TryGetMovieWithThrottle(slug, parser));
                }

                var onlineResults = await Task.WhenAll(onlineTasks);
                var newMovies = onlineResults.Where(m => m != null).ToList();

                // Сохраняем найденные фильмы в базу
                foreach (var movie in newMovies)
                {
                    if (!_databaseService.MovieExists(movie.Slug))
                    {
                        _databaseService.AddMovie(movie);
                    }
                }

                movies.AddRange(newMovies);

                // 3. Показываем результаты
                if (!movies.Any())
                {
                    MessageBox.Show("Фильмы не найдены.");
                    return;
                }

                var finalMovies = movies
                    .GroupBy(m => m.Slug)
                    .Select(g => g.First())
                    .OrderByDescending(m => m.Year)
                    .Take(8)
                    .ToList();

                Console.WriteLine($"🎬 Найдено: {finalMovies.Count} фильмов");
                DisplayMovies(finalMovies);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                SetProgressStatus(false);
            }
        }

        private async Task<Movie> TryGetMovieWithThrottle(string slug, LetterboxdParser parser)
        {
            await _throttler.WaitAsync();
            try
            {
                Console.WriteLine($"🌐 Парсим: {slug}");
                return await parser.TryParseMovie(slug);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка {slug}: {ex.Message}");
                return null;
            }
            finally
            {
                _throttler.Release();
            }
        }

        private List<string> GenerateSlugsWithYears(string baseSlug)
        {
            var slugs = new List<string> { baseSlug };

            var keyYears = new[] {
                2024, 2023, 2022, 2021, 2020,
                2019, 2018, 2017, 2016, 2015,
                2012, 2010, 2008, 2005, 2000,
                1999, 1998, 1995, 1990,
                1980, 1982, 1970, 1960, 1950
            };

            foreach (var year in keyYears)
            {
                slugs.Add($"{baseSlug}-{year}");
            }

            return slugs.Distinct().ToList();
        }

        private void DisplayMovies(List<Movie> movies)
        {
            MoviesPanel.Children.Clear(); // Изменил с MoviesGrid на MoviesPanel

            if (movies == null || !movies.Any())
            {
                var noResultsText = new TextBlock
                {
                    Text = "Фильмы не найдены",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16,
                    Foreground = Brushes.Gray
                };
                MoviesPanel.Children.Add(noResultsText);
                return;
            }

            foreach (var movie in movies.Take(8))
            {
                var movieButton = CreateMovieButton(movie);
                MoviesPanel.Children.Add(movieButton);
            }
        }

        private Button CreateMovieButton(Movie movie)
        {
            var button = new Button
            {
                Margin = new Thickness(10),
                Padding = new Thickness(0),
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Width = 160, // Фиксированная ширина
                Height = 280 // Фиксированная высота
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Контейнер для постера с фиксированными размерами
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

            // Жанры
            if (movie.Genres != null && movie.Genres.Any())
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
                    // Если ошибка - показываем заглушку
                }
            }

            // Заглушка
            image.Source = CreatePlaceholderImage();
            return image;
        }

        private ImageSource CreatePlaceholderImage()
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Серый фон
                context.DrawRectangle(Brushes.LightGray, new Pen(Brushes.Gray, 1),
                    new Rect(0, 0, 140, 200));

                // Текст
                var text = new FormattedText(
                    "Нет изображения",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    12,
                    Brushes.Gray,
                    1.0
                );

                // Центрируем текст
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
            var movieInfoPage = new MovieInfo(movie); // Передаем фильм в конструктор

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
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Введите название...")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = Brushes.Black;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Введите название...";
                SearchTextBox.Foreground = Brushes.Gray;
            }
        }

        private void SetProgressStatus(bool isProgress)
        {
            SearchProgressBar.IsIndeterminate = isProgress;
            SearchButton.IsEnabled = !isProgress;
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
        }
    }
}