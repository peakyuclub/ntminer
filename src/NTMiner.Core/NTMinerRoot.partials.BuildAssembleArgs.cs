﻿using NTMiner.Core;
using NTMiner.Core.Gpus;
using NTMiner.Core.Kernels;
using NTMiner.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NTMiner {
    public partial class NTMinerRoot : INTMinerRoot {
        private static readonly string[] gpuIndexChars = new string[] { "a", "b", "c", "d", "e", "f", "g", "h" };
        public string BuildAssembleArgs(out Dictionary<string, string> parameters, out Dictionary<Guid, string> fileWriters, out Dictionary<Guid, string> fragments) {
            parameters = new Dictionary<string, string>();
            fileWriters = new Dictionary<Guid, string>();
            fragments = new Dictionary<Guid, string>();
            if (!CoinSet.TryGetCoin(this.MinerProfile.CoinId, out ICoin mainCoin)) {
                return string.Empty;
            }
            ICoinProfile coinProfile = this.MinerProfile.GetCoinProfile(mainCoin.GetId());
            if (!PoolSet.TryGetPool(coinProfile.PoolId, out IPool mainCoinPool)) {
                return string.Empty;
            }
            if (!CoinKernelSet.TryGetCoinKernel(coinProfile.CoinKernelId, out ICoinKernel coinKernel)) {
                return string.Empty;
            }
            if (!KernelSet.TryGetKernel(coinKernel.KernelId, out IKernel kernel)) {
                return string.Empty;
            }
            if (!kernel.IsSupported(mainCoin)) {
                return string.Empty;
            }
            if (!KernelInputSet.TryGetKernelInput(kernel.KernelInputId, out IKernelInput kernelInput)) {
                return string.Empty;
            }
            ICoinKernelProfile coinKernelProfile = this.MinerProfile.GetCoinKernelProfile(coinProfile.CoinKernelId);
            string poolKernelArgs = string.Empty;
            IPoolKernel poolKernel = PoolKernelSet.FirstOrDefault(a => a.PoolId == mainCoinPool.GetId() && a.KernelId == kernel.GetId());
            if (poolKernel != null) {
                poolKernelArgs = poolKernel.Args;
            }
            string kernelArgs = kernelInput.Args;
            string coinKernelArgs = coinKernel.Args;
            string customArgs = coinKernelProfile.CustomArgs;
            parameters.Add("mainCoin", mainCoin.Code);
            if (mainCoinPool.IsUserMode) {
                IPoolProfile poolProfile = MinerProfile.GetPoolProfile(mainCoinPool.GetId());
                string password = poolProfile.Password;
                if (string.IsNullOrEmpty(password)) {
                    password = "x";
                }
                parameters.Add("userName", poolProfile.UserName);
                parameters.Add("password", password);
            }
            else {
                parameters.Add("wallet", coinProfile.Wallet);
            }
            parameters.Add("host", mainCoinPool.GetHost());
            parameters.Add("port", mainCoinPool.GetPort().ToString());
            parameters.Add("pool", mainCoinPool.Server);
            parameters.Add("worker", this.MinerProfile.MinerName);
            if (coinKernel.IsSupportPool1) {
                parameters.Add("worker1", this.MinerProfile.MinerName);
                if (PoolSet.TryGetPool(coinProfile.PoolId1, out IPool mainCoinPool1)) {
                    parameters.Add("host1", mainCoinPool1.GetHost());
                    parameters.Add("port1", mainCoinPool1.GetPort().ToString());
                    parameters.Add("pool1", mainCoinPool1.Server);
                    if (mainCoinPool1.IsUserMode) {
                        IPoolProfile poolProfile1 = MinerProfile.GetPoolProfile(mainCoinPool1.GetId());
                        string password1 = poolProfile1.Password;
                        if (string.IsNullOrEmpty(password1)) {
                            password1 = "x";
                        }
                        parameters.Add("userName1", poolProfile1.UserName);
                        parameters.Add("password1", password1);
                    }
                    else {
                        parameters.Add("wallet1", coinProfile.Wallet);
                    }
                }
            }
            // 这里不要考虑{logfile}，{logfile}往后推迟
            if (coinKernelProfile.IsDualCoinEnabled && kernelInput.IsSupportDualMine) {
                Guid dualCoinGroupId = coinKernel.DualCoinGroupId;
                if (dualCoinGroupId == Guid.Empty) {
                    dualCoinGroupId = kernelInput.DualCoinGroupId;
                }
                if (dualCoinGroupId != Guid.Empty) {
                    if (this.CoinSet.TryGetCoin(coinKernelProfile.DualCoinId, out ICoin dualCoin)) {
                        ICoinProfile dualCoinProfile = this.MinerProfile.GetCoinProfile(dualCoin.GetId());
                        if (PoolSet.TryGetPool(dualCoinProfile.DualCoinPoolId, out IPool dualCoinPool)) {
                            IPoolProfile dualPoolProfile = MinerProfile.GetPoolProfile(dualCoinPool.GetId());
                            string dualPassword = dualPoolProfile.Password;
                            if (string.IsNullOrEmpty(dualPassword)) {
                                dualPassword = "x";
                            }
                            parameters.Add("dualCoin", dualCoin.Code);
                            parameters.Add("dualWallet", dualCoinProfile.DualCoinWallet);
                            parameters.Add("dualUserName", dualPoolProfile.UserName);
                            parameters.Add("dualPassword", dualPassword);
                            parameters.Add("dualHost", dualCoinPool.GetHost());
                            parameters.Add("dualPort", dualCoinPool.GetPort().ToString());
                            parameters.Add("dualPool", dualCoinPool.Server);

                            kernelArgs = kernelInput.DualFullArgs;
                            AssembleArgs(parameters, ref kernelArgs, isDual: true);
                            AssembleArgs(parameters, ref poolKernelArgs, isDual: true);
                            AssembleArgs(parameters, ref customArgs, isDual: true);

                            string dualWeightArg;
                            if (!string.IsNullOrEmpty(kernelInput.DualWeightArg)) {
                                if (coinKernelProfile.IsAutoDualWeight && kernelInput.IsAutoDualWeight) {
                                    dualWeightArg = string.Empty;
                                }
                                else {
                                    dualWeightArg = $"{kernelInput.DualWeightArg} {Convert.ToInt32(coinKernelProfile.DualCoinWeight)}";
                                }
                            }
                            else {
                                dualWeightArg = string.Empty;
                            }
                            StringBuilder dualSb = new StringBuilder();
                            dualSb.Append(kernelArgs);
                            if (!string.IsNullOrEmpty(dualWeightArg)) {
                                dualSb.Append(" ").Append(dualWeightArg);
                            }
                            if (!string.IsNullOrEmpty(poolKernelArgs)) {
                                dualSb.Append(" ").Append(poolKernelArgs);
                            }
                            BuildFragments(coinKernel, parameters, out fileWriters, out fragments);
                            foreach (var fragment in fragments.Values) {
                                dualSb.Append(" ").Append(fragment);
                            }
                            if (!string.IsNullOrEmpty(customArgs)) {
                                dualSb.Append(" ").Append(customArgs);
                            }

                            // 注意：这里退出
                            return dualSb.ToString();
                        }
                    }
                }
            }
            AssembleArgs(parameters, ref kernelArgs, isDual: false);
            AssembleArgs(parameters, ref coinKernelArgs, isDual: false);
            AssembleArgs(parameters, ref poolKernelArgs, isDual: false);
            AssembleArgs(parameters, ref customArgs, isDual: false);
            string devicesArgs = string.Empty;
            if (!string.IsNullOrWhiteSpace(kernelInput.DevicesArg)) {
                List<int> useDevices = this.GpuSet.GetUseDevices();
                if (useDevices.Count != 0 && useDevices.Count != GpuSet.Count) {
                    string separator = kernelInput.DevicesSeparator;
                    if (kernelInput.DevicesSeparator == "space") {
                        separator = " ";
                    }
                    if (string.IsNullOrEmpty(separator)) {
                        List<string> gpuIndexes = new List<string>();
                        foreach (var index in useDevices) {
                            int i = index;
                            if (kernelInput.DeviceBaseIndex != 0) {
                                i = index + kernelInput.DeviceBaseIndex;
                            }
                            if (i > 9) {
                                gpuIndexes.Add(gpuIndexChars[i - 10]);
                            }
                            else {
                                gpuIndexes.Add(i.ToString());
                            }
                        }
                        devicesArgs = $"{kernelInput.DevicesArg} {string.Join(separator, gpuIndexes)}";
                    }
                    else {
                        devicesArgs = $"{kernelInput.DevicesArg} {string.Join(separator, useDevices)}";
                    }
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(kernelArgs);
            if (!string.IsNullOrEmpty(coinKernelArgs)) {
                sb.Append(" ").Append(coinKernelArgs);
            }
            if (!string.IsNullOrEmpty(poolKernelArgs)) {
                sb.Append(" ").Append(poolKernelArgs);
            }
            if (!string.IsNullOrEmpty(devicesArgs)) {
                sb.Append(" ").Append(devicesArgs);
            }
            BuildFragments(coinKernel, parameters, out fileWriters, out fragments);
            foreach (var fragment in fragments.Values) {
                sb.Append(" ").Append(fragment);
            }
            if (!string.IsNullOrEmpty(customArgs)) {
                sb.Append(" ").Append(customArgs);
            }

            return sb.ToString();
        }

        private static void AssembleArgs(Dictionary<string, string> prms, ref string args, bool isDual) {
            if (string.IsNullOrEmpty(args)) {
                args = string.Empty;
                return;
            }
            args = args.Replace("{mainCoin}", prms["mainCoin"]);
            if (prms.ContainsKey("wallet")) {
                args = args.Replace("{wallet}", prms["wallet"]);
            }
            if (prms.ContainsKey("userName")) {
                args = args.Replace("{userName}", prms["userName"]);
            }
            if (prms.ContainsKey("password")) {
                args = args.Replace("{password}", prms["password"]);
            }
            args = args.Replace("{host}", prms["host"]);
            args = args.Replace("{port}", prms["port"]);
            args = args.Replace("{pool}", prms["pool"]);
            args = args.Replace("{worker}", prms["worker"]);
            if (isDual) {
                args = args.Replace("{dualCoin}", prms["dualCoin"]);
                args = args.Replace("{dualWallet}", prms["dualWallet"]);
                args = args.Replace("{dualUserName}", prms["dualUserName"]);
                args = args.Replace("{dualPassword}", prms["dualPassword"]);
                args = args.Replace("{dualHost}", prms["dualHost"]);
                args = args.Replace("{dualPort}", prms["dualPort"]);
                args = args.Replace("{dualPool}", prms["dualPool"]);
            }
            // 这里不要考虑{logfile}，{logfile}往后推迟
        }

        public void BuildFragments(ICoinKernel coinKernel, Dictionary<string, string> parameters, out Dictionary<Guid, string> fileWriters, out Dictionary<Guid, string> fragments) {
            fileWriters = new Dictionary<Guid, string>();
            fragments = new Dictionary<Guid, string>();
            try {
                foreach (var writerId in coinKernel.FragmentWriterIds) {
                    if (FragmentWriterSet.TryGetFragmentWriter(writerId, out IFragmentWriter writer)) {
                        BuildFragment(parameters, fileWriters, fragments, writer);
                    }
                }
                foreach (var writerId in coinKernel.FileWriterIds) {
                    if (FileWriterSet.TryGetFileWriter(writerId, out IFileWriter writer)) {
                        BuildFragment(parameters, fileWriters, fragments, writer);
                    }
                }
            }
            catch (Exception e) {
                Logger.ErrorDebugLine(e);
            }
        }

        public class ParameterNames {
            // 根据这个判断是否换成过期
            internal string Body = string.Empty;
            internal readonly HashSet<string> Names = new HashSet<string>();
        }

        private static readonly Dictionary<Guid, ParameterNames> _parameterNameDic = new Dictionary<Guid, ParameterNames>();
        private static readonly object _locker = new object();
        private static ParameterNames GetParameterNames(IFragmentWriter writer) {
            if (string.IsNullOrEmpty(writer.Body)) {
                return new ParameterNames {
                    Body = writer.Body
                };
            }
            Guid writerId = writer.GetId();
            if (_parameterNameDic.TryGetValue(writerId, out ParameterNames parameterNames) && parameterNames.Body == writer.Body) {
                return parameterNames;
            }
            else {
                lock (_locker) {
                    if (_parameterNameDic.TryGetValue(writerId, out parameterNames) && parameterNames.Body == writer.Body) {
                        return parameterNames;
                    }
                    if (parameterNames != null) {
                        parameterNames.Body = writer.Body;
                    }
                    else {
                        parameterNames = new ParameterNames {
                            Body = writer.Body
                        };
                        _parameterNameDic.Add(writerId, parameterNames);
                    }
                    parameterNames.Names.Clear();
                    const string pattern = @"\{(\w+)\}";
                    var matches = Regex.Matches(writer.Body, pattern);
                    foreach (Match match in matches) {
                        parameterNames.Names.Add(match.Groups[1].Value);
                    }
                    return parameterNames;
                }                
            }
        }

        private static bool IsMatch(IFragmentWriter writer, Dictionary<string, string> parameters, out ParameterNames parameterNames) {
            parameterNames = GetParameterNames(writer);
            if (string.IsNullOrEmpty(writer.Body)) {
                return false;
            }
            if (parameterNames.Names.Count == 0) {
                return true;
            }
            foreach (var name in parameterNames.Names) {
                if (!parameters.ContainsKey(name)) {
                    return false;
                }
            }
            return true;
        }

        private static void BuildFragment(Dictionary<string, string> parameters, Dictionary<Guid, string> fileWriters, Dictionary<Guid, string> fragments, IFragmentWriter writer) {
            try {
                if (!IsMatch(writer, parameters, out ParameterNames parameterNames)) {
                    return;
                }
                string content = writer.Body;
                foreach (var parameterName in parameterNames.Names) {
                    content = content.Replace($"{{{parameterName}}}", parameters[parameterName]);
                }
                if (writer is IFileWriter) {
                    if (fileWriters.ContainsKey(writer.GetId())) {
                        fileWriters[writer.GetId()] = content;
                    }
                    else {
                        fileWriters.Add(writer.GetId(), content);
                    }
                }
                else {
                    if (fragments.ContainsKey(writer.GetId())) {
                        fragments[writer.GetId()] = content;
                    }
                    else {
                        fragments.Add(writer.GetId(), content);
                    }
                }
            }
            catch (Exception e) {
                Logger.ErrorDebugLine(e);
            }
        }
    }
}
