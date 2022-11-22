using Newtonsoft.Json;
using System.IO;

namespace CSMaze
{
    /// <summary>
    /// Contains the methods for loading and saving to the level JSON file.
    /// </summary>
    public static class MazeLevels
    {
        /// <summary>
        /// Load and deserialize a level JSON file. The file must be a list of levels as created by the <see cref="SaveLevelJson"/> function.
        /// </summary>
        public static Level[] LoadLevelJson(string path)
        {
            List<JsonLevel>? jsonLevels = JsonConvert.DeserializeObject<List<JsonLevel>>(File.ReadAllText(path));
            return jsonLevels is null ? Array.Empty<Level>() : jsonLevels.Select(x => x.GetLevel()).ToArray();
        }

        /// <summary>
        /// Serialize and save a list of levels.
        /// </summary>
        public static void SaveLevelJson(string path, Level[] levels)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(levels.Select(x => x.GetJsonLevel())));
        }
    }
}
