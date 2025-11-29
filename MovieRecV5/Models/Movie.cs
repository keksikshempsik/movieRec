using System.Collections.Generic;

public class Movie
{
    public string Title;
    public string Slug;
    public int Year;
    public string Description;
    public string PosterUrl;
    public string LetterBoxdUrl;
    public string Poster;
    public int VoteCount;
    public float Rating;
    public List<string> Genres { get; set; } = new List<string>();
    public bool IsWatched { get; set; } // Добавляем свойство для отслеживания просмотра

    public string FormatVoteCount(int voteCount)
    {
        if (voteCount < 1000)
        {
            return voteCount.ToString();
        }
        else if (voteCount < 1000000)
        {
            double thousands = voteCount / 1000.0;
            return $"{thousands:0.#}K";
        }
        else
        {
            double millions = voteCount / 1000000.0;
            return $"{millions:0.#}M";
        }
    }
}