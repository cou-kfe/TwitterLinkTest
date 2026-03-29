using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;

class Program
{
    static string rssUrl = "https://rsshub.app/twitter/user/WW_JP_Official";
    static string webhookUrl = "https://canary.discord.com/api/webhooks/1487747185339535550/6PHhaqc0uu7PDxzW-TaAQGTQ6gvul1urdqv_KlHQmfIn29E9FlysTSpWitpjMYtPjCiH";
    static string lastIdFile = "last_id.txt";

    static async Task Main()
    {
        
        while (true)
        {
            await CheckFeed();
            await Task.Delay(TimeSpan.FromMinutes(5));
            try
            {
                var client = new HttpClient();
                var xml = await client.GetStringAsync(rssUrl);
    
                var doc = XDocument.Parse(xml);
                var items = doc.Descendants("item");
    
                var latestItem = items.FirstOrDefault();
                if (latestItem == null) return;
    
                var link = latestItem.Element("link")?.Value;
                var id = link?.Split('/').Last();
    
                if (string.IsNullOrEmpty(id)) return;
    
                string lastId = File.Exists(lastIdFile) ? File.ReadAllText(lastIdFile) : "";
    
                if (id != lastId)
                {
                    Console.WriteLine("新着検知: " + link);
    
                    await SendToDiscord(link);
    
                    File.WriteAllText(lastIdFile, id);
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
    }

    static async Task SendToDiscord(string message)
    {
        using var client = new HttpClient();

        var json = $"{{\"content\":\"{message}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await client.PostAsync(webhookUrl, content);
    }
}
