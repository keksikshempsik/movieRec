using MovieRecV5.Models;
using MovieRecV5.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MovieRecV5.ViewModels
{
    public partial class FiltersWindow : Window
    {
        private PostgresDatabaseService _databaseService;
        public MovieFilters Filters { get; private set; }
        private int _userId;

        public FiltersWindow(MovieFilters currentFilters, int userId = 0)
        {
            InitializeComponent();

            _databaseService = new PostgresDatabaseService();
            _userId = userId;
            Filters = currentFilters ?? new MovieFilters();

            LoadGenres();
            LoadFilters();
            UpdateUserFiltersVisibility();
        }

        private void LoadGenres()
        {
            try
            {
                var genres = _databaseService.GetAllGenres();
                GenresItemsControl.ItemsSource = genres;

                // Отмечаем выбранные жанры
                foreach (var item in GenresItemsControl.Items)
                {
                    var container = GenresItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (container != null)
                    {
                        var checkBox = FindVisualChild<CheckBox>(container);
                        if (checkBox != null && Filters.SelectedGenres.Contains(checkBox.Content.ToString()))
                        {
                            checkBox.IsChecked = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFilters()
        {
            // Сортировка
            foreach (ComboBoxItem item in SortByComboBox.Items)
            {
                if (item.Tag.ToString() == Filters.SortBy)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            SortDescendingCheckBox.IsChecked = Filters.SortDescending;

            // Годы
            if (Filters.YearFrom.HasValue)
                YearFromTextBox.Text = Filters.YearFrom.Value.ToString();
            if (Filters.YearTo.HasValue)
                YearToTextBox.Text = Filters.YearTo.Value.ToString();

            // Рейтинг
            if (Filters.RatingFrom.HasValue)
                RatingFromTextBox.Text = Filters.RatingFrom.Value.ToString("F1");
            if (Filters.RatingTo.HasValue)
                RatingToTextBox.Text = Filters.RatingTo.Value.ToString("F1");
            if (Filters.VotesFrom.HasValue)
                VotesFromTextBox.Text = Filters.VotesFrom.Value.ToString();

            // Дополнительно
            OnlyWatchedCheckBox.IsChecked = Filters.OnlyWatched;
            OnlyWatchListCheckBox.IsChecked = Filters.OnlyWatchList;
            OnlyFavoritesCheckBox.IsChecked = Filters.OnlyFavorites;
            OnlyWithPosterCheckBox.IsChecked = Filters.OnlyWithPoster;
        }

        private void UpdateUserFiltersVisibility()
        {
            bool isLoggedIn = _userId > 0;

            OnlyWatchedCheckBox.IsEnabled = isLoggedIn;
            OnlyWatchListCheckBox.IsEnabled = isLoggedIn;
            OnlyFavoritesCheckBox.IsEnabled = isLoggedIn;

            if (!isLoggedIn)
            {
                OnlyWatchedCheckBox.ToolTip = "Требуется вход в систему";
                OnlyWatchListCheckBox.ToolTip = "Требуется вход в систему";
                OnlyFavoritesCheckBox.ToolTip = "Требуется вход в систему";
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Сортировка
                var selectedSortItem = SortByComboBox.SelectedItem as ComboBoxItem;
                Filters.SortBy = selectedSortItem?.Tag.ToString() ?? "popularity";
                Filters.SortDescending = SortDescendingCheckBox.IsChecked ?? true;

                // Жанры
                Filters.SelectedGenres.Clear();
                foreach (var item in GenresItemsControl.Items)
                {
                    var container = GenresItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (container != null)
                    {
                        var checkBox = FindVisualChild<CheckBox>(container);
                        if (checkBox != null && checkBox.IsChecked == true)
                        {
                            Filters.SelectedGenres.Add(checkBox.Content.ToString());
                        }
                    }
                }

                // Годы
                Filters.YearFrom = ParseInt(YearFromTextBox.Text);
                Filters.YearTo = ParseInt(YearToTextBox.Text);

                // Рейтинг
                Filters.RatingFrom = ParseFloat(RatingFromTextBox.Text);
                Filters.RatingTo = ParseFloat(RatingToTextBox.Text);
                Filters.VotesFrom = ParseInt(VotesFromTextBox.Text);

                // Дополнительно
                Filters.OnlyWatched = OnlyWatchedCheckBox.IsChecked ?? false;
                Filters.OnlyWatchList = OnlyWatchListCheckBox.IsChecked ?? false;
                Filters.OnlyFavorites = OnlyFavoritesCheckBox.IsChecked ?? false;
                Filters.OnlyWithPoster = OnlyWithPosterCheckBox.IsChecked ?? true;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка применения фильтров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Filters.Reset();
            LoadFilters();

            // Сбрасываем жанры
            foreach (var item in GenresItemsControl.Items)
            {
                var container = GenresItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container != null)
                {
                    var checkBox = FindVisualChild<CheckBox>(container);
                    if (checkBox != null)
                    {
                        checkBox.IsChecked = false;
                    }
                }
            }

            YearPresetsComboBox.SelectedIndex = 0;
        }

        private void YearPresetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = YearPresetsComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string tag = selectedItem.Tag.ToString();

            switch (tag)
            {
                case "all":
                    YearFromTextBox.Text = "";
                    YearToTextBox.Text = "";
                    break;
                case "2020s":
                    YearFromTextBox.Text = "2020";
                    YearToTextBox.Text = DateTime.Now.Year.ToString();
                    break;
                case "2010s":
                    YearFromTextBox.Text = "2010";
                    YearToTextBox.Text = "2019";
                    break;
                case "2000s":
                    YearFromTextBox.Text = "2000";
                    YearToTextBox.Text = "2009";
                    break;
                case "1990s":
                    YearFromTextBox.Text = "1990";
                    YearToTextBox.Text = "1999";
                    break;
                case "older":
                    YearFromTextBox.Text = "1900";
                    YearToTextBox.Text = "1989";
                    break;
            }
        }

        private int? ParseInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (int.TryParse(text, out int value))
                return value;

            return null;
        }

        private float? ParseFloat(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (float.TryParse(text, out float value))
                return value;

            return null;
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child != null && child is T)
                    return (T)child;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}