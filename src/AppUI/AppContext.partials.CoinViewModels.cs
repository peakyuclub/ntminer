﻿using NTMiner.Core;
using NTMiner.Vms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace NTMiner {
    public partial class AppContext {
        public class CoinViewModels : ViewModelBase {
            private readonly Dictionary<Guid, CoinViewModel> _dicById = new Dictionary<Guid, CoinViewModel>();

            public CoinViewModels() {
                if (Design.IsInDesignMode) {
                    return;
                }
                NTMinerRoot.Current.OnContextReInited += () => {
                    _dicById.Clear();
                    Init();
                };
                NTMinerRoot.Current.OnReRendContext += () => {
                    AllPropertyChanged();
                };
                Init();
            }

            private void Init() {
                VirtualRoot.On<CoinAddedEvent>("添加了币种后刷新VM内存", LogEnum.DevConsole,
                    action: (message) => {
                        _dicById.Add(message.Source.GetId(), new CoinViewModel(message.Source));
                        Current.MinerProfileVms.OnPropertyChanged(nameof(Current.MinerProfileVms.CoinVm));
                        AllPropertyChanged();
                    }).AddToCollection(NTMinerRoot.Current.ContextHandlers);
                VirtualRoot.On<CoinRemovedEvent>("移除了币种后刷新VM内存", LogEnum.DevConsole,
                    action: message => {
                        _dicById.Remove(message.Source.GetId());
                        Current.MinerProfileVms.OnPropertyChanged(nameof(Current.MinerProfileVms.CoinVm));
                        AllPropertyChanged();
                    }).AddToCollection(NTMinerRoot.Current.ContextHandlers);
                VirtualRoot.On<CoinUpdatedEvent>("更新了币种后刷新VM内存", LogEnum.DevConsole,
                    action: message => {
                        CoinViewModel coinVm = _dicById[message.Source.GetId()];
                        bool justAsDualCoin = coinVm.JustAsDualCoin;
                        coinVm.Update(message.Source);
                        coinVm.TestWalletVm.Address = message.Source.TestWallet;
                        coinVm.OnPropertyChanged(nameof(coinVm.Wallets));
                        coinVm.OnPropertyChanged(nameof(coinVm.WalletItems));
                        if (Current.MinerProfileVms.CoinId == message.Source.GetId()) {
                            Current.MinerProfileVms.OnPropertyChanged(nameof(Current.MinerProfileVms.CoinVm));
                        }
                        CoinKernelViewModel coinKernelVm = Current.MinerProfileVms.CoinVm.CoinKernel;
                        if (coinKernelVm != null
                            && coinKernelVm.CoinKernelProfile.SelectedDualCoin != null
                            && coinKernelVm.CoinKernelProfile.SelectedDualCoin.GetId() == message.Source.GetId()) {
                            coinKernelVm.CoinKernelProfile.OnPropertyChanged(nameof(coinKernelVm.CoinKernelProfile.SelectedDualCoin));
                        }
                        if (justAsDualCoin != coinVm.JustAsDualCoin) {
                            OnPropertyChanged(nameof(MainCoins));
                        }
                    }).AddToCollection(NTMinerRoot.Current.ContextHandlers);
                VirtualRoot.On<CoinIconDownloadedEvent>("下载了币种钱包后", LogEnum.DevConsole,
                    action: message => {
                        try {
                            if (string.IsNullOrEmpty(message.Source.Icon)) {
                                return;
                            }
                            string iconFileFullName = AppStatic.GetIconFileFullName(message.Source);
                            if (string.IsNullOrEmpty(iconFileFullName) || !File.Exists(iconFileFullName)) {
                                return;
                            }
                            CoinViewModel coinVm;
                            if (_dicById.TryGetValue(message.Source.GetId(), out coinVm)) {
                                try {
                                    coinVm.IconImageSource = new BitmapImage(new Uri(iconFileFullName, UriKind.Absolute));
                                }
                                catch (Exception e) {
                                    File.Delete(iconFileFullName);
                                    Logger.ErrorDebugLine(e.Message, e);
                                }
                            }
                        }
                        catch (Exception e) {
                            Logger.ErrorDebugLine(e.Message, e);
                        }
                    }).AddToCollection(NTMinerRoot.Current.ContextHandlers);
                foreach (var item in NTMinerRoot.Current.CoinSet) {
                    _dicById.Add(item.GetId(), new CoinViewModel(item));
                }
                foreach (var coinVm in _dicById.Values) {
                    coinVm.RefreshIcon();
                }
            }

            public bool TryGetCoinVm(Guid coinId, out CoinViewModel coinVm) {
                return _dicById.TryGetValue(coinId, out coinVm);
            }

            public bool TryGetCoinVm(string coinCode, out CoinViewModel coinVm) {
                ICoin coin;
                if (NTMinerRoot.Current.CoinSet.TryGetCoin(coinCode, out coin)) {
                    return TryGetCoinVm(coin.GetId(), out coinVm);
                }
                coinVm = CoinViewModel.Empty;
                return false;
            }

            public List<CoinViewModel> AllCoins {
                get {
                    return _dicById.Values.OrderBy(a => a.SortNumber).ToList();
                }
            }

            public List<CoinViewModel> MainCoins {
                get {
                    return AllCoins.Where(a => !a.JustAsDualCoin).ToList();
                }
            }

            private IEnumerable<CoinViewModel> GetPleaseSelect() {
                yield return CoinViewModel.PleaseSelect;
                foreach (var item in AllCoins) {
                    yield return item;
                }
            }
            public List<CoinViewModel> PleaseSelect {
                get {
                    return GetPleaseSelect().ToList();
                }
            }

            private IEnumerable<CoinViewModel> GetMainCoinPleaseSelect() {
                yield return CoinViewModel.PleaseSelect;
                foreach (var item in MainCoins) {
                    yield return item;
                }
            }
            public List<CoinViewModel> MainCoinPleaseSelect {
                get {
                    return GetMainCoinPleaseSelect().OrderBy(a => a.SortNumber).ToList();
                }
            }

            private IEnumerable<CoinViewModel> GetDualPleaseSelect() {
                yield return CoinViewModel.PleaseSelect;
                yield return CoinViewModel.DualCoinEnabled;
                foreach (var group in AppContext.Current.GroupVms.List) {
                    foreach (var item in group.DualCoinVms) {
                        yield return item;
                    }
                }
            }
            public List<CoinViewModel> DualPleaseSelect {
                get {
                    return GetDualPleaseSelect().Distinct().OrderBy(a => a.SortNumber).ToList();
                }
            }
        }
    }
}
