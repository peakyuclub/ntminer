﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NTMiner {
    public static partial class Server {
        public static readonly ControlCenterServiceFace ControlCenterService = ControlCenterServiceFace.Instance;
        public static readonly AppSettingServiceFace AppSettingService = AppSettingServiceFace.Instance;
        public static readonly ReportServiceFace ReportService = ReportServiceFace.Instance;
        public static readonly WrapperMinerClientServiceFace MinerClientService = WrapperMinerClientServiceFace.Instance;

        private static void PostAsync<T>(string controller, string action, object param, Action<T, Exception> callback) where T : class {
            Task.Factory.StartNew(() => {
                try {
                    string serverHost = Registry.GetControlCenterHost();
                    using (HttpClient client = new HttpClient()) {
                        Task<HttpResponseMessage> message =
                            client.PostAsJsonAsync($"http://{serverHost}:{Consts.ControlCenterPort}/api/{controller}/{action}", param);
                        T response = message.Result.Content.ReadAsAsync<T>().Result;
                        callback?.Invoke(response, null);
                    }
                }
                catch (Exception e) {
                    callback?.Invoke(null, e);
                }
            });
        }

        private static T Post<T>(string controller, string action, object param, int? timeout = null) where T : class {
            try {
                string serverHost = Registry.GetControlCenterHost();
                using (HttpClient client = new HttpClient()) {
                    if (timeout.HasValue) {
                        client.Timeout = TimeSpan.FromMilliseconds(timeout.Value);
                    }
                    Task<HttpResponseMessage> message = client.PostAsJsonAsync($"http://{serverHost}:{Consts.ControlCenterPort}/api/{controller}/{action}", param);
                    T response = message.Result.Content.ReadAsAsync<T>().Result;
                    return response;
                }
            }
            catch (Exception e) {
                Logger.ErrorDebugLine(e);
                return null;
            }
        }

        private static void GetAsync<T>(string controller, string action, Dictionary<string, string> param, Action<T, Exception> callback) {
            Task.Factory.StartNew(() => {
                try {
                    string serverHost = Registry.GetControlCenterHost();
                    using (HttpClient client = new HttpClient()) {
                        string queryString = string.Empty;
                        if (param != null && param.Count != 0) {
                            queryString = "?" + string.Join("&", param.Select(a => a.Key + "=" + a.Value));
                        }

                        Task<HttpResponseMessage> message =
                            client.GetAsync($"http://{serverHost}:{Consts.ControlCenterPort}/api/{controller}/{action}{queryString}");
                        T response = message.Result.Content.ReadAsAsync<T>().Result;
                        callback?.Invoke(response, null);
                    }
                }
                catch (Exception e) {
                    callback?.Invoke(default(T), e);
                }
            });
        }
    }
}
