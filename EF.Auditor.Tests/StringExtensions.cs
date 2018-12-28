using Newtonsoft.Json;

namespace EF.Auditor.Tests
{
    public static class StringExtensions
    {
        public static T DeserializeTo<T>(this string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
