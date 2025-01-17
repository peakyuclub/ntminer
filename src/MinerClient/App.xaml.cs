﻿using NTMiner.Core;
using NTMiner.Notifications;
using NTMiner.OverClock;
using NTMiner.View;
using NTMiner.Views;
using NTMiner.Vms;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace NTMiner {
    public partial class App : Application, IDisposable {
        public App() {
            if (DevMode.IsDevMode && !Debugger.IsAttached && !Design.IsInDesignMode) {
                Write.Init();
            }
            Logging.LogDir.SetDir(Path.Combine(VirtualRoot.GlobalDirFullName, "Logs"));
            AppUtil.Init(this);
            InitializeComponent();
        }

        private readonly IAppViewFactory _appViewFactory = new AppViewFactory();

        private bool createdNew;
        private Mutex appMutex;
        private static string s_appPipName = "ntminerclient";
        protected override void OnExit(ExitEventArgs e) {
            AppContext.NotifyIcon?.Dispose();
            NTMinerRoot.Instance.Exit();
            HttpServer.Stop();
            base.OnExit(e);
            ConsoleManager.Hide();
        }

        protected override void OnStartup(StartupEventArgs e) {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            VirtualRoot.Window<UpgradeCommand>(LogEnum.DevConsole,
                action: message => {
                    AppStatic.Upgrade(message.FileName, message.Callback);
                });
            if (!string.IsNullOrEmpty(CommandLineArgs.Upgrade)) {
                VirtualRoot.Execute(new UpgradeCommand(CommandLineArgs.Upgrade, () => {
                    UIThread.Execute(() => { Environment.Exit(0); });
                }));
            }
            else {
                try {
                    appMutex = new Mutex(true, s_appPipName, out createdNew);
                }
                catch (Exception) {
                    createdNew = false;
                }
                if (createdNew) {
                    if (!NTMiner.Windows.WMI.IsWmiEnabled) {
                        DialogWindow.ShowDialog(message: "开源矿工无法运行所需的组件，因为本机未开启WMI服务，开源矿工需要使用WMI服务检测windows的内存、显卡等信息，请先手动开启WMI。", title: "提醒", icon: "Icon_Error");
                        Shutdown();
                        Environment.Exit(0);
                    }
                    NTMinerOverClockUtil.ExtractResource();

                    NTMinerRoot.SetIsMinerClient(true);
                    NotiCenterWindowViewModel.IsHotKeyEnabled = true;
                    Window splashWindow = _appViewFactory.CreateSplashWindow();
                    splashWindow.Show();
                    NotiCenterWindow.Instance.Show();
                    if (DevMode.IsDevMode) {
                        HandlerIdsWindow window = new HandlerIdsWindow();
                        window.Show();
                    }
                    if (!NTMiner.Windows.Role.IsAdministrator) {
                        NotiCenterWindowViewModel.Instance.Manager
                            .CreateMessage()
                            .Warning("请以管理员身份运行。")
                            .WithButton("点击以管理员身份运行", button => {
                                Wpf.Util.RunAsAdministrator();
                            })
                            .Dismiss().WithButton("忽略", button => {
                                
                            }).Queue();
                    }
                    VirtualRoot.On<StartingMineFailedEvent>("开始挖矿失败", LogEnum.DevConsole,
                        action: message => {
                            AppContext.Instance.MinerProfileVm.IsMining = false;
                            Write.UserFail(message.Message);
                        });
                    NTMinerRoot.Instance.Init(() => {
                        _appViewFactory.Link();
                        if (NTMinerRoot.Instance.GpuSet.Count == 0) {
                            NotiCenterWindowViewModel.Instance.Manager.ShowErrorMessage("没有矿卡或矿卡未驱动。");
                        }
                        UIThread.Execute(() => {
                            if (NTMinerRoot.GetIsNoUi() && Registry.GetIsAutoStart()) {
                                MainWindow = NotiCenterWindow.Instance;
                                NotiCenterWindowViewModel.Instance.Manager.ShowSuccessMessage("已切换为无界面模式运行", "开源矿工");
                            }
                            else {
                                _appViewFactory.ShowMainWindow(isToggle: false);
                            }
                            AppContext.NotifyIcon = ExtendedNotifyIcon.Create("开源矿工", isMinerStudio: false);
                            splashWindow?.Close();
                        });
                        #region 处理显示主界面命令
                        VirtualRoot.Window<ShowMainWindowCommand>("处理显示主界面命令", LogEnum.DevConsole,
                            action: message => {
                                ShowMainWindow(message.IsToggle);
                            });
                        #endregion
                        Task.Factory.StartNew(() => {
                            try {
                                HttpServer.Start($"http://localhost:{Consts.MinerClientPort}");
                                NTMinerRoot.Instance.Start();
                            }
                            catch (Exception ex) {
                                Logger.ErrorDebugLine(ex);
                            }
                        });
                    });
                    Link();
                }
                else {
                    try {
                        _appViewFactory.ShowMainWindow(this, MinerServer.NTMinerAppType.MinerClient);
                    }
                    catch (Exception) {
                        DialogWindow.ShowDialog(message: "另一个NTMiner正在运行，请手动结束正在运行的NTMiner进程后再次尝试。", title: "提醒", icon: "Icon_Error");
                        Process currentProcess = Process.GetCurrentProcess();
                        NTMiner.Windows.TaskKill.KillOtherProcess(currentProcess);
                    }
                }
            }
            base.OnStartup(e);
        }

        private void ShowMainWindow(bool isToggle) {
            UIThread.Execute(() => {
                _appViewFactory.ShowMainWindow(isToggle);
                // 使状态栏显示显示最新状态
                if (NTMinerRoot.Instance.IsMining) {
                    var mainCoin = NTMinerRoot.Instance.CurrentMineContext.MainCoin;
                    if (mainCoin == null) {
                        return;
                    }
                    var coinShare = NTMinerRoot.Instance.CoinShareSet.GetOrCreate(mainCoin.GetId());
                    VirtualRoot.Happened(new ShareChangedEvent(coinShare));
                    if ((NTMinerRoot.Instance.CurrentMineContext is IDualMineContext dualMineContext) && dualMineContext.DualCoin != null) {
                        coinShare = NTMinerRoot.Instance.CoinShareSet.GetOrCreate(dualMineContext.DualCoin.GetId());
                        VirtualRoot.Happened(new ShareChangedEvent(coinShare));
                    }
                    AppContext.Instance.GpuSpeedVms.Refresh();
                }
            });
        }

        private void Link() {
            VirtualRoot.Window<CloseNTMinerCommand>("处理关闭NTMiner客户端命令", LogEnum.UserConsole,
                action: message => {
                    UIThread.Execute(() => {
                        try {
                            if (MainWindow != null) {
                                MainWindow.Close();
                            }
                            Shutdown();
                        }
                        catch (Exception e) {
                            Logger.ErrorDebugLine(e);
                            Environment.Exit(0);
                        }
                    });
                });
            VirtualRoot.Window<CloseMainWindowCommand>("处理关闭主界面命令", LogEnum.DevConsole,
                action: message => {
                    UIThread.Execute(() => {
                        MainWindow = NotiCenterWindow.Instance;
                        foreach (Window window in Windows) {
                            if (window != MainWindow) {
                                window.Close();
                            }
                        }
                        NotiCenterWindowViewModel.Instance.Manager.ShowSuccessMessage("已切换为无界面模式运行", "开源矿工");
                    });
                });
            #region 周期确保守护进程在运行
            Daemon.DaemonUtil.RunNTMinerDaemon();
            VirtualRoot.On<Per20SecondEvent>("周期确保守护进程在运行", LogEnum.DevConsole,
                action: message => {
                    if (Registry.GetDaemonActiveOn().AddSeconds(20) < DateTime.Now) {
                        Daemon.DaemonUtil.RunNTMinerDaemon();
                    }
                });
            #endregion
            #region 1080小药丸
            VirtualRoot.On<MineStartedEvent>("开始挖矿后启动1080ti小药丸、挖矿开始后如果需要启动DevConsole则启动DevConsole", LogEnum.DevConsole,
                action: message => {
                    // 启动DevConsole
                    if (NTMinerRoot.IsUseDevConsole) {
                        var mineContext = message.MineContext;
                        string poolIp = mineContext.MainCoinPool.GetIp();
                        string consoleTitle = mineContext.MainCoinPool.Server;
                        Daemon.DaemonUtil.RunDevConsoleAsync(poolIp, consoleTitle);
                    }
                    OhGodAnETHlargementPill.OhGodAnETHlargementPillUtil.Start();
                });
            VirtualRoot.On<MineStopedEvent>("停止挖矿后停止1080ti小药丸", LogEnum.DevConsole,
                action: message => {
                    OhGodAnETHlargementPill.OhGodAnETHlargementPillUtil.Stop();
                });
            #endregion
            #region 处理开启A卡计算模式
            VirtualRoot.Window<SwitchRadeonGpuCommand>("处理开启A卡计算模式命令", LogEnum.DevConsole,
                action: message => {
                    if (NTMinerRoot.Instance.GpuSet.GpuType == GpuType.AMD) {
                        if (NTMinerRoot.Instance.IsMining) {
                            NTMinerRoot.Instance.StopMineAsync(() => {
                                SwitchRadeonGpuMode();
                                NTMinerRoot.Instance.StartMine();
                            });
                        }
                        else {
                            SwitchRadeonGpuMode();
                        }
                    }
                });
            #endregion
            #region 处理A卡驱动签名
            VirtualRoot.Window<AtikmdagPatcherCommand>("处理A卡驱动签名命令", LogEnum.DevConsole,
                action: message => {
                    if (NTMinerRoot.Instance.GpuSet.GpuType == GpuType.AMD) {
                        if (NTMinerRoot.Instance.IsMining) {
                            NTMinerRoot.Instance.StopMineAsync(() => {
                                AtikmdagPatcher.AtikmdagPatcher.Run();
                                NTMinerRoot.Instance.StartMine();
                            });
                        }
                        else {
                            AtikmdagPatcher.AtikmdagPatcher.Run();
                        }
                    }
                });
            #endregion
        }

        private static void SwitchRadeonGpuMode() {
            SwitchRadeonGpu.SwitchRadeonGpu.Run((isSuccess, e) => {
                if (isSuccess) {
                    NotiCenterWindowViewModel.Instance.Manager.ShowSuccessMessage("开启A卡计算模式成功");
                }
                else if (e != null) {
                    NotiCenterWindowViewModel.Instance.Manager.ShowErrorMessage(e.Message, delaySeconds: 4);
                }
                else {
                    NotiCenterWindowViewModel.Instance.Manager.ShowErrorMessage("开启A卡计算模式失败", delaySeconds: 4);
                }
            });
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                if (appMutex != null) {
                    appMutex.Dispose();
                }
            }
        }
    }
}
