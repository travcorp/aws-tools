using System.Net.Http;

namespace TTC.Deployment.Tests
{
    internal class Http
    {
        internal static string Get(string url)
        {
            return new HttpClient().GetStringAsync(url).Result;
        }
    }
}
