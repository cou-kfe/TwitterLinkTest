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
    "https://nitter.privacydev.net/kou_wuwa/rss",
    "https://nitter.1d4.us/kou_wuwa/rss",
    "https://nitter.kavin.rocks/kou_wuwa/rss"
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
                    // await SendToDiscord(ConvertToX(link));
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
            for (int retry = 0; retry < 2; retry++)
            {
                try
                {
                    Console.WriteLine($"試行: {url} (retry {retry})");
    
                    var handler = new HttpClientHandler()
                    {
                        AutomaticDecompression = DecompressionMethods.All
                    };
    
                    using var client = new HttpClient(handler);
    
                    client.DefaultRequestHeaders.Add(
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
                    );
    
                    client.Timeout = TimeSpan.FromSeconds(10);
    
                    var res = await client.GetAsync(url);
    
                    Console.WriteLine("Status: " + res.StatusCode);
    
                    if (res.IsSuccessStatusCode)
                    {
                        Console.WriteLine("RSS取得成功");
                        return await res.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("RSS失敗: " + ex.Message);
                }
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

    static string ConvertToX(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
    
        var converted = url
            .Replace("https://nitter.net/", "https://x.com/")
            .Replace("https://nitter.poast.org/", "https://x.com/")
            .Replace("https://nitter.privacydev.net/", "https://x.com/");
    
        // #m など削除
        var hashIndex = converted.IndexOf('#');
        if (hashIndex != -1)
        {
            converted = converted.Substring(0, hashIndex);
        }
    
        return converted;
    }
}
