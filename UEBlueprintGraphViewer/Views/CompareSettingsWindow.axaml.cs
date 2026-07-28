using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using UEBlueprintGraphViewer.Comparing;

namespace UEBlueprintGraphViewer
{
    public partial class CompareSettingsWindow : Window
    {
        Settings settings = new();
        public GameSettings Game1 { get; set; }
        public GameSettings Game2 { get; set; }

        private bool applied;

        public CompareSettingsWindow()
        {
            if (Settings.IsConfigExists())
                settings = Settings.ReadConfig();

            Game1 = settings.CompareGame1 ?? new(settings.Game);
            Game2 = settings.CompareGame2 ?? new(settings.Game);
            
            InitializeComponent();
            DataContext = this;
            CheckIsValid();
        }

        public void CheckIsValid()
        {
            ApplyButton.IsEnabled = Game1.IsValid() && Game2.IsValid();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            applied = true;
            settings.CompareGame1 = Game1;
            settings.CompareGame2 = Game2;
            settings.IsInCompareMode = true;
            settings.WriteConfig();
            AssetsComparer.DeleteCache();
            Close();
        }

        public static async Task<bool> ShowWindow(Window owner)
        {
            CompareSettingsWindow dialog = new();
            await dialog.ShowDialog(owner);
            return dialog.applied;
        }
    }
}
