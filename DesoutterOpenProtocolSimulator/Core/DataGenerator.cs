using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DesoutterSimulator.Core
{
    public class DataGenerator
    {
        private static readonly ConcurrentDictionary<long, TighteningResult> _results = new();
        private static long _currentId = 1000;
        private readonly Random _random = Random.Shared;
        private readonly Timer _simulationTimer;

        public event EventHandler<TighteningResult> TighteningGenerated;
        public event EventHandler<Alarm> AlarmGenerated;

        public DataGenerator()
        {
            _simulationTimer = new Timer(GenerateSimulationData, null, 5000, 5000);
        }

        public void Start() => _simulationTimer.Change(1000, 5000);
        public void Stop() => _simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);

        private void GenerateSimulationData(object state)
        {
            var result = GenerateRandomTighteningResult();
            _results[result.TighteningId] = result;
            TighteningGenerated?.Invoke(this, result);

            // 随机生成报警
            if (_random.Next(100) < 5)
            {
                var alarm = new Alarm
                {
                    ErrorCode = $"E{_random.Next(100, 999):D3}",
                    ControllerReady = _random.Next(2),
                    ToolReady = _random.Next(2),
                    TimeStamp = DateTime.Now
                };
                AlarmGenerated?.Invoke(this, alarm);
            }
        }

        private TighteningResult GenerateRandomTighteningResult()
        {
            var id = Interlocked.Increment(ref _currentId);
            var isOk = _random.Next(100) < 90;

            return new TighteningResult
            {
                TighteningId = id,
                VIN = $"VIN_{_random.Next(100000):D6}",
                JobId = _random.Next(1, 6),
                PsetId = _random.Next(1, 11),
                ChannelId = 1,
                BatchSize = 10,
                BatchCounter = _random.Next(1, 11),
                Status = isOk ? 1 : 0,
                BatchStatus = isOk ? 1 : 0,
                TorqueStatus = isOk ? 1 : _random.Next(3),
                AngleStatus = isOk ? 1 : _random.Next(3),
                TorqueMin = 5.0,
                TorqueMax = 15.0,
                TorqueTarget = 10.0,
                Torque = 10.0 + (_random.NextDouble() - 0.5) * 4.0,
                AngleMin = 0,
                AngleMax = 360,
                AngleTarget = 180,
                Angle = 180 + _random.Next(-30, 30),
                TimeStamp = DateTime.Now,
                PsetChangeTime = DateTime.Now.AddDays(-_random.Next(1, 30)),
                Strategy = 2,
                StrategyOptions = "00000",
                RundownAngleStatus = isOk ? 1 : _random.Next(3),
                CurrentMonitoringStatus = 1,
                SelftapStatus = 1,
                PrevailTorqueStatus = 1,
                CompensateStatus = 1,
                TighteningErrors = "0000000000",
                RundownAngleMin = 0,
                RundownAngleMax = 100,
                RundownAngle = 50 + _random.Next(-10, 10),
                CurrentMonitoringMin = 80,
                CurrentMonitoringMax = 120,
                CurrentMonitoringValue = 100 + _random.Next(-15, 15),
                SelftapMin = 1.0,
                SelftapMax = 5.0,
                SelftapTorque = 3.0 + (_random.NextDouble() - 0.5) * 2.0,
                PrevailTorqueMin = 1.0,
                PrevailTorqueMax = 5.0,
                PrevailTorque = 3.0 + (_random.NextDouble() - 0.5) * 2.0,
                JobSequence = _random.Next(10000, 99999),
                SyncTighteningId = _random.Next(1, 99999),
                ToolSerialNumber = $"C{_random.NextInt64(1000000000000, 9999999999999):D13}", // .NET 6+,
                CellId = 1,
                ParameterSetName = $"Pset_{_random.Next(1, 11):D3}",
            };
        }

        public static TighteningResult GetTighteningResult(long id)
        {
            _results.TryGetValue(id, out var result);
            return result;
        }

        public static TighteningResult GetLatestTighteningResult()
        {
            long maxId = 0;
            TighteningResult latest = null;
            foreach (var kvp in _results)
            {
                if (kvp.Key > maxId)
                {
                    maxId = kvp.Key;
                    latest = kvp.Value;
                }
            }
            return latest;
        }

        public void ClearOldResults(int keepCount = 100)
        {
            var ids = new List<long>(_results.Keys);
            ids.Sort();
            while (ids.Count > keepCount)
            {
                _results.TryRemove(ids[0], out _);
                ids.RemoveAt(0);
            }
        }
    }
}