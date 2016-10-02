using System;

namespace M2MqttAkkaNet
{
    public class PSConfig
    {
        public string BrokerUrl { get; }
        public string UserName { get; }
        public string Password { get; }
        public bool CleanSession { get; }
        public int StashCapacity { get; }
        public TimeSpan StashTimeToLive => TimeSpan.FromMinutes(1);

        private readonly TimeSpan reconnectDelayMin = TimeSpan.FromMilliseconds(10);
        private readonly TimeSpan reconnectDelayMax = TimeSpan.FromSeconds(30);
        private readonly int connectCountMax;

        public PSConfig(string brokerUrl, string userName="", string password="", 
            bool cleanSession = true, int stashCapacity = 8000)
        {            
            BrokerUrl = brokerUrl;
            UserName = userName;
            Password = password;
            CleanSession = cleanSession;
            StashCapacity = stashCapacity;
            connectCountMax = Convert.ToInt32(Math.Floor(Math.Log(reconnectDelayMax.Ticks / reconnectDelayMin.Ticks) / Math.Log(2)));            
        }

        public long ConnectDelay(int connectCount)
        {
            if (connectCount >= connectCountMax)
            {
                return reconnectDelayMax.Milliseconds;
            }
            return reconnectDelayMin.Milliseconds * (1L << connectCount);
        }

        // Source: http://stackoverflow.com/questions/10030070/isfinite-equivalent
        public bool IsFinite() => !double.IsInfinity(StashTimeToLive.Minutes) && !double.IsNaN(StashTimeToLive.Minutes);
    }
}


