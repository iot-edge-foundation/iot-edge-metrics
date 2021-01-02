namespace IoTEdgeMetricsModule
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Metrics
    {
        public Metrics()
        {
            Timestamp = DateTime.UtcNow;
            Disks = new Dictionary<string, Disk>();
            Modules = new Dictionary<string, Module>();
        }

        public DateTime Timestamp { get; set; }

        public int Uptime { get; set; }

        public Dictionary<string, Disk> Disks {get; private set;}

        public Dictionary<string, Module> Modules {get; private set;}

        public int MessagesSentCount 
        { 
            get
            {
                return Modules.Sum(x => x.Value.MessagesSentCount);
            } 
        }

        public int MessagesRecievedCount 
        { 
            get
            {
                return Modules.Sum(x => x.Value.MessagesRecievedCount);
            } 
        }       
    }
}
