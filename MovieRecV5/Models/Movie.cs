using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using HtmlAgilityPack;

namespace MovieRecV5.Models
{
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

        public string FormatVoteCount(int voteCount)
        {
            if (voteCount < 1000)
            {
                return voteCount.ToString(); // Меньше 1000 - просто число
            }
            else if (voteCount < 1000000)
            {
                // Тысячи: 34597 -> 34.6К
                double thousands = voteCount / 1000.0;
                return $"{thousands:0.#}K";
            }
            else
            {
                // Миллионы: 78234784 -> 78.2М
                double millions = voteCount / 1000000.0;
                return $"{millions:0.#}M";
            }
        }

    }
}
