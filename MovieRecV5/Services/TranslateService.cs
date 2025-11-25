using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;

namespace MovieRecV5.Services
{
    public class TranslateService
    {
        private readonly HttpClient _httpClient;

        public TranslateService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<string> TranslateTextAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return text;

                // Если текст меньше 500 символов - переводим как есть
                if (text.Length <= 500)
                {
                    return await TranslateChunk(text);
                }

                // Разбиваем текст на части по 500 символов
                var chunks = SplitTextIntoChunks(text, 500);
                var translatedChunks = new List<string>();

                // Переводим каждую часть отдельно
                foreach (var chunk in chunks)
                {
                    var translatedChunk = await TranslateChunk(chunk);
                    translatedChunks.Add(translatedChunk);
                    await Task.Delay(100); // Небольшая задержка между запросами
                }

                // Собираем переведенные части обратно
                return string.Join(" ", translatedChunks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation error: {ex.Message}");
                return text;
            }
        }

        private async Task<string> TranslateChunk(string text)
        {
            try
            {
                string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair=en|ru";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonConvert.DeserializeObject<TranslationResult>(response);
                return result?.ResponseData?.TranslatedText ?? text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chunk translation error: {ex.Message}");
                return text;
            }
        }

        private List<string> SplitTextIntoChunks(string text, int maxChunkSize)
        {
            var chunks = new List<string>();

            for (int i = 0; i < text.Length; i += maxChunkSize)
            {
                int chunkSize = Math.Min(maxChunkSize, text.Length - i);
                string chunk = text.Substring(i, chunkSize);

                // Пытаемся не разрывать слова - ищем последний пробел в чанке
                if (chunkSize == maxChunkSize && i + chunkSize < text.Length)
                {
                    int lastSpace = chunk.LastIndexOf(' ');
                    if (lastSpace > maxChunkSize * 0.7) // Если пробел в последних 30% чанка
                    {
                        chunk = chunk.Substring(0, lastSpace);
                        i -= (maxChunkSize - lastSpace); // Корректируем позицию
                    }
                }

                chunks.Add(chunk.Trim());
            }

            return chunks;
        }
    }

    public class TranslationResult
    {
        [JsonProperty("responseData")]
        public ResponseData ResponseData { get; set; }
    }

    public class ResponseData
    {
        [JsonProperty("translatedText")]
        public string TranslatedText { get; set; }
    }
}