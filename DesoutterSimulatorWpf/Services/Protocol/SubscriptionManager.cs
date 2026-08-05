using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DesoutterSimulatorWpf.Services.Protocol
{
    public class SubscriptionManager
    {
        private readonly ConcurrentDictionary<string, Subscription> _subscriptions;

        public SubscriptionManager()
        {
            _subscriptions = new ConcurrentDictionary<string, Subscription>();
        }

        public bool HasSubscription(string type) => _subscriptions.ContainsKey(type);

        public void AddSubscription(string type, bool noAck)
        {
            _subscriptions[type] = new Subscription { Type = type, NoAck = noAck };
        }

        public void RemoveSubscription(string type)
        {
            _subscriptions.TryRemove(type, out _);
        }

        public Subscription GetSubscription(string type)
        {
            _subscriptions.TryGetValue(type, out var sub);
            return sub;
        }

        public bool IsNoAck(string type) => _subscriptions.TryGetValue(type, out var sub) && sub.NoAck;

        public void ClearAll() => _subscriptions.Clear();

        public IEnumerable<string> GetAllSubscriptionTypes() => _subscriptions.Keys;

        public class Subscription
        {
            public string Type { get; set; } = "";
            public bool NoAck { get; set; }
        }
    }
}