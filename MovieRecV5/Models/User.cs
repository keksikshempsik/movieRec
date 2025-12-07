using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MovieRecV5.Models
{
    public class User
    {
        public int Id;
        public string Login;
        public string DisplayName;
        public string Password;
        public string Email;
        public string AvatarUrl;

        public class UserStats
        {
            public Dictionary<string, int> GenreDistribution { get; set; } = new();
            public Dictionary<int, int> YearDistribution { get; set; } = new();
            public Dictionary<int, int> RatingDistribution { get; set; } = new();
            public List<RatingDatePoint> RatingTimeline { get; set; } = new();
        }

        public class RatingDatePoint
        {
            public DateTime Date { get; set; }
            public int Rating { get; set; }
        }

        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                string saltedPassword = password + "MovieRecV5_Salt_2024!" + password.Length;
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
                bytes = sha256.ComputeHash(bytes);

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}