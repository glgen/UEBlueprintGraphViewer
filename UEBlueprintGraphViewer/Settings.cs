using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System.IO;

namespace UEBlueprintGraphViewer
{
    public partial class Settings : ObservableObject
    {
        public static Settings Instance = new();

        [ObservableProperty]
        private bool reorderEvents;

        [ObservableProperty]
        private bool selectedEventFirst;

        [ObservableProperty]
        private string gameProfileName;

        [ObservableProperty]
        [property: JsonIgnore]
        private GameSettings game = new();

        [ObservableProperty]
        private GameSettings? compareGame1;

        [ObservableProperty]
        private GameSettings? compareGame2;

        [ObservableProperty]
        private bool isInCompareMode;
        
        [ObservableProperty]
        private bool isMSAGL;

        // Debug settings
        public static bool DrawDebugGraph;

        public static bool ExperimentalExecStraightening;

        [JsonIgnore]
        public Dictionary<string, BPGraph> Macros = [];

        const string ConfigPath = "settings.json";

        public static Settings ReadConfig()
        {
            Settings settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(ConfigPath)) ?? new();

            if (GameSettings.IsConfigExists(settings.GameProfileName))
                settings.Game = GameSettings.ReadConfig(settings.GameProfileName);

            LoadMacros(settings);
            return settings;
        }

        public static void LoadMacros(Settings settings)
        {
            settings.Macros.Clear();
            string[] macrosFiles = Directory.GetFiles("Macros", "*.json");
            foreach (var path in macrosFiles)
            {
                BPGraph graph = GraphJson.FromJson(File.ReadAllText(path));
                settings.Macros.Add(Path.GetFileNameWithoutExtension(path), graph);
            }
        }

        public void WriteConfig()
        {
            string config = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(ConfigPath, config);
            Game.WriteConfig();
        }

        public static bool IsConfigExists()
        {
            return File.Exists(ConfigPath);
        }

        public static void SaveMacro(string macroName,  BPGraph macroGraph)
        {
            string fileName = $"Macros/{macroName}.json";
            File.WriteAllText(fileName, GraphJson.ToJson(macroGraph));
        }
    }

    public partial class GameSettings : ObservableObject
    {
        [JsonIgnore]
        public string ProfileName { get; set; }

        [ObservableProperty]
        private string paksFolder;

        [ObservableProperty]
        private string objectDump;

        [ObservableProperty]
        private string mappings;

        [ObservableProperty]
        private EGame _UEVersion = EGame.GAME_UE5_5;

        [JsonIgnore]
        public ParamMappings ParamsDump;

        public static GameSettings ReadConfig(string profileName)
        {
            GameSettings settings = JsonConvert.DeserializeObject<GameSettings>(File.ReadAllText($"Profiles/{profileName}.json")) ?? new();
            settings.ProfileName = profileName;
            return settings;
        }

        public GameSettings() { }

        public GameSettings(GameSettings game)
        {
            ProfileName = game.ProfileName;
            PaksFolder = game.PaksFolder;
            ObjectDump = game.ObjectDump;
            Mappings = game.Mappings;
            UEVersion = game.UEVersion;
        }

        public void LoadParamDumpings()
        {
            if (File.Exists(ObjectDump))
                ParamsDump = new ParamMappings(ObjectDump);
        }

        public void WriteConfig()
        {
            string config = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText($"Profiles/{ProfileName}.json", config);
        }

        public static bool IsConfigExists(string profileName)
        {
            return File.Exists($"Profiles/{profileName}.json");
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(PaksFolder) && !string.IsNullOrEmpty(ObjectDump);
        }
    }
}
