
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QueueHolder", "Jake_Rich", 1.0)]
    [Description("Saves your position in queue if you disconnect")]

    public class QueueHolder : RustPlugin
    {
        public static QueueHolder _plugin { get; set; }

        public Dictionary<ulong, QueueData> queueData { get; set; } = new Dictionary<ulong, QueueData>();

        public const float timeToHoldSpot = 5; //Minutes

        public class QueueData
        {
            public ulong userID { get; set; }
            public DateTime disconnectTime { get; set; } = new DateTime();
            public int lastQueuePosition = -1;

            public QueueData()
            {

            }

            public QueueData(ulong id)
            {
                userID = id;
            }
        }

        void Loaded()
        {
            _plugin = this;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var data = GetQueueData(player);
            data.disconnectTime = DateTime.Now;
        }

        public QueueData GetQueueData(BasePlayer player)
        {
            return GetQueueData(player.userID);
        }

        public QueueData GetQueueData(ulong player)
        {
            QueueData data;
            if (!queueData.TryGetValue(player, out data))
            {
                data = new QueueData(player);
                queueData[player] = data;
            }
            return data;
        }

        object CanBypassQueue(Network.Connection connection)
        {
            if (connection.authLevel > 0)
            {
                return true;
            }
            var data = GetQueueData(connection.userid);
            if (DateTime.Now.Subtract(data.disconnectTime).TotalSeconds < timeToHoldSpot * 60f)
            {
                return true;
            }
            return null;
        }
    }
}


