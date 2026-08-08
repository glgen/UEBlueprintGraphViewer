using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer
{
    public partial class SettingsWindow : Window
    {
        Settings settings = new();
        GameSettings game;

        public ObservableCollection<string> Profiles { get; set; } = [];

        private bool applied;

        public SettingsWindow()
        {
            InitializeComponent();

            if (Settings.IsConfigExists())
                settings = Settings.ReadConfig();

            foreach (var profile in Directory.GetFiles("Profiles", "*.json").Select(o => o[9..^5]))
                Profiles.Add(profile);

            if (!Settings.IsConfigExists() && Profiles.Any())
                settings.GameProfileName = Profiles[0];

            DataContext = settings;
            game = settings.Game;

            CheckIsValid();
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            while (!Profiles.Any())
                await CreateNewProfile(false);
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            if (!game.IsValid())
                e.Cancel = true;
        }

        public void CheckIsValid()
        {
            ApplyButton.IsEnabled = game.IsValid();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            applied = true;
            settings.IsInCompareMode = false;
            settings.WriteConfig();
            Close();
        }

        private async void NewProfileButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateNewProfile();
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameSettings.IsConfigExists(settings.GameProfileName))
            {
                game = GameSettings.ReadConfig(settings.GameProfileName);
                settings.Game = game;
            }
            else
            {
                game = new GameSettings { ProfileName = settings.GameProfileName };
                settings.Game = game;
            }
            CheckIsValid();
        }

        private async Task CreateNewProfile(bool canCancel = true)
        {
            DialogWindow window = await DialogWindow.Show("New profile name:", "New profile", canCancel, true, this);
            string profileName = window.EnteredText;

            if (window.Result == DialogWindowResult.Cancel)
                return;

            if (!string.IsNullOrEmpty(profileName))
            {
                string name = Utils.ToValidFileName(profileName);
                Profiles.Add(name);
                settings.GameProfileName = name;
            }
        }

        public static async Task<bool> ShowWindow(Window owner)
        {
            SettingsWindow dialog = new();
            await dialog.ShowDialog(owner);
            return dialog.applied;
        }
    }
}
