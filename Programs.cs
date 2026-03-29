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
    static string webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK");

    // 環境変数から取得
    static List<string> users = Environment
        .GetEnvironmentVariable("TWITTER_USERS")?
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .ToList() ?? new List<string>();

    // 各ユーザーごとに最新IDを保持
    static Dictionary<string, string> lastIds = new();

    static async Task Main()
    {
        Console.WriteLine("起動");

        ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        while (true)
        {
            foreach (var user in users)
            {
                await CheckFeed(user);
            }

            await Task.Delay(TimeSpan.FromMinutes(3));
        }
    }

    static async Task CheckFeed(string user)
    {
        try
        {
            var xml = await GetRss(user);

            var doc = XDocument.Parse(xml);
            var items = doc.Descendants("item").Take(5);

            foreach (var item in items)
            {
                var link = item.Element("link")?.Value;
                var id = link?.Split('/').Last();

                if (string.IsNullOrEmpty(id)) continue;

                if (!lastIds.ContainsKey(user))
                {
                    lastIds[user] = id;
                    continue; // 初回はスキップ
                }

                if (id != lastIds[user])
                {
                    Console.WriteLine($"[{user}] 新着: {link}");

                    await SendToDiscord(user, ConvertToX(link));
                }
            }

            // 最新ID更新
            var latest = items.FirstOrDefault()?.Element("link")?.Value?.Split('/').Last();
            if (!string.IsNullOrEmpty(latest))
            {
                lastIds[user] = latest;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{user}] エラー: " + ex.Message);
        }
    }

    static async Task<string> GetRss(string user)
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
                    return await res.Content.ReadAsStringAsync();
                }
            }
            catch { }
        }

        throw new Exception("RSS取得失敗");
    }

    static async Task SendToDiscord(string user, string url)
    {
        using var client = new HttpClient();

        var json = $@"
{{
  ""embeds"": [
    {{
      ""title"": ""{user} の新着ポスト"",
      ""url"": ""{url}""
    }}
  ]
}}";

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var res = await client.PostAsync(webhookUrl, content);

        Console.WriteLine("Discord: " + res.StatusCode);
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
