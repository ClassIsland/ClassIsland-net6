﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Controls;
using ClassIsland.Services;
using MaterialDesignThemes.Wpf;

namespace ClassIsland.Controls.Components;

/// <summary>
/// ScheduleComponentSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class ScheduleComponentSettingsControl
{
    public SettingsService SettingsService { get; }

    public ScheduleComponentSettingsControl(SettingsService settingsService)
    {
        SettingsService = settingsService;
        InitializeComponent();
    }

    private async void ButtonImportLegacySettings_OnClick(object sender, RoutedEventArgs e)
    {
        var r = await this.ShowDialog(FindResource("MigrateConfirm")) as bool? ?? false;
        if (!r)
        {
            return;
        }
        var settings = SettingsService.Settings;
        Settings.CountdownSeconds = settings.CountdownSeconds;
        Settings.ExtraInfoType = settings.ExtraInfoType;
        Settings.IsCountdownEnabled = settings.IsCountdownEnabled;
        Settings.ShowExtraInfoOnTimePoint = settings.ShowExtraInfoOnTimePoint;
    }

    private void ButtonShowAttachedSettings_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsPageBase.OpenDrawerCommand.Execute(new RootAttachedSettingsDependencyControl(IAttachedSettingsHostService.RegisteredControls.First(x => x.Guid == new Guid("58e5b69a-764a-472b-bcf7-003b6a8c7fdf"))));
    }
}