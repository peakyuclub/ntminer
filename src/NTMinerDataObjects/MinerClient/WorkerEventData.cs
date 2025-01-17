﻿using System;

namespace NTMiner.MinerClient {
    public class WorkerEventData : IWorkerEvent, IDbEntity<Guid> {
        public WorkerEventData() { }

        public static WorkerEventData Create(IWorkerEvent data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }
            if (data is WorkerEventData result) {
                return result;
            }
            return new WorkerEventData {
                Id = data.TypeId,
                TypeId = data.TypeId,
                Description = data.Description,
                EventOn = data.EventOn
            };
        }

        public Guid GetId() {
            return this.Id;
        }

        public Guid Id { get; set; }

        public Guid TypeId { get; set; }

        public string Description { get; set; }

        public DateTime EventOn { get; set; }
    }
}
