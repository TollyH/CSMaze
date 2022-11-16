using Newtonsoft.Json;

namespace CSMaze
{
    public static class MazeLevels
    {
        public static Level[] LoadLevelJson(string path)
        {
            List<JsonLevel>? jsonLevels = JsonConvert.DeserializeObject<List<JsonLevel>>(File.ReadAllText(path));
            return jsonLevels is null ? Array.Empty<Level>() : jsonLevels.Select(x => x.GetLevel()).ToArray();
        }

        public static void SaveLevelJson(string path, Level[] levels)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(levels.Select(x => x.GetJsonLevel())));
        }
    }
}
