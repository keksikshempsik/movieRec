using System;
using System.Security.Cryptography;
using System.Text;

namespace MovieRecV5.Models
{
    public class User
    {
        public int Id;
        public string Login;
        public string DisplayName; // Новое поле: отображаемое имя
        public string Password;
        public string Email;
        public string AvatarUrl; // Новое поле: ссылка на аватар

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