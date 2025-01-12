﻿using System.Drawing.Imaging;
using System.IO;
using System.Windows.Input;
using DlssUpdater.Controls;
using DlssUpdater.Defines;
using DlssUpdater.GameLibrary;
using DlssUpdater.Helpers;
using DlssUpdater.Singletons;
using DlssUpdater.ViewModels.Pages;
using DLSSUpdater.Singletons;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Controls;
using static DlssUpdater.Defines.DlssTypes;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace DlssUpdater.Views.Pages;

public partial class GamesPage : INavigableView<GamesViewModel>
{
    private readonly GameContainer _gameContainer;
    private readonly ISnackbarService _snackbar;
    private readonly DllUpdater _updater;
    private readonly AsyncFileWatcher _fileWatcher;

    public GamesPage(GamesViewModel viewModel, DllUpdater updater, GameContainer gameContainer,
        ISnackbarService snackbar, AsyncFileWatcher watcher)
    {
        _updater = updater;
        _gameContainer = gameContainer;
        _snackbar = snackbar;
        _fileWatcher = watcher;
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        gridProps.Visibility = Visibility.Collapsed;
        gridProps.Opacity = 0;
    }

    private bool _newGameInfo { get; set; }
    public GamesViewModel ViewModel { get; }

    private void ButtonAdd_Click(object sender, RoutedEventArgs e)
    {
        _newGameInfo = true;
        ViewModel.SelectedGame = new GameInfo("", "", LibraryType.Manual);
        updateUi();
        gridProps.ToggleControlFade(100, 1.0);
    }

    private async void GameButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is GameButton btn && btn.GameInfo is not null)
        {
            if (btn.GameInfo != ViewModel.SelectedGame && !await ensureNewGameData()) return;

            _newGameInfo = false;
            await btn.GameInfo.GatherInstalledVersions();
            ViewModel.SelectedGame = btn.GameInfo;
            updateUi();
            gridProps.ToggleControlFade(100, 1.0);
        }
    }

    private void btnPropsClose_Click(object sender, RoutedEventArgs e)
    {
        gridProps.ToggleControlFade(100, 0.0);
    }

    private async void ButtonPath_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dlg = new()
        {
            Multiselect = false
        };
        if (dlg.ShowDialog() == true)
        {
            ViewModel.SelectedGame!.GamePath = dlg.FolderName;
            if (string.IsNullOrEmpty(ViewModel.SelectedGame?.GameName))
                // Set folder name as name
                ViewModel.SelectedGame!.GameName = Path.GetFileName(dlg.FolderName)!;
            await ViewModel.SelectedGame!.GatherInstalledVersions();
            updateUi();
        }
    }

    private async void btnPropsAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_newGameInfo)
        {
            ViewModel.Games!.Add(ViewModel.SelectedGame!);
            _fileWatcher.AddFile(ViewModel.SelectedGame!);
        }
        else
        {
            var index = ViewModel.Games!.IndexOf(ViewModel.SelectedGame!);
            if (index != -1) ViewModel.Games[index] = ViewModel.SelectedGame!;
        }

        ViewModel.SelectedGame!.InstalledDlls[DllType.Dlss].Version = ViewModel.DlssItem ?? "";
        ViewModel.SelectedGame!.InstalledDlls[DllType.DlssD].Version = ViewModel.DlssDItem ?? "";
        ViewModel.SelectedGame!.InstalledDlls[DllType.DlssG].Version = ViewModel.DlssGItem ?? "";

        var updateResult = _updater.UpdateGameDlls(ViewModel.SelectedGame!);
        _gameContainer.SaveGames();

        gridProps.ToggleControlFade(100, 0.0);

        switch (updateResult)
        {
            case DllUpdater.UpdateResult.NothingDone:
                _snackbar.ShowEx("Installation", _newGameInfo ? "Added successfully" : "Nothing done",
                    ControlAppearance.Dark);
                break;
            case DllUpdater.UpdateResult.Success:
                _snackbar.ShowEx("Installation", "DLLs were installed successfully!", ControlAppearance.Success);
                break;
            case DllUpdater.UpdateResult.Failure:
                _snackbar.ShowEx("Installation", "DLLs were not installed!", ControlAppearance.Danger);
                break;
        }

        await ViewModel.SelectedGame!.GatherInstalledVersions();
        _gameContainer.DoUpdate();
    }

    private async Task<bool> ensureNewGameData()
    {
        if (gridProps.Visibility == Visibility.Visible)
        {
            var uiMessageBox = new MessageBox
            {
                Title = "Warning",
                Content = "The currently open game has not been saved and changed data will be lost. Continue?",
                SecondaryButtonText = "Yes",
                CloseButtonText = "Cancel",
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = true
            };

            return await uiMessageBox.ShowDialogAsync() == MessageBoxResult.Secondary;
        }

        return true;
    }

    private void ButtonImage_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dlg = new();
        dlg.CheckFileExists = true;
        dlg.Multiselect = false;
        dlg.Filter = "";

        var codecs = ImageCodecInfo.GetImageEncoders();
        var sep = string.Empty;

        foreach (var c in codecs)
        {
            var codecName = c.CodecName!.Substring(8).Replace("Codec", "Files").Trim();
            dlg.Filter = string.Format("{0}{1}{2} ({3})|{3}", dlg.Filter, sep, codecName, c.FilenameExtension);
            sep = "|";
        }

        if (dlg.ShowDialog() == true)
        {
            ViewModel.SelectedGame!.SetGameImageUri(dlg.FileName);
            updateUi();
        }
    }

    private void updateUi()
    {
        var isConfigurable = ViewModel.SelectedGame!.LibraryType == LibraryType.Manual;
        edtName.IsEnabled = isConfigurable;
        edtPath.IsEnabled = isConfigurable;
        edtImage.IsEnabled = isConfigurable;
        btnPath.IsEnabled = isConfigurable;
        btnImage.IsEnabled = isConfigurable;
        gridAntiCheat.Visibility = ViewModel.SelectedGame!.HasAntiCheat ? Visibility.Visible : Visibility.Hidden;
        ViewModel.Update();
    }
}