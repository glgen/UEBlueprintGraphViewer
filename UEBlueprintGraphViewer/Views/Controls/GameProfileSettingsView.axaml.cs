using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using CUE4Parse.UE4.Versions;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace UEBlueprintGraphViewer;

public partial class GameProfileSettingsView : UserControl
{
    public static List<EGame> VersionOptions { get; } =
    [
        EGame.GAME_UE4_0,
        EGame.GAME_UE4_1,
        EGame.GAME_UE4_2,
        EGame.GAME_UE4_3,
        EGame.GAME_UE4_4,
        EGame.GAME_UE4_5,
        EGame.GAME_UE4_6,
        EGame.GAME_UE4_7,
        EGame.GAME_UE4_8,
        EGame.GAME_UE4_9,
        EGame.GAME_UE4_10,
        EGame.GAME_UE4_11,
        EGame.GAME_UE4_12,
        EGame.GAME_UE4_13,
        EGame.GAME_UE4_14,
        EGame.GAME_UE4_15,
        EGame.GAME_UE4_16,
        EGame.GAME_UE4_17,
        EGame.GAME_UE4_18,
        EGame.GAME_UE4_19,
        EGame.GAME_UE4_20,
        EGame.GAME_UE4_21,
        EGame.GAME_UE4_22,
        EGame.GAME_UE4_23,
        EGame.GAME_UE4_24,
        EGame.GAME_UE4_25,
        EGame.GAME_UE4_26,
        EGame.GAME_UE4_27,
        EGame.GAME_UE5_0,
        EGame.GAME_UE5_1,
        EGame.GAME_UE5_2,
        EGame.GAME_UE5_3,
        EGame.GAME_UE5_4,
        EGame.GAME_UE5_5,
        EGame.GAME_UE5_6,
        EGame.GAME_UE5_7,
        EGame.GAME_UE5_8,
    ];

    GameSettings game;
    IStorageProvider storageProvider;

    public static readonly StyledProperty<ICommand?> UpdatedProperty =
    AvaloniaProperty.Register<Button, ICommand?>(nameof(Updated), enableDataValidation: true);
    public ICommand? Updated { get => GetValue(UpdatedProperty);set => SetValue(UpdatedProperty, value); }

    public GameProfileSettingsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        storageProvider = this.FindLogicalAncestorOfType<Window>()?.StorageProvider!;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is GameSettings settings)
            game = settings;
    }

    private async void LoadFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folders = await storageProvider.OpenFolderPickerAsync(new());

        if (folders.Any())
        {
            game.PaksFolder = folders.First().Path.LocalPath;
            Updated?.Execute(null);
        }
    }

    private async void LoadDumpButton_Click(object sender, RoutedEventArgs e)
    {
        var files = await storageProvider.OpenFilePickerAsync(new()
        {
            Title = "Select .jmap file",
            FileTypeFilter = [new(".jmap file") { Patterns = ["*.jmap", "*.jmap.gz"] }]
        });

        if (files.Any())
        {
            game.ObjectDump = files.First().Path.LocalPath;
            Updated?.Execute(null);
        }
    }

    
    private void EraseAESButton_Click(object sender, RoutedEventArgs e)
    {
        game.EncryptionKey = "0x0000000000000000000000000000000000000000000000000000000000000000";
    }
}