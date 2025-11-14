using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using HtmlAgilityPack;

namespace MovieRecV5
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
        public List<string> Genres { get; set; } = new List<string>();

    }
}
