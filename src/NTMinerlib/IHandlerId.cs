﻿using System;

namespace NTMiner {
    public interface IHandlerId {
        Type MessageType { get; }
        bool IsEnabled { get; set; }
        Type Location { get; }
        string HandlerPath { get; }
        LogEnum LogType { get; }
        string Description { get; }
    }
}
