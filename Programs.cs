using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;

class Program
{
    static string user = Environment.GetEnvironmentVariable("TWITTER_USER");
    static string webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK");

    static HashSet<string> sentIds = new();

    static async Task Main()
    {
        Console.WriteLine($"起動: {user}");

        ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        while (true)
        {
            await CheckFeed();
            await Task.Delay(TimeSpan.FromMinutes(3));
        }
    }

    static async Task CheckFeed()
    {
        try
        {
            var xml = await GetRss();

            var doc = XDocument.Parse(xml);
            var items = doc.Descendants("item").Take(5).Reverse();

            foreach (var item in items)
            {
                var link = item.Element("link")?.Value;
                var id = link?.Split('/').Last();

                if (string.IsNullOrEmpty(id)) continue;

                if (!sentIds.Contains(id))
                {
                    Console.WriteLine($"新着: {link}");

                    await SendToDiscord(ConvertToX(link));

                    sentIds.Add(id);
                }
            }

            // メモリ肥大防止
            if (sentIds.Count > 100)
            {
                sentIds.Clear();
                Console.WriteLine("sentIdsリセット");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("エラー: " + ex.Message);
        }
    }

    static async Task<string> GetRss()
    {
        var feeds = new[]
        {
            $"https://nitter.net/{user}/rss",
            $"https://nitter.poast.org/{user}/rss",
            $"https://nitter.privacydev.net/{user}/rss"
        };

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

                client.DefaultRequestHeaders.Add(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
                );

                client.Timeout = TimeSpan.FromSeconds(10);

                var res = await client.GetAsync(url);

                if (res.IsSuccessStatusCode)
                {
                    Console.WriteLine("RSS取得成功");
                    return await res.Content.ReadAsStringAsync();
                }
            }
            catch { }
        }

        throw new Exception("RSS取得失敗");
    }

    static async Task SendToDiscord(string url)
    {
        using var client = new HttpClient();

        var json = $"{{\"content\":\"{url}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await client.PostAsync(webhookUrl, content);
    }

    static string ConvertToX(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;

        var converted = url
            .Replace("https://nitter.net/", "https://x.com/")
            .Replace("https://nitter.poast.org/", "https://x.com/")
            .Replace("https://nitter.privacydev.net/", "https://x.com/");

        var hashIndex = converted.IndexOf('#');
        if (hashIndex != -1)
        {
            converted = converted.Substring(0, hashIndex);
        }

        return converted;
    }
}
