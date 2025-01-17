﻿using NTMiner.Bus;
using NTMiner.Bus.DirectBus;
using NTMiner.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Timers;

namespace NTMiner {
    public static partial class VirtualRoot {
        public static readonly Stopwatch Stopwatch = new Stopwatch();

        public static readonly string AppFileFullName = Process.GetCurrentProcess().MainModule.FileName;
        public static Guid Id { get; private set; }
        public static string GlobalDirFullName { get; set; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NTMiner");

        public static bool IsMinerStudio { get; private set; }

        public static void SetIsMinerStudio(bool value) {
            IsMinerStudio = value;
        }

        public static IObjectSerializer JsonSerializer { get; private set; }

        public static readonly IMessageDispatcher SMessageDispatcher;
        private static readonly ICmdBus SCommandBus;
        private static readonly IEventBus SEventBus;

        public static int _secondCount = 0;
        public static int SecondCount {
            get {
                return _secondCount;
            }
        }
        static VirtualRoot() {
            Id = Registry.GetClientId();
            if (!Directory.Exists(GlobalDirFullName)) {
                Directory.CreateDirectory(GlobalDirFullName);
            }
            JsonSerializer = new ObjectJsonSerializer();
            SMessageDispatcher = new MessageDispatcher();
            SCommandBus = new DirectCommandBus(SMessageDispatcher);
            SEventBus = new DirectEventBus(SMessageDispatcher);
        }

        private static Timer _timer;
        public static void StartTimer() {
            if (_timer != null) {
                return;
            }
            _timer = new Timer(1000);
            _timer.Elapsed += (object sender, ElapsedEventArgs e) => {
                Elapsed();
            };
            _timer.Start();
        }

        public static void StopTimer() {
            if (_timer != null) {
                _timer.Stop();
                _timer = null;
            }
        }

        public static void Elapsed() {
            _secondCount++;
            const int daySecond = 24 * 60 * 60;
            #region one
            if (_secondCount <= 20) {
                if (_secondCount == 1) {
                    Happened(new HasBoot1SecondEvent());
                }
                if (_secondCount == 2) {
                    Happened(new HasBoot2SecondEvent());
                }
                if (_secondCount == 5) {
                    Happened(new HasBoot5SecondEvent());
                }
                if (_secondCount == 10) {
                    Happened(new HasBoot10SecondEvent());
                }
                if (_secondCount == 20) {
                    Happened(new HasBoot20SecondEvent());
                }
            }
            else if (_secondCount <= 6000) {
                if (_secondCount == 60) {
                    Happened(new HasBoot1MinuteEvent());
                }
                if (_secondCount == 120) {
                    Happened(new HasBoot2MinuteEvent());
                }
                if (_secondCount == 300) {
                    Happened(new HasBoot5MinuteEvent());
                }
                if (_secondCount == 600) {
                    Happened(new HasBoot10MinuteEvent());
                }
                if (_secondCount == 1200) {
                    Happened(new HasBoot20MinuteEvent());
                }
                if (_secondCount == 3000) {
                    Happened(new HasBoot50MinuteEvent());
                }
                if (_secondCount == 6000) {
                    Happened(new HasBoot100MinuteEvent());
                }
            }
            else if (_secondCount <= daySecond) {
                if (_secondCount == daySecond) {
                    Happened(new HasBoot24HourEvent());
                }
            }
            #endregion

            #region per
            Happened(new Per1SecondEvent());
            if (_secondCount % 2 == 0) {
                Happened(new Per2SecondEvent());
            }
            if (_secondCount % 5 == 0) {
                Happened(new Per5SecondEvent());
            }
            if (_secondCount % 10 == 0) {
                Happened(new Per10SecondEvent());
            }
            if (_secondCount % 20 == 0) {
                Happened(new Per20SecondEvent());
            }
            if (_secondCount % 60 == 0) {
                Happened(new Per1MinuteEvent());
            }
            if (_secondCount % 120 == 0) {
                Happened(new Per2MinuteEvent());
            }
            if (_secondCount % 300 == 0) {
                Happened(new Per5MinuteEvent());
            }
            if (_secondCount % 600 == 0) {
                Happened(new Per10MinuteEvent());
            }
            if (_secondCount % 1200 == 0) {
                Happened(new Per20MinuteEvent());
            }
            if (_secondCount % 3000 == 0) {
                Happened(new Per50MinuteEvent());
            }
            if (_secondCount % 6000 == 0) {
                Happened(new Per100MinuteEvent());
            }
            if (_secondCount % daySecond == 0) {
                Happened(new Per24HourEvent());
            }
            #endregion
        }

        /// <summary>
        /// 发生某个事件
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="evnt"></param>
        public static void Happened<TEvent>(TEvent evnt) where TEvent : class, IEvent {
            SEventBus.Publish(evnt);
            SEventBus.Commit();
        }

        /// <summary>
        /// 执行某个命令
        /// </summary>
        /// <param name="command"></param>
        public static void Execute(ICmd command) {
            SCommandBus.Publish(command);
            SCommandBus.Commit();
        }

        // 修建消息（命令或事件）的运动路径
        public static DelegateHandler<TMessage> Path<TMessage>(string description, LogEnum logType, Action<TMessage> action) {
            StackTrace ss = new StackTrace(false);
            // 0是Path，1是Accpt或On，2是当地
            Type location = ss.GetFrame(2).GetMethod().DeclaringType;
            IHandlerId handlerId = HandlerId.Create(typeof(TMessage), location, description, logType);
            return SMessageDispatcher.Register(handlerId, action);
        }

        /// <summary>
        /// 命令窗口。使用该方法的代码行应将前两个参数放在第一行以方便vs查找引用时展示出参数信息
        /// </summary>
        public static DelegateHandler<TCmd> Window<TCmd>(LogEnum logType, Action<TCmd> action)
            where TCmd : ICmd {
            MessageTypeAttribute messageTypeDescription = MessageTypeAttribute.GetMessageTypeDescription(typeof(TCmd));
            string description = "处理" + messageTypeDescription.Description;
            return Path(description, logType, action);
        }

        /// <summary>
        /// 事件响应
        /// </summary>
        public static DelegateHandler<TEvent> On<TEvent>(LogEnum logType, Action<TEvent> action)
            where TEvent : IEvent {
            MessageTypeAttribute messageTypeDescription = MessageTypeAttribute.GetMessageTypeDescription(typeof(TEvent));
            string description = "处理" + messageTypeDescription.Description;
            return Path(description, logType, action);
        }

        /// <summary>
        /// 命令窗口。使用该方法的代码行应将前两个参数放在第一行以方便vs查找引用时展示出参数信息
        /// </summary>
        public static DelegateHandler<TCmd> Window<TCmd>(string description, LogEnum logType, Action<TCmd> action)
            where TCmd : ICmd {
            return Path(description, logType, action);
        }

        /// <summary>
        /// 事件响应
        /// </summary>
        public static DelegateHandler<TEvent> On<TEvent>(string description, LogEnum logType, Action<TEvent> action)
            where TEvent : IEvent {
            return Path(description, logType, action);
        }

        // 拆除消息（命令或事件）的运动路径
        public static void UnPath(IDelegateHandler handler) {
            if (handler == null) {
                return;
            }
            SMessageDispatcher.UnRegister(handler);
        }

        public static void TagBrandId(string brandKeyword, Guid brandId, string inputFileFullName, string outFileFullName) {
            string brand = $"{brandKeyword}{brandId}{brandKeyword}";
            string rawBrand = $"{brandKeyword}{GetBrandId(inputFileFullName, brandKeyword)}{brandKeyword}";
            byte[] data = Encoding.UTF8.GetBytes(brand);
            byte[] rawData = Encoding.UTF8.GetBytes(rawBrand);
            if (data.Length != rawData.Length) {
                throw new InvalidProgramException();
            }
            byte[] source = File.ReadAllBytes(inputFileFullName);
            int index = 0;
            for (int i = 0; i < source.Length - rawData.Length; i++) {
                int j = 0;
                for (; j < rawData.Length; j++) {
                    if (source[i + j] != rawData[j]) {
                        break;
                    }
                }
                if (j == rawData.Length) {
                    index = i;
                    break;
                }
            }
            for (int i = index; i < index + data.Length; i++) {
                source[i] = data[i - index];
            }
            File.WriteAllBytes(outFileFullName, source);
        }

        public static Guid GetBrandId(string fileFullName, string keyword) {
#if DEBUG
            Stopwatch.Restart();
#endif
            Guid guid = Guid.Empty;
            int LEN = keyword.Length;
            if (fileFullName == AppFileFullName) {
                Assembly assembly = Assembly.GetEntryAssembly();
                string name = $"NTMiner.Brand.{keyword}";
                using (var stream = assembly.GetManifestResourceStream(name)) {
                    if (stream == null) {
                        return guid;
                    }
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    string rawBrand = Encoding.UTF8.GetString(data);
                    string guidString = rawBrand.Substring(LEN, rawBrand.Length - 2 * LEN);
                    Guid.TryParse(guidString, out guid);
                }
            }
            else {
                string rawBrand = $"{keyword}{Guid.Empty}{keyword}";
                byte[] rawData = Encoding.UTF8.GetBytes(rawBrand);
                int len = rawData.Length;
                byte[] source = File.ReadAllBytes(fileFullName);
                int index = 0;
                for (int i = 0; i < source.Length - len; i++) {
                    int j = 0;
                    for (; j < len; j++) {
                        if ((j < LEN || j > len - LEN) && source[i + j] != rawData[j]) {
                            break;
                        }
                    }
                    if (j == rawData.Length) {
                        index = i;
                        break;
                    }
                }
                string guidString = Encoding.UTF8.GetString(source, index + LEN, len - 2 * LEN);
                Guid.TryParse(guidString, out guid);
            }
#if DEBUG
            Write.DevWarn($"耗时{Stopwatch.ElapsedMilliseconds}毫秒 {typeof(VirtualRoot).Name}.GetBrandId");
#endif
            return guid;
        }
    }
}
