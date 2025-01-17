﻿using System;

namespace NTMiner.MinerClient {
    public interface IWorkerEvent : IEntity<Guid> {
        Guid TypeId { get; }
        string Description { get; }
        DateTime EventOn { get; }
    }
}
