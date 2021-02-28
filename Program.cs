using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;

namespace dyn_dns
{
    class Program
    {
        static void Main(string[] args)
        {
            Params p;
            p = ParseArgs(args);

            if (p.Action == "list")
            {
                ListDNSZones(p);
            }
            if (p.Action == "listRecords")
            {
                ListRecords(p);
            }
            if (p.Action == "ip")
            {
                GetIP();
            }
            if (p.Action == "update")
            {
                UpdateRecord(p);
            }
        }

        private static void ListDNSZones(Params p)
        {
            var core = new Core();

            var dnsList = core.GetDNSZones(p.AuthToken).GetAwaiter().GetResult();
            foreach (var dns in dnsList)
            {
                Console.WriteLine($"{dns.name}:{dns.id}");
            }
        }

        private static void ListRecords(Params p)
        {
            var core = new Core();

            var records = core.Records(p.AuthToken, p.ZoneId).GetAwaiter().GetResult();
            foreach (var record in records)
            {
                Console.WriteLine($"{record.id}:{record.type}:{record.hostname}:{record.value}");
            }
        }

        private static string GetIP()
        {
            var core = new Core();

            var ipReq = core.GetIP().GetAwaiter().GetResult();
            var ipContent = ipReq.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var ip = JsonConvert.DeserializeObject<IPResult>(ipContent);
            Console.WriteLine(ip.Value);

            return ip.Value;
        }

        private static void UpdateRecord(Params p)
        {
            if (string.IsNullOrWhiteSpace(p.HostName))
            {
                Console.WriteLine("cannot update without hostname");
                return;
            }

            var ip = GetIP();
            var type = "A";

            var core = new Core();

            var records = core.Records(p.AuthToken, p.ZoneId).GetAwaiter().GetResult();

            foreach (var record in records)
            {
                if (record.hostname.ToLower() == p.HostName.ToLower()
                    && record.type == type)
                {
                    var result = core.DeleteRecord(p.AuthToken, p.ZoneId, record.id).GetAwaiter().GetResult();
                    if (!result.IsSuccessStatusCode)
                    {
                        Console.WriteLine(
                            $"unable to delete old record: {record.id}, {record.hostname}, {record.value}");
                    }
                }
            }

            var create = core.CreateRecord(
                    p.AuthToken,
                    p.ZoneId,
                    type,
                    p.HostName,
                    ip
                ).GetAwaiter().GetResult();
            var createResult = create.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Console.WriteLine(createResult);
        }

        private static Params ParseArgs(string[] args)
        {
            var ret = new Params();

            if (args.Length < 1)
            {
                return ret;
            }

            ret.Action = args[0].Trim().ToLower();

            if (args.Length < 2)
            {
                return ret;
            }

            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];

                var split = arg.Split('=');
                var key = split[0].Trim().ToLower();
                var value = split[1].Trim();

                if (key == "token")
                {
                    ret.AuthToken = value;
                }
                if (key == "zone")
                {
                    ret.ZoneId = value;
                }
                if (key == "hostname")
                {
                    ret.HostName = value;
                }
            }

            return ret;
        }

        private class Params
        {
            public string Action;
            public string AuthToken;
            public string ZoneId;
            public string HostName;
        }

        private class IPResult
        {
            public string ip;

            public string Value { get { return ip; } }
        }

        private class DNSZone
        {
            public string id;
            public string name;
        }

        private class DNSRecord
        {
            public string id;
            public string hostname;
            public string type;
            public string value;
        }

        private class Core
        {
            public async Task<DNSRecord[]> Records(
                string authToken,
                string zoneId)
            {
                HttpClient client = new HttpClient();

                var url = $"https://api.netlify.com/api/v1/dns_zones/{zoneId}/dns_records";

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var res = await client.SendAsync(message);
                var records = 
                    JsonConvert.DeserializeObject<DNSRecord[]>(
                        await res.Content.ReadAsStringAsync());

                return records;
            }

            public async Task<HttpResponseMessage> CreateRecord(
                string authToken,
                string zoneId,
                string type,
                string hostName,
                string value)
            {
                HttpClient client = new HttpClient();

                var url = $"https://api.netlify.com/api/v1/dns_zones/{zoneId}/dns_records";
                var content = $"{{ \"type\": \"{type}\", \"hostname\": \"{hostName}\", \"value\": \"{value}\" }}";

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                message.Content = new StringContent(content, Encoding.UTF8, "application/json");

                return await client.SendAsync(message);
            }

            public async Task<HttpResponseMessage> DeleteRecord(
                string authToken,
                string zoneId,
                string recordId)
            {
                HttpClient client = new HttpClient();

                var url = $"https://api.netlify.com/api/v1/dns_zones/{zoneId}/dns_records/{recordId}";

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Delete, new Uri(url));
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                return await client.SendAsync(message);
            }

            public async Task<DNSZone[]> GetDNSZones(string authToken)
            {
                HttpClient client = new HttpClient();

                var url = $"https://api.netlify.com/api/v1/dns_zones/";

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var result = await client.SendAsync(message);
                var content = await result.Content.ReadAsStringAsync();

                var zones = JsonConvert.DeserializeObject<DNSZone[]>(content);

                return zones;
            }

            public async Task<HttpResponseMessage> GetIP()
            {
                HttpClient client = new HttpClient();

                var url = $"https://api.ipify.org?format=json";

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, new Uri(url));

                return await client.SendAsync(message);
            }
        }
    }
}
