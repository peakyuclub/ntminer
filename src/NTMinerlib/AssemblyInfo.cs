﻿namespace NTMiner {
    public static class AssemblyInfo {
        public const string VERSION = "1.1.3";
        public const string COPYRIGHT = "Copyright ©  2019";
        public const string Tag = "谜麟";
        public static readonly string ServerJsonFileName = $"server{VERSION}.json";
        public static readonly string ServerLangJsonFileName = $"serverLang{VERSION}.json";

        public static string LocalJsonFileNameETag;
        public static string LocalLangJsonFileNameETag;
        public static readonly string ServerJsonFileUrl = "https://minerjson.oss-cn-beijing.aliyuncs.com/" + ServerJsonFileName;
        public static readonly string ServerLangJsonFileUrl = "https://minerjson.oss-cn-beijing.aliyuncs.com/" + ServerLangJsonFileName;
    }
}
