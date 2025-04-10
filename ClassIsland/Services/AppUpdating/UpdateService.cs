using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
#if IsMsix
using Windows.Storage;
#endif
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Helpers;
using ClassIsland.Core.Helpers.Native;
using ClassIsland.Core.Models;
using ClassIsland.Core.Models.Updating;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Shared;
using ClassIsland.Shared.Enums;
using ClassIsland.Shared.Helpers;
using ClassIsland.Views;
using Downloader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Bcpg.OpenPgp;
using PgpCore;
using Application = System.Windows.Application;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
using File = System.IO.File;
using ProductHeaderValue = Octokit.ProductHeaderValue;

namespace ClassIsland.Services.AppUpdating;

public class UpdateService : IHostedService, INotifyPropertyChanged
{
    private UpdateWorkingStatus _currentWorkingStatus = UpdateWorkingStatus.Idle;
    private long _downloadedSize = 0;
    private long _totalSize = 0;
    private double _downloadSpeed = 0;
    public IDownload? Downloader;
    private bool _isCanceled = false;
    private Exception? _networkErrorException;
    private TimeSpan _downloadEtcSeconds = TimeSpan.Zero;
    private VersionInfo _selectedVersionInfo = new();

    public string CurrentUpdateSourceUrl => Settings.SelectedChannel;

    internal static string UpdateCachePath { get; } = Path.Combine(App.AppCacheFolderPath, "Update");

    internal const string UpdateMetadataUrl =
        "https://get.classisland.tech/d/ClassIsland-Ningbo-S3/classisland/disturb-net6/index.json";

    public static string UpdateTempPath =>
#if IsMsix
        Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "UpdateTemp");
#else
        Path.Combine(App.AppRootFolderPath, "UpdateTemp");
#endif

    public VersionsIndex Index { get; set; }

    public VersionInfo SelectedVersionInfo
    {
        get => _selectedVersionInfo;
        set => SetField(ref _selectedVersionInfo, value);
    }

    public Stopwatch DownloadStatusUpdateStopwatch { get; } = new();

    public UpdateWorkingStatus CurrentWorkingStatus
    {
        get => _currentWorkingStatus;
        set => SetField(ref _currentWorkingStatus, value);
    }

    private SettingsService SettingsService
    {
        get;
    }

    private Settings Settings => SettingsService.Settings;

    private ITaskBarIconService TaskBarIconService
    {
        get;
    }

    private ISplashService SplashService { get; }

    private ILogger<UpdateService> Logger { get; }

    private string MetadataPublisherPublicKey { get; }

    public event EventHandler? UpdateInfoUpdated;

    public UpdateService(SettingsService settingsService, ITaskBarIconService taskBarIconService, IHostApplicationLifetime lifetime,
        ISplashService splashService, ILogger<UpdateService> logger)
    {
        SettingsService = settingsService;
        TaskBarIconService = taskBarIconService;
        SplashService = splashService;
        Logger = logger;

        var keyStream = Application
            .GetResourceStream(new Uri("/Assets/TrustedPublicKeys/ClassIsland.MetadataPublisher.asc", UriKind.RelativeOrAbsolute))!.Stream;
        MetadataPublisherPublicKey = new StreamReader(keyStream).ReadToEnd();

        Index = ConfigureFileHelper.LoadConfig<VersionsIndex>(Path.Combine(UpdateCachePath, "Index.json"));
        SelectedVersionInfo = ConfigureFileHelper.LoadConfig<VersionInfo>(Path.Combine(UpdateCachePath, "SelectedVersionInfo.json"));


        SyncSpeedTestResults();
    }

    private void SyncSpeedTestResults()
    {
        foreach (var i in Index.Mirrors)
        {
            if (!Settings.SpeedTestResults.ContainsKey(i.Key))
            {
                Settings.SpeedTestResults.Add(i.Key, new SpeedTestResult());
            }
            i.Value.SpeedTestResult = Settings.SpeedTestResults[i.Key];
        }
    }

    public bool IsCanceled
    {
        get => _isCanceled;
        set => SetField(ref _isCanceled, value);
    }

    public long DownloadedSize
    {
        get => _downloadedSize;
        set => SetField(ref _downloadedSize, value);
    }

    public long TotalSize
    {
        get => _totalSize;
        set => SetField(ref _totalSize, value);
    }

    public double DownloadSpeed
    {
        get => _downloadSpeed;
        set => SetField(ref _downloadSpeed, value);
    }

    public Exception? NetworkErrorException
    {
        get => _networkErrorException;
        set => SetField(ref _networkErrorException, value);
    }

    public TimeSpan DownloadEtcSeconds
    {
        get => _downloadEtcSeconds;
        set => SetField(ref _downloadEtcSeconds, value);
    }

    public async Task<bool> AppStartup()
    {
        if (Settings.AutoInstallUpdateNextStartup 
            && Settings.LastUpdateStatus == UpdateStatus.UpdateDownloaded
            && File.Exists(Path.Combine(UpdateTempPath, "update.zip")))
        {
            SplashService.SplashStatus = "正在准备更新…";
            App.GetService<ISplashService>().CurrentProgress = 90;
            return await RestartAppToUpdateAsync();
        }

        if (Settings.UpdateMode < 1)
        {
            return false;
        }
        
        _ = AppStartupBackground();
        return false;
    }

    private async Task AppStartupBackground()
    {
        if (Settings.IsAutoSelectUpgradeMirror && DateTime.Now - Settings.LastSpeedTest >= TimeSpan.FromDays(7))
        {
            await App.GetService<UpdateNodeSpeedTestingService>().RunSpeedTestAsync();
        }
        await CheckUpdateAsync();

        if (Settings.UpdateMode < 2)
        {
            return;
        }

        if (Settings.LastUpdateStatus == UpdateStatus.UpdateAvailable)
        {
            await DownloadUpdateAsync();
            Settings.AutoInstallUpdateNextStartup = false;
        }

        if (Settings.UpdateMode < 3)
        {
            return;
        }

        if (Settings.LastUpdateStatus == UpdateStatus.UpdateDownloaded)
        {
            Settings.AutoInstallUpdateNextStartup = true;
        }
    }


    public static async Task ReplaceApplicationFile(string target)
    {
        var progressWindow = new UpdateProgressWindow();
        progressWindow.Show();
        if (!File.Exists(target))
        {
            return;
        }
        var s = Environment.ProcessPath!;
        var t = target;
        Console.WriteLine(Path.GetFullPath(t));
        Console.WriteLine(Path.GetDirectoryName(Path.GetFullPath(t)));
        progressWindow.ProgressText = "正在备份应用数据……";
        await FileFolderService.CreateBackupAsync(filename: $"Update_Backup_{App.AppVersion}_{DateTime.Now:yy-MMM-dd_HH-mm-ss}", rootPath: Path.GetDirectoryName(Path.GetFullPath(t)) ?? ".");
        progressWindow.ProgressText = "正在等待应用退出……";
        await Task.Run(() => NativeWindowHelper.WaitForFile(t));
        progressWindow.ProgressText = "正在覆盖应用文件……";
        await Task.Run(() => File.Copy(s, t, true));
        progressWindow.CanClose = true;
        progressWindow.Close();
    }

    public static void RemoveUpdateTemporary(string target)
    {
        if (File.Exists(target))
        {
            NativeWindowHelper.WaitForFile(target);
            File.Delete(target);
        }
        try
        {
            Directory.Delete(UpdateTempPath, true);
        }
        catch (Exception e)
        {
            // ignored
            Console.WriteLine(e);
        }
    }


    public async Task CheckUpdateAsync(bool isForce=false, bool isCancel=false)
    {
        try
        {
            CurrentWorkingStatus = UpdateWorkingStatus.CheckingUpdates;
            Index = await WebRequestHelper.SaveJson<VersionsIndex>(new Uri(UpdateMetadataUrl + $"?time={DateTime.Now.ToFileTimeUtc()}"), Path.Combine(UpdateCachePath, "Index.json"), verifySign:true, publicKey:MetadataPublisherPublicKey);
            SyncSpeedTestResults();
            var version = Index.Versions
                .Where(x => Version.TryParse(x.Version, out _) && x.Channels.Contains(Settings.SelectedUpdateChannelV2))
                .OrderByDescending(x => Version.Parse(x.Version))
                .FirstOrDefault();
            if (version == null || !IsNewerVersion(isForce, isCancel, Version.Parse(version.Version)))
            {
                Settings.LastUpdateStatus = UpdateStatus.UpToDate;
                return;
            }

            SelectedVersionInfo = await WebRequestHelper.SaveJson<VersionInfo>(new Uri(version.VersionInfoUrl + $"?time={DateTime.Now.ToFileTimeUtc()}"), Path.Combine(UpdateCachePath, "SelectedVersionInfo.json"), verifySign: true, publicKey: MetadataPublisherPublicKey);
            Settings.LastUpdateStatus = UpdateStatus.UpdateAvailable;
            TaskBarIconService.ShowNotification("发现新版本",
                $"{Assembly.GetExecutingAssembly().GetName().Version} -> {version.Version}\n" +
                "点击以查看详细信息。", clickedCallback:UpdateNotificationClickedCallback);

            Settings.LastUpdateStatus = UpdateStatus.UpdateAvailable;
        }
        catch (Exception ex)
        {
            Settings.LastUpdateStatus = UpdateStatus.UpToDate;
            NetworkErrorException = ex;
            Logger.LogError(ex, "检查应用更新失败。");
        }
        finally
        {
            Settings.LastCheckUpdateTime = DateTime.Now;
            UpdateInfoUpdated?.Invoke(this, EventArgs.Empty);
            CurrentWorkingStatus = UpdateWorkingStatus.Idle;
        }
    }

    private void UpdateNotificationClickedCallback()
    {
        IAppHost.GetService<IUriNavigationService>().NavigateWrapped(new Uri("classisland://app/settings/update"));
    }

    private bool IsNewerVersion(bool isForce, bool isCancel, Version verCode)
    {
        return (verCode > Assembly.GetExecutingAssembly().GetName().Version &&
                (Settings.LastUpdateStatus != UpdateStatus.UpdateDownloaded || isCancel)) // 正常更新
               || isForce;
    }

    public async Task DownloadUpdateAsync()
    {
        try
        {
            if (Directory.Exists(UpdateTempPath))
            {
                Directory.Delete(UpdateTempPath, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "移除下载临时文件失败。");
        }
        try
        {
            var downloadInfo = SelectedVersionInfo.DownloadInfos[AppBase.Current.AppSubChannel];
            Settings.UpdateArtifactHash = downloadInfo.ArchiveSHA256;
            Logger.LogInformation("下载应用更新包：{}", downloadInfo.ArchiveDownloadUrls[Settings.SelectedUpdateMirrorV2]);
            TotalSize = 0;
            DownloadedSize = 0;
            DownloadSpeed = 0;
            DownloadStatusUpdateStopwatch.Start();
            CurrentWorkingStatus = UpdateWorkingStatus.DownloadingUpdates;

            Downloader = DownloadBuilder.New()
                .WithUrl(downloadInfo.ArchiveDownloadUrls[Settings.SelectedUpdateMirrorV2])
                .Configure((c) =>
                {
                    c.ChunkCount = 32;
                    c.ParallelCount = 32;
                    c.ParallelDownload = true;
                    //c.Timeout = 4096;
                })
                .WithDirectory(UpdateTempPath)
                .WithFileName("update.zip")
                .Build();
            Downloader.DownloadProgressChanged += DownloaderOnDownloadProgressChanged;
            Downloader.DownloadFileCompleted += (sender, args) =>
            {
                DownloadStatusUpdateStopwatch.Stop();
                DownloadStatusUpdateStopwatch.Reset();
                if (IsCanceled)
                {
                    IsCanceled = false;
                    return;
                }

                if (!File.Exists(Path.Combine(UpdateTempPath, @"update.zip")) || args.Error != null)
                {
                    //await RemoveDownloadedFiles();
                    throw new Exception("更新下载失败。", args.Error);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Settings.LastUpdateStatus = UpdateStatus.UpdateDownloaded;
                    });
                }
            };
            await Downloader.StartAsync();
        }
        catch (Exception ex)
        {
            NetworkErrorException = ex;
            Logger.LogError(ex, "下载应用更新失败。");
            await RemoveDownloadedFiles();
        }
        finally
        {
            CurrentWorkingStatus = UpdateWorkingStatus.Idle;
        }
    }

    public async void StopDownloading()
    {
        if (Downloader == null)
        {
            return;
        }

        Logger.LogInformation("应用更新下载停止。");
        IsCanceled = true;
        Downloader.Pause();
        Downloader.Dispose();
        CurrentWorkingStatus = UpdateWorkingStatus.Idle;
        await RemoveDownloadedFiles();
    }

    public async Task RemoveDownloadedFiles()
    {
        try
        {
            Directory.Delete(UpdateTempPath, true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "移除下载临时文件失败。");
        }
        await CheckUpdateAsync(isCancel:true);
    }

    private void DownloaderOnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        if (DownloadStatusUpdateStopwatch.ElapsedMilliseconds < 250)
            return;
        DownloadStatusUpdateStopwatch.Restart();
        TotalSize = e.TotalBytesToReceive;
        DownloadedSize = e.ReceivedBytesSize;
        DownloadSpeed = e.BytesPerSecondSpeed;
        
        DownloadEtcSeconds = TimeSpan.FromSeconds(DownloadSpeed == 0 ? 0 : (long)((TotalSize - DownloadedSize) / DownloadSpeed));
        Logger.LogInformation("Download progress changed: {}/{} ({}B/s)", TotalSize, DownloadedSize, DownloadSpeed);
    }

    public async Task ExtractUpdateAsync()
    {
        Logger.LogInformation("正在展开应用更新包。");
        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(Path.Combine(UpdateTempPath, @"./update.zip"), Path.Combine(UpdateTempPath, @"./extracted"), true);
        });
    }

    private async Task ValidateUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.UpdateArtifactHash))
        {
            Logger.LogWarning("未找到缓存的校验信息，跳过更新文件校验。");
            return;
        }

        await using var stream = File.OpenRead(Path.Combine(UpdateTempPath, @"./update.zip"));
        var sha256 = SHA256.Create();
        var result = await sha256.ComputeHashAsync(stream);
        var str = BitConverter.ToString(result);
        str = str.Replace("-", "");
        Logger.LogDebug("更新文件哈希：{}", str);
        if (!string.Equals(str, Settings.UpdateArtifactHash, StringComparison.CurrentCultureIgnoreCase))
        {
            throw new Exception("更新文件校验失败，可能下载已经损坏。");
        }
    }

    public async Task<bool> RestartAppToUpdateAsync()
    {
        var success = true;
        Logger.LogInformation("正在重启至升级模式。");
        TaskBarIconService.ShowNotification("正在安装应用更新", "这可能需要10-30秒的时间，请稍后……");
        CurrentWorkingStatus = UpdateWorkingStatus.ExtractingUpdates;
        try
        {
            await ValidateUpdateAsync();
            await ExtractUpdateAsync();
            Process.Start(new ProcessStartInfo()
            {
                FileName = Path.Combine(UpdateTempPath, @"extracted/ClassIsland.exe"),
                ArgumentList =
                {
                    "-urt", Environment.ProcessPath!,
                    "-m", "true"
                }
            });
            AppBase.Current.Stop();
        }
        catch (Exception ex)
        {
            success = false;
            Logger.LogError(ex, "无法安装更新");
            TaskBarIconService.ShowNotification("安装更新失败", ex.Message, clickedCallback:UpdateNotificationClickedCallback);
            CurrentWorkingStatus = UpdateWorkingStatus.Idle;
            await RemoveDownloadedFiles();
        }

        return success;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        return;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        return;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}