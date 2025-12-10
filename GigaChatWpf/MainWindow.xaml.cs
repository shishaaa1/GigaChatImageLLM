using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GigaChatWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ClientId = "019b038f-be32-7728-9ba8-86b03afb5efc";
        private const string AuthorizationKey = "MDE5YjAzOGYtYmUzMi03NzI4LTliYTgtODZiMDNhZmI1ZWZjOjdlZTk5Y2MzLTFlMGEtNGFjMC1hMjI0LWMxY2ZiZjY1ZmNiMA==";

        private string _currentToken;
        private string _lastGeneratedPath;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            StatusTextBlock.Text = "Получение токена...";
            _currentToken = await GetTokenAsync();
            if (_currentToken == null)
            {
                StatusTextBlock.Text = "Ошибка получения токена!";
            }
            else
            {
                StatusTextBlock.Text = "Готово к генерации";
                await AutoGenerateHolidayWallpaperOnStartup();
            }
        }

        private async Task AutoGenerateHolidayWallpaperOnStartup()
        {
            string holidayPrompt = GetHolidayPrompt();
            DateTime today = DateTime.Today;
            string holidayName = today.Month == 12 || today.Month <= 2
                ? (today.Month == 12 && today.Day >= 20 ? "Предновогодние дни" : "Зимние дни")
                : GetHolidayName(today);

            HolidayInfoTextBlock.Text = $"Сегодня {today:dd MMMM} — {holidayName.ToLower()}";
            StatusTextBlock.Text = "Генерация тематических обоев...";
            HolidayInfoTextBlock.Foreground = new SolidColorBrush(Colors.Gray);

            string finalPrompt = BuildFinalPrompt(holidayPrompt, isHoliday: true);

            _lastGeneratedPath = await GenerateAndSaveImage(finalPrompt);

            if (_lastGeneratedPath != null)
            {
                PreviewImage.Source = new BitmapImage(new Uri(_lastGeneratedPath));
                SetWallpaperButton.IsEnabled = true;
                StatusTextBlock.Text = $"Тематические обои сгенерированы: {holidayName}";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);

                // Предлагаем установить сразу
                var result = MessageBox.Show(
                    $"Сгенерированы обои под {holidayName.ToLower()}.\nУстановить их как обои рабочего стола прямо сейчас?",
                    "Тематические обои готовы",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    WallpaperSetter.SetWallpaper(_lastGeneratedPath);
                    StatusTextBlock.Text = "Обои установлены автоматически!";
                }
            }
            else
            {
                StatusTextBlock.Text = "Не удалось сгенерировать обои при запуске";
                HolidayInfoTextBlock.Text += " (генерация при запуске не удалась)";
            }
        }

        private string GetHolidayName(DateTime date)
        {
            var holidays = new Dictionary<DateTime, string>
    {
        { new DateTime(date.Year, 1, 1), "Новый год" },
        { new DateTime(date.Year, 1, 7), "Рождество" },
        { new DateTime(date.Year, 2, 23), "День защитника Отечества" },
        { new DateTime(date.Year, 3, 8), "Международный женский день" },
        { new DateTime(date.Year, 5, 1), "Праздник весны и труда" },
        { new DateTime(date.Year, 5, 9), "День Победы" },
        { new DateTime(date.Year, 12, 31), "Канун Нового года" },
    };

            if (holidays.TryGetValue(date, out string name))
                return name;

            return date.Month switch
            {
                12 or 1 or 2 => "Зимний сезон",
                3 or 4 or 5 => "Весенний сезон",
                6 or 7 or 8 => "Летний сезон",
                _ => "Осенний сезон"
            };
        }
        private string GetHolidayPrompt()
        {
            var today = DateTime.Today;

            var holidays = new Dictionary<DateTime, string>
    {
        { new DateTime(today.Year, 1, 1),  "Новогодняя ёлка, снег, подарки, праздничные огни" },
        { new DateTime(today.Year, 1, 7),  "Рождество, церковь, свечи, снег" },
        { new DateTime(today.Year, 2, 23), "23 февраля, военная тематика, флаг России" },
        { new DateTime(today.Year, 3, 8),  "8 марта, цветы, тюльпаны, весна" },
        { new DateTime(today.Year, 5, 1),  "1 мая, весна, труд, природа" },
        { new DateTime(today.Year, 5, 9),  "9 мая, День Победы, георгиевская лента" },
        { new DateTime(today.Year, 12, 31),"Новый год, куранты, шампанское, салют" },
    };

            if (holidays.TryGetValue(today, out string prompt))
                return prompt;

            int month = today.Month;
            if (month == 12 || month == 1 || month == 2)
                return "Зимний пейзаж, снег, новогодние огни, уют";

            if (month >= 3 && month <= 5)
                return "Весенний пейзаж, цветущие деревья, солнце";

            if (month >= 9 && month <= 11)
                return "Осенний лес, золотые листья, уют";

            return "Летний пейзаж, море, солнце, природа";
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            await GenerateImageAsync(isHoliday: false);
        }

        private async void HolidayButton_Click(object sender, RoutedEventArgs e)
        {
            await GenerateImageAsync(isHoliday: true);
        }

        private async Task GenerateImageAsync(bool isHoliday)
        {
            if (_currentToken == null)
            {
                MessageBox.Show("Нет токена авторизации.");
                return;
            }

            GenerateButton.IsEnabled = false;
            HolidayButton.IsEnabled = false;
            SetWallpaperButton.IsEnabled = false;
            StatusTextBlock.Text = "Генерация изображения...";
            StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);

            try
            {
                string basePrompt = isHoliday ? GetHolidayPrompt() : (PromptTextBox.Text.Trim() ?? "");
                if (string.IsNullOrWhiteSpace(basePrompt) && !isHoliday)
                    basePrompt = "красивые обои для рабочего стола";

                string finalPrompt = BuildFinalPrompt(basePrompt, isHoliday);

                _lastGeneratedPath = await GenerateAndSaveImage(finalPrompt);

                if (_lastGeneratedPath != null)
                {
                    PreviewImage.Source = new BitmapImage(new Uri(_lastGeneratedPath));
                    SetWallpaperButton.IsEnabled = true;
                    StatusTextBlock.Text = isHoliday ? "Праздничная картинка готовы! 🎄" : "Изображение сгенерировано!";
                    StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                else
                {
                    StatusTextBlock.Text = "Не удалось сгенерировать изображение";
                    StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
                StatusTextBlock.Text = "Произошла ошибка";
            }
            finally
            {
                GenerateButton.IsEnabled = true;
                HolidayButton.IsEnabled = true;
            }
        }
        private string BuildFinalPrompt(string basePrompt, bool isHoliday)
        {
            string style = GetComboBoxText(StyleComboBox) ?? "реалистичный";
            string palette = GetComboBoxText(PaletteComboBox) ?? "нейтральная";
            string aspectDesc = GetAspectDescription(GetComboBoxText(AspectRatioComboBox));
            string technical = $"высокое качество, детализированное изображение, {aspectDesc}, как картинки для рабочего стола";
            if (isHoliday)
            {
                return $"{basePrompt}, в {style.ToLower()} стиле, {GetPaletteDescription(palette)}, {technical}";
            }
            else
            {
                return $"{basePrompt}, {style.ToLower()}, {GetPaletteDescription(palette)}, {technical}";
            }
        }

        private string GetComboBoxText(ComboBox combo)
        {
            return (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        }

        private string GetAspectDescription(string selected)
        {
            return selected switch
            {
                "16:9 (монитор)" => "соотношение сторон 16:9, широкоформатное",
                "21:9 (ультраширокий)" => "ультраширокое соотношение сторон 21:9",
                "9:16 (вертикаль)" => "вертикальное соотношение сторон 9:16",
                "4:3" => "соотношение сторон 4:3",
                "1:1" => "квадратное изображение 1:1",
                _ => "соотношение сторон 16:9, широкоформатное"
            };
        }

        private string GetPaletteDescription(string palette)
        {
            return palette.ToLower() switch
            {
                "тёплые тона" => "тёплые тона",
                "холодные тона" => "холодные тона",
                "пастельные" => "пастельные тона",
                "яркие" => "яркие насыщенные цвета",
                "тёмная" => "тёмная цветовая гамма",
                "светлая" => "светлая цветовая гамма",
                "новогодняя" => "новогодние цвета: красный, зелёный, золотой, белый",
                _ => "нейтральная цветовая палитра"
            };
        }




        private void SetWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastGeneratedPath) && File.Exists(_lastGeneratedPath))
            {
                WallpaperSetter.SetWallpaper(_lastGeneratedPath);
                StatusTextBlock.Text = "Обои успешно установлены!";
            }
            else
            {
                MessageBox.Show("Файл изображения не найден.");
            }
        }

        private async Task<string> GetTokenAsync()
        {
            // (тот же код, что у вас в GetToken)
            string url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
            using var client = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("RqUID", ClientId);
            request.Headers.Add("Authorization", $"Bearer {AuthorizationKey}");

            var data = new List<KeyValuePair<string, string>> { new("scope", "GIGACHAT_API_PERS") };
            request.Content = new FormUrlEncodedContent(data);

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tokenObj = JsonConvert.DeserializeObject<dynamic>(json);
                return tokenObj.access_token;
            }
            return null;
        }

        private async Task<string> GenerateAndSaveImage(string prompt)
        {
            string baseUrl = "https://gigachat.devices.sberbank.ru/api/v1";
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) }; // Увеличили таймаут

            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("Accept", "application/json");
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_currentToken}");
            http.DefaultRequestHeaders.Add("X-Client-ID", ClientId);

            var payload = new
            {
                model = "GigaChat",
                messages = new[] { new { role = "user", content = prompt } },
                function_call = "auto"
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            try
            {
                resp = await http.PostAsync($"{baseUrl}/chat/completions", content);
            }
            catch (TaskCanceledException)
            {
                MessageBox.Show("Превышен таймаут запроса (2 минуты). Попробуйте позже или упростите промпт.");
                return null;
            }

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                MessageBox.Show("Ошибка API: " + resp.StatusCode + "\n" + err);
                return null;
            }

            var respJson = await resp.Content.ReadAsStringAsync();
            var jObject = JObject.Parse(respJson);
            string messageContent = jObject["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrEmpty(messageContent))
            {
                MessageBox.Show("Пустой ответ от модели.");
                return null;
            }
            var match = Regex.Match(messageContent, "<img\\s+[^>]*src\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string fileId = match.Groups[1].Value;
                var fileResp = await http.GetAsync($"{baseUrl}/files/{fileId}/content");
                if (!fileResp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Ошибка скачивания изображения.");
                    return null;
                }

                var bytes = await fileResp.Content.ReadAsByteArrayAsync();
                string path = Path.Combine(Environment.CurrentDirectory, $"wallpaper_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                await File.WriteAllBytesAsync(path, bytes);
                return path;
            }
            else
            {
                MessageBox.Show($"Модель не сгенерировала изображение, но ответила:\n\n{messageContent}\n\n" +
                                "Это частая ситуация в декабре из-за высокой нагрузки. Попробуйте ещё раз через минуту или упростите промпт.",
                                "Изображение не получено", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
        }

        public static class WallpaperSetter
        {
            private const int SPI_SETDESKWALLPAPER = 0x0014;
            private const int SPIF_UPDATEINFILE = 0x01;
            private const int SPIF_SENDWININCHANGE = 0x02;

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

            public static void SetWallpaper(string path)
            {
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINFILE | SPIF_SENDWININCHANGE);
            }
        }
    }
}