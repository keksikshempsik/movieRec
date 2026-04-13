using System.Collections.Generic;

namespace MovieRecV5.Models
{
    public class MovieFilters
    {
        public string SearchQuery { get; set; } = "";
        public List<string> SelectedGenres { get; set; } = new List<string>();
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public float? RatingFrom { get; set; }
        public float? RatingTo { get; set; }
        public int? VotesFrom { get; set; }
        public string SortBy { get; set; } = "popularity"; // popularity, rating, year, title
        public bool SortDescending { get; set; } = true;
        public bool OnlyWatched { get; set; } = false;
        public bool OnlyWatchList { get; set; } = false;
        public bool OnlyFavorites { get; set; } = false;
        public bool OnlyWithPoster { get; set; } = true;

        public MovieFilters()
        {
            SelectedGenres = new List<string>();
        }

        public bool HasActiveFilters()
        {
            return SelectedGenres.Count > 0 ||
                   YearFrom.HasValue ||
                   YearTo.HasValue ||
                   RatingFrom.HasValue ||
                   RatingTo.HasValue ||
                   VotesFrom.HasValue ||
                   OnlyWatched ||
                   OnlyWatchList ||
                   OnlyFavorites;
        }

        public void Reset()
        {
            SelectedGenres.Clear();
            YearFrom = null;
            YearTo = null;
            RatingFrom = null;
            RatingTo = null;
            VotesFrom = null;
            SortBy = "popularity";
            SortDescending = true;
            OnlyWatched = false;
            OnlyWatchList = false;
            OnlyFavorites = false;
            OnlyWithPoster = true;
        }
    }
}