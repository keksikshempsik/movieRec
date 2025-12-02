using System.Collections.Generic;

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Slug { get; set; }
    public int Year { get; set; }
    public string Description { get; set; }
    public string PosterUrl { get; set; }
    public string LetterBoxdUrl { get; set; }
    public string Poster { get; set; }
    public List<string> Genres { get; set; }
    public int VoteCount { get; set; }
    public float Rating { get; set; }
    public bool IsWatched { get; set; }
    public int? UserRating { get; set; }
    public bool InWatchList { get; set; }

    public Movie()
    {
        Genres = new List<string>();
        IsWatched = false;
        InWatchList = false;
    }

    public string FormatVoteCount(int count)
    {
        if (count >= 1000000)
            return (count / 1000000.0).ToString("F1") + "M";
        else if (count >= 1000)
            return (count / 1000.0).ToString("F1") + "K";
        else
            return count.ToString();
    }
}