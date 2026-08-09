using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

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

        [ObservableProperty]
        private bool rememberOpenTabs = true;

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
            string[] macrosFiles = [.. Directory.GetFiles("Macros", "*.json").OrderBy(o => o)];
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
        private string _paksFolder;

        [ObservableProperty]
        private string _objectDump;
        
        [ObservableProperty]
        private string _encryptionKey = "0x0000000000000000000000000000000000000000000000000000000000000000";

        [ObservableProperty]
        private EGame _UEVersion = EGame.GAME_UE5_5;
        
        public List<string> OpenTabs { get; set; } = [];
        
        public string? ActiveTab { get; set; }

        [JsonIgnore]
        public JmapData Jmap;

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
            UEVersion = game.UEVersion;
            EncryptionKey = game.EncryptionKey;
        }

        public void LoadParamDumpings()
        {
            if (File.Exists(ObjectDump))
                Jmap = new JmapData(ObjectDump);
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
