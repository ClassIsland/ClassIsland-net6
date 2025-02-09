﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ClassIsland.Core.Abstractions.Services;
using H.NotifyIcon;
using H.NotifyIcon.Core;

using Microsoft.Extensions.Hosting;

namespace ClassIsland.Services;

public class TaskBarIconService : IHostedService, ITaskBarIconService
{
    public TaskbarIcon MainTaskBarIcon
    {
        get;
    } = new()
    {
        IconSource = new GeneratedIconSource()
        {
            BackgroundSource = new BitmapImage(new Uri("pack://application:,,,/ClassIsland;component/Assets/AppLogo.png", UriKind.Absolute)),
        },
        MenuActivation = PopupActivationMode.RightClick,
        ToolTipText = "ClassIsland"
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        return;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        return;
    }
}