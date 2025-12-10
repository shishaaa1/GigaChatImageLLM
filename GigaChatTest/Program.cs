using GigaChatTest.Response;
using Newtonsoft.Json;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using GigaChatTest.Models;

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
            string Token = await GetToken(ClientId, AuthorizationKey);
            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
                return; 
            }

            List<Request.Message> ConversationHistory = new List<Request.Message>();

            while (true)
            {
                Console.Write("Сообщение: ");
                string UserInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(UserInput)) continue;
                var userMessage = new Request.Message()
                {
                    role = "user",
                    content = UserInput
                };
                ConversationHistory.Add(userMessage);

                ResponseMessage Answer = await GetAnswer(Token, ConversationHistory);

                if (Answer != null && Answer.choices != null && Answer.choices.Count > 0)
                {
                    string botResponse = Answer.choices[0].message.content;
                    Console.WriteLine("Ответ: " + botResponse);

                   
                    ConversationHistory.Add(Answer.choices[0].message);
                }
                else
                {
                    Console.WriteLine("Ошибка получения ответа или пустой ответ.");
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
    }
}