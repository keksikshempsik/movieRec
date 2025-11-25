using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MovieRecV5.Services
{
    public class MoviePosterService
    {
        private readonly HttpClient _httpClient;
        public MoviePosterService()
        {
            _httpClient = new HttpClient();
        }
        public async Task<string> DownloadPosterAsBase64(string posterUrl)
        {
            try
            {
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(posterUrl);
                string base64 = Convert.ToBase64String(imageBytes);
                string mimeType = DetectImageType(imageBytes);
                return $"data:{mimeType};base64,{base64}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        private string DetectImageType(byte[] imageBytes)
        {
            if (imageBytes.Length >= 3)
            {
                if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
                    return "image/jpeg";
                if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E)
                    return "image/png";
            }
            return "image/jpeg";
        }

        public BitmapImage Base64ToBitmapImage(string base64String)
        {
            try
            {
                if (base64String.Contains("base64,"))
                    base64String = base64String.Split(',')[1];

                byte[] imageBytes = Convert.FromBase64String(base64String);

                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(imageBytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }


    }
}
