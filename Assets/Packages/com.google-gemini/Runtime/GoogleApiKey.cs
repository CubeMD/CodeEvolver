using System;
using System.Linq;
using System.IO;

namespace GoogleApis
{
    public static class GoogleApiKey
    {
        public static string apiKey;

        static GoogleApiKey()
        {
            // Load from env
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            var envFile = File.ReadAllText(envPath);

            var dict = envFile
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('='))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1]);

            if (!dict.TryGetValue("key", out string key))
            {
                throw new Exception($"key=YOUR_API_KEY not found in .env file");
            }

            apiKey = key;
        }
    }
}
