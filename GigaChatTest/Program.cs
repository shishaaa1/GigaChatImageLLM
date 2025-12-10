using GigaChatTest.Classes;
using GigaChatTest.Models;
using GigaChatTest.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GigaChatTest
{
    internal class Program
    {
        /// <summary>
        /// Клиент ID
        /// </summary>
        static string ClientId = "019b038f-be32-7728-9ba8-86b03afb5efc";
        /// <summary>
        /// Код авторизации
        /// </summary>
        static string AuthorizationKey = "MDE5YjAzOGYtYmUzMi03NzI4LTliYTgtODZiMDNhZmI1ZWZjOjdlZTk5Y2MzLTFlMGEtNGFjMC1hMjI0LWMxY2ZiZjY1ZmNiMA==";

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Для создания изображений, перед запросом введите /img Ваш запрос \n" +
                "Для текстового запроса, введите просто ваш запрос,без /img");
            string Token = await GetToken(ClientId, AuthorizationKey);
            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
                return;
            }
            Console.ForegroundColor = ConsoleColor.White;
            List<Request.Message> ConversationHistory = new List<Request.Message>();

            while (true)
            {
                Console.Write("Сообщение: ");
                string UserInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(UserInput))
                    continue;
                if (UserInput.StartsWith("/img", StringComparison.OrdinalIgnoreCase))
                {
                    string prompt = UserInput.Substring(4).Trim();

                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        Console.WriteLine("Введите описание после /img");
                        continue;
                    }

                    var imgMessages = new List<Request.Message>()
                        {
                            new Request.Message() { role = "user", content = prompt }
                        };

                    string baseUrl = "https://gigachat.devices.sberbank.ru/api/v1";
                    string postUrl = $"{baseUrl}/chat/completions";

                    using (var handler = new HttpClientHandler())
                    {
                        handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, ssl) => true;
                        using (var http = new HttpClient(handler))
                        {
                            http.DefaultRequestHeaders.Clear();
                            http.DefaultRequestHeaders.Add("Accept", "application/json");
                            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {Token}");
                            http.DefaultRequestHeaders.Add("X-Client-ID", ClientId);

                            var payload = new
                            {
                                model = "GigaChat",
                                messages = imgMessages,
                                function_call = "auto"
                            };

                            string jsonPayload = JsonConvert.SerializeObject(payload);
                            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                            var resp = await http.PostAsync(postUrl, content);
                            if (!resp.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"Ошибка при генерации изображения: {resp.StatusCode}");
                                continue;
                            }

                            string respJson = await resp.Content.ReadAsStringAsync();
                            string htmlContent = null;

                            try
                            {
                                var j = JObject.Parse(respJson);
                                htmlContent = j["choices"]?[0]?["message"]?["content"]?.ToString();
                            }
                            catch
                            {
                                Console.WriteLine("Не удалось распарсить ответ модели.");
                                continue;
                            }

                            if (string.IsNullOrEmpty(htmlContent))
                            {
                                Console.WriteLine("Ответ модели пустой или тег <img> не найден.");
                                continue;
                            }
                            var imgMatches = Regex.Matches(htmlContent, "<img\\s+[^>]*src\\s*=\\s*\"([^\"]+)\"[^>]*>");
                            if (imgMatches.Count == 0)
                            {
                                Console.WriteLine("Теги <img> не найдены в ответе.");
                                continue;
                            }

                            Console.WriteLine("Сгенерированные изображения:");
                            foreach (Match m in imgMatches)
                            {
                                Console.WriteLine(m.Value); 
                            }
                            string firstFileId = imgMatches[0].Groups[1].Value;
                            var fileUrl = $"{baseUrl}/files/{firstFileId}/content";

                            var fileResp = await http.GetAsync(fileUrl);
                            if (!fileResp.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"Ошибка при скачивании изображения: {fileResp.StatusCode}");
                                continue;
                            }

                            byte[] bytes = await fileResp.Content.ReadAsByteArrayAsync();
                            string outPath = Path.Combine(Environment.CurrentDirectory, $"gigachat_{firstFileId}.jpg");
                            await File.WriteAllBytesAsync(outPath, bytes);
                            Console.WriteLine($"Первое изображение сохранено: {outPath}");

                            Console.Write("Установить это изображение как обои? (д/н): ");
                            string ans = Console.ReadLine()?.Trim().ToLower();
                            if (ans == "д" || ans == "да" || ans == "y")
                            {
                                WallpaperSetter.SetWallpaper(outPath);
                            }
                        }
                    }

                    continue;
                }
            }

            }


        /// <summary>
        /// Метод получения токена пользователя
        /// </summary>
        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string ReturnToken = null;
            string Url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyError) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("RqUID", rqUID);
                    Request.Headers.Add("Authorization", $"Bearer {bearer}");
                    var Data = new List<KeyValuePair<string, string>>
                    {
                       new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                    };
                    Request.Content = new FormUrlEncodedContent(Data);
                    HttpResponseMessage Response = await client.SendAsync(Request);
                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        ResponseToken Token = JsonConvert.DeserializeObject<ResponseToken>(ResponseContent);
                        ReturnToken = Token.access_token;
                    }
                }
            }
            return ReturnToken;
        }

        ///<summary>
        ///Метод получения ответа
        ///</summary>
        ///<param name="token">Токен пользователя</param>
        ///<param name="messages">История сообщений (диалог)</param>
        ///<returns></returns>
        public static async Task<ResponseMessage> GetAnswer(string token, List<Request.Message> messages)
        {
            ResponseMessage responseMessage = null;
            string Url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("Authorization", $"Bearer {token}");

                    Models.Request DataRequest = new Models.Request()
                    {
                        model = "GigaChat",
                        stream = false,
                        repetition_penalty = 1,
                        messages = messages
                    };

                    string JsonContent = JsonConvert.SerializeObject(DataRequest);
                    Request.Content = new StringContent(JsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage Response = await client.SendAsync(Request);

                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(ResponseContent);
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка API: {Response.StatusCode}");
                    }
                }
            }
            return responseMessage;
        }
        ///<summary>
        ///Метод для генерации изображения
        /// </summary>
        /// <param name="token">Токен полльзователя</param>
        ///<param name="messages">Сообщение пользователя</param>
        ///<returns></returns>
        public static async Task<string> GetPictureAndSave(string token, List<Models.Request.Message> messages, string clientId = null)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            string baseUrl = "https://gigachat.devices.sberbank.ru/api/v1";

            using (var handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, ssl) => true;

                using (var http = new HttpClient(handler))
                {
                    http.DefaultRequestHeaders.Clear();
                    http.DefaultRequestHeaders.Add("Accept", "application/json");
                    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        http.DefaultRequestHeaders.Add("X-Client-ID", clientId);
                    }
                    var payload = new
                    {
                        model = "GigaChat",
                        messages = messages,
                        function_call = "auto"
                    };

                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    using (var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                    {
                        var postUrl = $"{baseUrl}/chat/completions";
                        var resp = await http.PostAsync(postUrl, content);

                        if (!resp.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Ошибка при создании изображения: {resp.StatusCode}");
                            var errBody = await resp.Content.ReadAsStringAsync();
                            Console.WriteLine(errBody);
                            return null;
                        }

                        var respJson = await resp.Content.ReadAsStringAsync();
                        string htmlContent = null;
                        try
                        {
                            var j = JObject.Parse(respJson);
                            htmlContent = j["choices"]?[0]?["message"]?["content"]?.ToString();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Не удалось распарсить ответ completions: " + ex.Message);
                            return null;
                        }

                        if (string.IsNullOrEmpty(htmlContent))
                        {
                            Console.WriteLine("В ответе нет содержимого с тегом <img>.");
                            return null;
                        }
                        var m = Regex.Match(htmlContent, "<img\\s+[^>]*src\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                        if (!m.Success)
                        {
                            Console.WriteLine("Тег <img> не найден в ответе или у него нет src.");
                            Console.WriteLine("Ответ модели: " + htmlContent);
                            return null;
                        }

                        string fileId = m.Groups[1].Value;
                        if (string.IsNullOrEmpty(fileId))
                        {
                            Console.WriteLine("Не удалось извлечь file_id из тега <img>.");
                            return null;
                        }
                        var fileUrl = $"{baseUrl}/files/{fileId}/content";
                        using (var request = new HttpRequestMessage(HttpMethod.Get, fileUrl))
                        {
                            request.Headers.Add("Accept", "application/jpg");
                            if (!string.IsNullOrEmpty(clientId))
                            {
                                request.Headers.Add("X-Client-ID", clientId);
                            }

                            var fileResp = await http.SendAsync(request);
                            if (!fileResp.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"Ошибка при скачивании изображения: {fileResp.StatusCode}");
                                var body = await fileResp.Content.ReadAsStringAsync();
                                Console.WriteLine(body);
                                return null;
                            }

                            var bytes = await fileResp.Content.ReadAsByteArrayAsync();
                            string outPath = Path.Combine(Environment.CurrentDirectory, $"gigachat_{fileId}.jpg");
                            await File.WriteAllBytesAsync(outPath, bytes);
                            Console.WriteLine($"Изображение сохранено: {outPath}");
                            return outPath;
                        }
                    }
                }
            }
        }

    }

}