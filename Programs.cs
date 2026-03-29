using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

class Program
{
    static string[] feeds = new[]
    {
        "https://nitter.net/kou_wuwa/rss",
        "https://nitter.poast.org/kou_wuwa/rss",
        "https://nitter.privacydev.net/kou_wuwa/rss"
    };

    static string webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK");

    static string lastId = "";
    static bool isFirstRun = true;

    static async Task Main()
    {
        Console.WriteLine("起動");

        // TLS対策（重要）
        ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        while (true)
        {
            await CheckFeed();
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }

    static async Task CheckFeed()
    {
        try
        {
            var xml = await GetRss();

            var doc = XDocument.Parse(xml);
            var item = doc.Descendants("item").FirstOrDefault();

            if (item == null)
            {
                Console.WriteLine("itemなし");
                return;
            }

            var link = item.Element("link")?.Value;
            Console.WriteLine("取得リンク: " + link);

            var id = link?.Split('/').Last();
            if (string.IsNullOrEmpty(id)) return;

            if (id != lastId)
            {
                Console.WriteLine("新着検知");

                // if (!isFirstRun) // 初回スパム防止
                // {
                    await SendToDiscord(link);
                // }

                lastId = id;
                isFirstRun = false;
            }
            else
            {
                Console.WriteLine("更新なし");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("CheckFeedエラー: " + ex.ToString());
        }
    }

    static async Task<string> GetRss()
    {
        foreach (var url in feeds)
        {
            try
            {
                Console.WriteLine("試行: " + url);

                var handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.All
                };

                using var client = new HttpClient(handler);

                // ★ 超重要：User-Agent
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                client.Timeout = TimeSpan.FromSeconds(10);

                var res = await client.GetAsync(url);

                Console.WriteLine("Status: " + res.StatusCode);

                if (res.IsSuccessStatusCode)
                {
                    var content = await res.Content.ReadAsStringAsync();
                    Console.WriteLine("RSS取得成功");
                    return content;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("RSS失敗: " + ex.Message);
            }
        }

        throw new Exception("RSS全滅");
    }

    static async Task SendToDiscord(string message)
    {
        try
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            var json = $"{{\"content\":\"{message}\"}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var res = await client.PostAsync(webhookUrl, content);

            Console.WriteLine("Discord status: " + res.StatusCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Discord送信エラー: " + ex.ToString());
        }
    }
}
