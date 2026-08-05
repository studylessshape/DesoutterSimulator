using System;
using System.Collections.Generic;
using System.Linq;

namespace DesoutterSimulator.Core
{
    public class ParameterSet
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int BatchSize { get; set; } = 1;
        public double TorqueMin { get; set; } = 5.0;
        public double TorqueMax { get; set; } = 15.0;
        public double TorqueTarget { get; set; } = 10.0;
        public int AngleMin { get; set; } = 0;
        public int AngleMax { get; set; } = 360;
        public int AngleTarget { get; set; } = 180;
        public double FirstTarget { get; set; } = 8.0;
        public double StartFinalAngle { get; set; } = 2.0;
    }

    public class ToolData
    {
        public string SerialNumber { get; set; } = "C341212025487";
        public long Tightenings { get; set; } = 9603200;
        public DateTime LastCalibrationDate { get; set; } = DateTime.Now.AddDays(-30);
        public string ControllerSerialNumber { get; set; } = "04670919";
        public double CalibrationValue { get; set; } = 100.0;
        public DateTime LastServiceDate { get; set; } = DateTime.Now.AddDays(-90);
        public long TighteningsSinceService { get; set; } = 50000;
        public int ToolType { get; set; } = 1;
        public int MotorSize { get; set; } = 25;
        public string OpenEndData { get; set; } = "000";
        public string SoftwareVersion { get; set; } = "V1.0.0";
        public double MaxTorque { get; set; } = 50.0;
        public double GearRatio { get; set; } = 1.0;
        public double FullSpeed { get; set; } = 1000.0;
    }

    public class JobInfo
    {
        public int JobId { get; set; }
        public int Status { get; set; } // 0=not completed, 1=OK, 2=NOK, 3=ABORTED
        public int BatchMode { get; set; }
        public int BatchSize { get; set; }
        public int BatchCounter { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.Now;
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public int StepType { get; set; }
        public int TighteningStatus { get; set; }
    }

    public class VINData
    {
        public string VIN { get; set; } = "KPOL3456JKLO897";
        public string Part2 { get; set; } = "";
        public string Part3 { get; set; } = "";
        public string Part4 { get; set; } = "";
    }

    public class Alarm
    {
        public string ErrorCode { get; set; } = "E404";
        public int ControllerReady { get; set; } = 1;
        public int ToolReady { get; set; } = 1;
        public DateTime TimeStamp { get; set; } = DateTime.Now;
    }

    public class MultiSpindleStatus
    {
        public int NumberOfSpindles { get; set; } = 2;
        public int SyncTighteningId { get; set; } = 1;
        public int CommonStatus { get; set; }
        public List<SpindleStatusItem> Spindles { get; set; } = new();
    }

    public class SpindleStatusItem
    {
        public int SpindleNumber { get; set; }
        public int Status { get; set; }
    }

    public class MultiSpindleResult
    {
        public int NumberOfSpindles { get; set; }
        public string VIN { get; set; } = "";
        public int JobId { get; set; }
        public int PsetId { get; set; }
        public int BatchSize { get; set; }
        public int BatchCounter { get; set; }
        public int BatchStatus { get; set; }
        public double TorqueMin { get; set; }
        public double TorqueMax { get; set; }
        public double TorqueTarget { get; set; }
        public int AngleMin { get; set; }
        public int AngleMax { get; set; }
        public int AngleTarget { get; set; }
        public DateTime PsetChangeTime { get; set; }
        public DateTime TimeStamp { get; set; }
        public int SyncTighteningId { get; set; }
        public int SyncOverallStatus { get; set; }
        public List<MultiSpindleResultSpindle> Spindles { get; set; } = new();
    }

    public class MultiSpindleResultSpindle
    {
        public int SpindleNumber { get; set; }
        public int ChannelId { get; set; }
        public int Status { get; set; }
        public int TorqueStatus { get; set; }
        public double Torque { get; set; }
        public int AngleStatus { get; set; }
        public int Angle { get; set; }
    }

    public class TighteningResult
    {
        public long TighteningId { get; set; }
        public string VIN { get; set; } = "KPOL3456JKLO897";
        public int JobId { get; set; } = 1;
        public int PsetId { get; set; } = 1;
        public int ChannelId { get; set; } = 1;
        public int BatchSize { get; set; } = 10;
        public int BatchCounter { get; set; } = 1;
        public int Status { get; set; } = 1; // 0=NOK, 1=OK
        public int BatchStatus { get; set; } = 1;
        public int TorqueStatus { get; set; } = 1;
        public int AngleStatus { get; set; } = 1;
        public double TorqueMin { get; set; } = 5.0;
        public double TorqueMax { get; set; } = 15.0;
        public double TorqueTarget { get; set; } = 10.0;
        public double Torque { get; set; } = 10.5;
        public int AngleMin { get; set; } = 0;
        public int AngleMax { get; set; } = 360;
        public int AngleTarget { get; set; } = 180;
        public int Angle { get; set; } = 185;
        public DateTime TimeStamp { get; set; } = DateTime.Now;
        public DateTime PsetChangeTime { get; set; } = DateTime.Now.AddDays(-1);
        public int Strategy { get; set; } = 2;
        public string StrategyOptions { get; set; } = "00000";
        public int RundownAngleStatus { get; set; } = 1;
        public int CurrentMonitoringStatus { get; set; } = 1;
        public int SelftapStatus { get; set; } = 1;
        public int PrevailTorqueStatus { get; set; } = 1;
        public int CompensateStatus { get; set; } = 1;
        public string TighteningErrors { get; set; } = "0000000000";
        public int RundownAngleMin { get; set; } = 0;
        public int RundownAngleMax { get; set; } = 100;
        public int RundownAngle { get; set; } = 50;
        public int CurrentMonitoringMin { get; set; } = 80;
        public int CurrentMonitoringMax { get; set; } = 120;
        public int CurrentMonitoringValue { get; set; } = 100;
        public double SelftapMin { get; set; } = 1.0;
        public double SelftapMax { get; set; } = 5.0;
        public double SelftapTorque { get; set; } = 3.0;
        public double PrevailTorqueMin { get; set; } = 1.0;
        public double PrevailTorqueMax { get; set; } = 5.0;
        public double PrevailTorque { get; set; } = 3.0;
        public int JobSequence { get; set; } = 12345;
        public int SyncTighteningId { get; set; } = 0;
        public string ToolSerialNumber { get; set; } = "C341212025487";
        public int CellId { get; set; } = 1;
    }

    public class ControllerState
    {
        public bool CommunicationStarted { get; set; }
        public bool ToolEnabled { get; set; } = true;
        public int CurrentParameterSetId { get; set; } = 1;
        public int CurrentJobId { get; set; } = 0;
        public bool JobRunning { get; set; }
        public bool JobOffMode { get; set; }

        private readonly Dictionary<int, ParameterSet> _parameterSets;
        private readonly Dictionary<int, JobInfo> _jobs;
        private Alarm _activeAlarm;

        public ControllerState()
        {
            _parameterSets = new Dictionary<int, ParameterSet>();
            _jobs = new Dictionary<int, JobInfo>();
            InitializeDefaultData();
        }

        private void InitializeDefaultData()
        {
            for (int i = 1; i <= 10; i++)
            {
                _parameterSets[i] = new ParameterSet
                {
                    Id = i,
                    Name = $"Pset_{i:D3}",
                    BatchSize = 10,
                    TorqueMin = 5.0 + (i - 1) * 0.5,
                    TorqueMax = 15.0 + (i - 1) * 0.5,
                    TorqueTarget = 10.0 + (i - 1) * 0.5
                };
            }

            for (int i = 1; i <= 5; i++)
            {
                _jobs[i] = new JobInfo
                {
                    JobId = i,
                    BatchSize = 10 + i * 2,
                    BatchMode = 0,
                    Status = 0,
                    CurrentStep = 0,
                    TotalSteps = 5,
                    StepType = 0,
                    TighteningStatus = 0
                };
            }
        }

        public int[] GetParameterSetIDs() => _parameterSets.Keys.ToArray();
        public ParameterSet GetParameterSet(int id) => _parameterSets.GetValueOrDefault(id);
        public ParameterSet GetCurrentParameterSet() => GetParameterSet(CurrentParameterSetId);
        public bool SelectParameterSet(int id)
        {
            if (!_parameterSets.ContainsKey(id)) return false;
            CurrentParameterSetId = id;
            return true;
        }

        public int[] GetJobIDs() => _jobs.Keys.ToArray();
        public bool SelectJob(int id)
        {
            if (!_jobs.ContainsKey(id)) return false;
            CurrentJobId = id;
            JobRunning = true;
            return true;
        }
        public JobInfo GetCurrentJobInfo()
        {
            if (_jobs.TryGetValue(CurrentJobId, out var job))
                return job;
            return new JobInfo { JobId = 0, Status = 0 };
        }

        public ToolData GetToolData() => new();
        public VINData GetVINData() => new();

        public bool HasActiveAlarm() => _activeAlarm != null;
        public Alarm GetActiveAlarm() => _activeAlarm;
        public void SetAlarm(Alarm alarm) => _activeAlarm = alarm;
        public void AcknowledgeAlarm() => _activeAlarm = null;
    }
}