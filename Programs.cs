using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

class Program
{
    static string rssUrl = "https://rsshub.app/twitter/user/f41co_";
    static string webhookUrl = "https://canary.discord.com/api/webhooks/1487797235751059548/NAbEzqkDyoMeNbeMiAjsQXmd3-dWvuT3yqDZexDJt20G51PEjSffCkdmsuoaz8RuAXFC";
    
    static string lastId = "";

    static async Task Main()
    {
        Console.WriteLine("起動");

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
            using var client = new HttpClient();

            var xml = await client.GetStringAsync(rssUrl);
            var doc = XDocument.Parse(xml);

            var item = doc.Descendants("item").FirstOrDefault();
            if (item == null) return;

            var link = item.Element("link")?.Value;
            var id = link?.Split('/').Last();

            if (string.IsNullOrEmpty(id)) return;

            if (id != lastId)
            {
                Console.WriteLine("新着: " + link);

                await SendToDiscord(link);

                lastId = id;
            }
            else
            {
                Console.WriteLine("更新なし");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("エラー: " + ex.Message);
        }
    }

    static async Task SendToDiscord(string message)
    {
        using var client = new HttpClient();

        var json = $"{{\"content\":\"{message}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // await client.PostAsync(webhookUrl, content);
        var res = await client.PostAsync(webhookUrl, content);

        Console.WriteLine("Discord status: " + res.StatusCode);
    }
}
