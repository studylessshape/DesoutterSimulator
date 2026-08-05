using DesoutterSimulator.Core;
using DesoutterSimulator.Utils;
using System;
using System.Collections.Generic;

namespace DesoutterSimulator.Protocol
{
    public static class MessageFactory
    {
        // ============ 通信消息 ============
        public static Message CreateCommunicationStartAcknowledge(int revision = 1)
        {
            var data = revision switch
            {
                1 => "0001000103Airbag1",
                2 => "0001000103Airbag1         ACT",
                3 => "0001000103Airbag1         ACT 1.3.0    CVI3 V1.0.0  Tool V1.0.0",
                4 => "0001000103Airbag1         ACT 1.3.0    CVI3 V1.0.0  Tool V1.0.0  RBU-Type   1234567890",
                _ => "0001000103Airbag1"
            };
            return new Message(2, revision, data);
        }

        public static Message CreateCommandAccepted(int mid)
        {
            return new Message(5, 1, $"{mid:D4}");
        }

        public static Message CreateCommandError(int mid, int errorCode)
        {
            return new Message(4, 1, $"{mid:D4}{errorCode:D2}");
        }

        // ============ 参数集消息 ============
        public static Message CreateParameterSetIDUploadReply(int[] psetIds)
        {
            var count = psetIds.Length;
            var data = $"{count:D2}";
            foreach (var id in psetIds)
                data += $"{id:D3}";
            return new Message(11, 1, data);
        }

        public static Message CreateParameterSetDataUploadReply(ParameterSet pset, int revision = 1)
        {
            // 将浮点数转换为整数（乘以100）以便使用 D 格式
            long torqueMin = (long)(pset.TorqueMin * 100);
            long torqueMax = (long)(pset.TorqueMax * 100);
            long torqueTarget = (long)(pset.TorqueTarget * 100);
            long firstTarget = (long)(pset.FirstTarget * 100);
            long startFinalAngle = (long)(pset.StartFinalAngle * 100);

            var data = revision switch
            {
                1 => $"{pset.Id:D3}{pset.Name.PadRight(25)}1{pset.BatchSize:D2}{torqueMin:D6}{torqueMax:D6}{torqueTarget:D6}{pset.AngleMin:D5}{pset.AngleMax:D5}{pset.AngleTarget:D5}",
                2 => $"{pset.Id:D3}{pset.Name.PadRight(25)}1{pset.BatchSize:D2}{torqueMin:D6}{torqueMax:D6}{torqueTarget:D6}{pset.AngleMin:D5}{pset.AngleMax:D5}{pset.AngleTarget:D5}{firstTarget:D6}{startFinalAngle:D6}",
                _ => $"{pset.Id:D3}{pset.Name.PadRight(25)}1{pset.BatchSize:D2}{torqueMin:D6}{torqueMax:D6}{torqueTarget:D6}{pset.AngleMin:D5}{pset.AngleMax:D5}{pset.AngleTarget:D5}"
            };
            return new Message(13, revision, data);
        }

        public static Message CreateParameterSetSelected(ParameterSet pset)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd:HH:mm:ss");
            var data = $"{pset.Id:D3}{timestamp}";
            return new Message(15, 1, data);
        }

        // ============ 拧紧结果 ============
        public static Message CreateLastTighteningResult(TighteningResult result, int revision = 1)
        {
            var data = BuildTighteningData(result, revision);
            return new Message(61, revision, data);
        }

        public static Message CreateOldTighteningUploadReply(TighteningResult result, int revision = 1)
        {
            var data = BuildTighteningData(result, revision);
            return new Message(65, revision, data);
        }

        private static string BuildTighteningData(TighteningResult result, int revision)
        {
            // 时间戳固定19字符
            var timestamp = result.TimeStamp.ToString("yyyy-MM-dd:HH:mm:ss");
            var psetChangeTime = result.PsetChangeTime.ToString("yyyy-MM-dd:HH:mm:ss");

            // 浮点数转整数（乘以100）
            long torqueMin = (long)(result.TorqueMin * 100);
            long torqueMax = (long)(result.TorqueMax * 100);
            long torqueTarget = (long)(result.TorqueTarget * 100);
            long torque = (long)(result.Torque * 100);
            long selftapMin = (long)(result.SelftapMin * 100);
            long selftapMax = (long)(result.SelftapMax * 100);
            long selftapTorque = (long)(result.SelftapTorque * 100);
            long prevailMin = (long)(result.PrevailTorqueMin * 100);
            long prevailMax = (long)(result.PrevailTorqueMax * 100);
            long prevailTorque = (long)(result.PrevailTorque * 100);

            // 字符串字段精确长度
            string controllerName = "Airbag1".PadRight(25);          // 25字符
            string vin = result.VIN.PadRight(25);                    // 25字符
            string toolSerial = result.ToolSerialNumber.PadRight(14); // 14字符
            string strategyOptions = result.StrategyOptions.PadLeft(5, '0');   // 5位数字
            string tighteningErrors = result.TighteningErrors.PadLeft(10, '0'); // 10位数字

            if (revision == 7)
            {
                var sb = new System.Text.StringBuilder(365);
                // 每个字段的key是2位数字，值固定长度
                sb.Append("01").Append(result.CellId.ToString("D4"));
                sb.Append("02").Append(result.ChannelId.ToString("D2"));
                sb.Append("03").Append("Airbag1".PadRight(25));
                sb.Append("04").Append(result.VIN.PadRight(25));
                sb.Append("05").Append(result.JobId.ToString("D4"));
                sb.Append("06").Append(result.PsetId.ToString("D3"));
                sb.Append("07").Append(result.Strategy.ToString("D2"));
                sb.Append("08").Append(strategyOptions); // 已确保5位
                sb.Append("09").Append(result.BatchSize.ToString("D4"));
                sb.Append("10").Append(result.BatchCounter.ToString("D4"));
                sb.Append("11").Append(result.Status);
                sb.Append("12").Append(result.BatchStatus);
                sb.Append("13").Append(result.TorqueStatus);
                sb.Append("14").Append(result.AngleStatus);
                sb.Append("15").Append(result.RundownAngleStatus);
                sb.Append("16").Append(result.CurrentMonitoringStatus);
                sb.Append("17").Append(result.SelftapStatus);
                sb.Append("18").Append(result.PrevailTorqueStatus);
                sb.Append("19").Append(result.CompensateStatus);
                sb.Append("20").Append(tighteningErrors); // 已确保10位
                sb.Append("21").Append(torqueMin.ToString("D6"));
                sb.Append("22").Append(torqueMax.ToString("D6"));
                sb.Append("23").Append(torqueTarget.ToString("D6"));
                sb.Append("24").Append(torque.ToString("D6"));
                sb.Append("25").Append(result.AngleMin.ToString("D5"));
                sb.Append("26").Append(result.AngleMax.ToString("D5"));
                sb.Append("27").Append(result.AngleTarget.ToString("D5"));
                sb.Append("28").Append(result.Angle.ToString("D5"));
                sb.Append("29").Append(result.RundownAngleMin.ToString("D5"));
                sb.Append("30").Append(result.RundownAngleMax.ToString("D5"));
                sb.Append("31").Append(result.RundownAngle.ToString("D5"));
                sb.Append("32").Append(result.CurrentMonitoringMin.ToString("D3"));
                sb.Append("33").Append(result.CurrentMonitoringMax.ToString("D3"));
                sb.Append("34").Append(result.CurrentMonitoringValue.ToString("D3"));
                sb.Append("35").Append(selftapMin.ToString("D6"));
                sb.Append("36").Append(selftapMax.ToString("D6"));
                sb.Append("37").Append(selftapTorque.ToString("D6"));
                sb.Append("38").Append(prevailMin.ToString("D6"));
                sb.Append("39").Append(prevailMax.ToString("D6"));
                sb.Append("40").Append(prevailTorque.ToString("D6"));
                sb.Append("41").Append(result.TighteningId.ToString("D10"));
                sb.Append("42").Append(result.JobSequence.ToString("D5"));
                sb.Append("43").Append(result.SyncTighteningId.ToString("D5"));
                sb.Append("44").Append(toolSerial); // 已确保14位
                sb.Append("45").Append(timestamp); // 19位
                sb.Append("46").Append(psetChangeTime); // 19位

                string data = sb.ToString();
                // 记录长度
                Logger.Debug($"修订版7数据长度: {data.Length}, 期望365");
                if (data.Length != 365)
                {
                    Logger.Warning($"修订版7数据长度异常: {data.Length}, 将强制截断或补齐");
                    // 如果长度大于365，截断；小于365，补空格（但应检查原因）
                    if (data.Length > 365) data = data.Substring(0, 365);
                    else data = data.PadRight(365, ' ');
                }
                return data;
            }

            // ========== 标准修订版本1-6、998、999（固定字段格式） ==========
            return revision switch
            {
                1 => $"0001{result.ChannelId:D2}Airbag1              {result.VIN.PadRight(25)}{result.JobId:D2}{result.PsetId:D3}{result.BatchSize:D4}{result.BatchCounter:D4}{result.Status}{result.TorqueStatus}{result.AngleStatus}{torqueMin:D6}{torqueMax:D6}{torqueTarget:D6}{torque:D6}{result.AngleMin:D5}{result.AngleMax:D5}{result.AngleTarget:D5}{result.Angle:D5}{timestamp}{psetChangeTime}{result.BatchStatus}{result.TighteningId:D10}",
                2 => $"0001{result.ChannelId:D2}Airbag1              {result.VIN.PadRight(25)}{result.JobId:D4}{result.PsetId:D3}{result.Strategy:D2}{strategyOptions}{result.BatchSize:D4}{result.BatchCounter:D4}{result.Status}{result.BatchStatus}{result.TorqueStatus}{result.AngleStatus}{result.RundownAngleStatus}{result.CurrentMonitoringStatus}{result.SelftapStatus}{result.PrevailTorqueStatus}{result.CompensateStatus}{tighteningErrors}{torqueMin:D6}{torqueMax:D6}{torqueTarget:D6}{torque:D6}{result.AngleMin:D5}{result.AngleMax:D5}{result.AngleTarget:D5}{result.Angle:D5}{result.RundownAngleMin:D5}{result.RundownAngleMax:D5}{result.RundownAngle:D5}{result.CurrentMonitoringMin:D3}{result.CurrentMonitoringMax:D3}{result.CurrentMonitoringValue:D3}{selftapMin:D6}{selftapMax:D6}{selftapTorque:D6}{prevailMin:D6}{prevailMax:D6}{prevailTorque:D6}{result.TighteningId:D10}{result.JobSequence:D5}{result.SyncTighteningId:D5}{toolSerial}{timestamp}{psetChangeTime}",
                _ => $"0001{result.ChannelId:D2}Airbag1              {result.VIN.PadRight(25)}{result.JobId:D2}{result.PsetId:D3}{result.BatchSize:D4}{result.BatchCounter:D4}{result.Status}{result.TorqueStatus}{result.AngleStatus}{torqueMin:D6}{torqueMax:D6}{torqueTarget:D6}{torque:D6}{result.AngleMin:D5}{result.AngleMax:D5}{result.AngleTarget:D5}{result.Angle:D5}{timestamp}{psetChangeTime}{result.BatchStatus}{result.TighteningId:D10}"
            };
        }

        // ============ 报警 ============
        public static Message CreateAlarm(Alarm alarm)
        {
            var timestamp = alarm.TimeStamp.ToString("yyyy-MM-dd:HH:mm:ss");
            var data = $"{alarm.ErrorCode}{alarm.ControllerReady}{alarm.ToolReady}{timestamp}";
            return new Message(71, 1, data);
        }

        public static Message CreateAlarmStatus(Alarm alarm)
        {
            var timestamp = alarm.TimeStamp.ToString("yyyy-MM-dd:HH:mm:ss");
            var data = $"1{alarm.ErrorCode}{alarm.ControllerReady}{alarm.ToolReady}{timestamp}";
            return new Message(76, 1, data);
        }

        public static Message CreateAlarmAcknowledgedOnController(string errorCode)
        {
            return new Message(74, 1, errorCode);
        }

        // ============ 工具 ============
        public static Message CreateToolDataUploadReply(ToolData tool, int revision = 1)
        {
            long calibration = (long)(tool.CalibrationValue * 100);
            long maxTorque = (long)(tool.MaxTorque * 100);
            long gearRatio = (long)(tool.GearRatio * 100);
            long fullSpeed = (long)(tool.FullSpeed * 100);

            var data = revision switch
            {
                1 => $"{tool.SerialNumber.PadRight(14)}{tool.Tightenings:D10}{tool.LastCalibrationDate:yyyy-MM-dd:HH:mm:ss}{tool.ControllerSerialNumber}",
                2 => $"{tool.SerialNumber.PadRight(14)}{tool.Tightenings:D10}{tool.LastCalibrationDate:yyyy-MM-dd:HH:mm:ss}{tool.ControllerSerialNumber}{calibration:D6}",
                3 => $"{tool.SerialNumber.PadRight(14)}{tool.Tightenings:D10}{tool.LastCalibrationDate:yyyy-MM-dd:HH:mm:ss}{tool.ControllerSerialNumber}{calibration:D6}{tool.LastServiceDate:yyyy-MM-dd:HH:mm:ss}{tool.TighteningsSinceService:D10}{tool.ToolType:D2}{tool.MotorSize:D2}{tool.OpenEndData.PadLeft(3, '0')}{tool.SoftwareVersion.PadRight(19)}{maxTorque:D6}{gearRatio:D6}{fullSpeed:D6}",
                _ => $"{tool.SerialNumber.PadRight(14)}{tool.Tightenings:D10}{tool.LastCalibrationDate:yyyy-MM-dd:HH:mm:ss}{tool.ControllerSerialNumber}"
            };
            return new Message(41, revision, data);
        }

        // ============ Job ============
        public static Message CreateJobIDUploadReply(int[] jobIds, int revision = 1)
        {
            var data = revision switch
            {
                1 => $"{jobIds.Length:D2}{string.Join("", Array.ConvertAll(jobIds, id => id.ToString("D2")))}",
                2 => $"{jobIds.Length:D4}{string.Join("", Array.ConvertAll(jobIds, id => id.ToString("D4")))}",
                _ => $"{jobIds.Length:D2}{string.Join("", Array.ConvertAll(jobIds, id => id.ToString("D2")))}"
            };
            return new Message(31, revision, data);
        }

        public static Message CreateJobInfo(JobInfo jobInfo)
        {
            var timestamp = jobInfo.TimeStamp.ToString("yyyy-MM-dd:HH:mm:ss");
            var data = $"{jobInfo.JobId:D4}{jobInfo.Status}{jobInfo.BatchMode}{jobInfo.BatchSize:D4}{jobInfo.BatchCounter:D4}{timestamp}{jobInfo.CurrentStep:D3}{jobInfo.TotalSteps:D3}{jobInfo.StepType:D2}{jobInfo.TighteningStatus:D2}";
            return new Message(35, 4, data);
        }

        // ============ VIN ============
        public static Message CreateVehicleIDNumber(VINData vinData, int revision = 1)
        {
            var data = revision switch
            {
                1 => vinData.VIN.PadRight(25),
                2 => $"{vinData.VIN.PadRight(25)}{vinData.Part2.PadRight(25)}{vinData.Part3.PadRight(25)}{vinData.Part4.PadRight(25)}",
                _ => vinData.VIN.PadRight(25)
            };
            return new Message(52, revision, data);
        }

        // ============ 时间 ============
        public static Message CreateReadTimeUploadReply(DateTime time)
        {
            var data = time.ToString("yyyy-MM-dd:HH:mm:ss");
            return new Message(81, 1, data);
        }

        // ============ 多主轴 ============
        public static Message CreateMultiSpindleStatus(MultiSpindleStatus status)
        {
            var data = $"{status.NumberOfSpindles:D2}{status.SyncTighteningId:D5}";
            data += $"{status.CommonStatus}";
            foreach (var spindle in status.Spindles)
                data += $"{spindle.SpindleNumber:D2}{spindle.Status}";
            return new Message(91, 1, data);
        }

        public static Message CreateMultiSpindleResult(MultiSpindleResult result)
        {
            long torqueMin = (long)(result.TorqueMin * 100);
            long torqueMax = (long)(result.TorqueMax * 100);
            long torqueTarget = (long)(result.TorqueTarget * 100);

            var data = $"{result.NumberOfSpindles:D2}{result.VIN.PadRight(25)}{result.JobId:D2}{result.PsetId:D3}{result.BatchSize:D4}{result.BatchCounter:D4}{result.BatchStatus}{torqueMin:D6}{torqueMax:D6}{torqueTarget:D6}{result.AngleMin:D5}{result.AngleMax:D5}{result.AngleTarget:D5}{result.PsetChangeTime:yyyy-MM-dd:HH:mm:ss}{result.TimeStamp:yyyy-MM-dd:HH:mm:ss}{result.SyncTighteningId:D5}{result.SyncOverallStatus}";
            foreach (var spindle in result.Spindles)
            {
                long torque = (long)(spindle.Torque * 100);
                data += $"{spindle.SpindleNumber:D2}{spindle.ChannelId:D2}{spindle.Status}{spindle.TorqueStatus}{torque:D6}{spindle.AngleStatus}{spindle.Angle:D5}";
            }
            return new Message(101, 1, data);
        }

        public static Message CreateMultiSpindleStationData(MultiSpindleResult result)
        {
            var data = $"02{result.NumberOfSpindles:D2}01{result.SyncTighteningId:D10}01Station1{result.TimeStamp:yyyy-MM-dd:HH:mm:ss}01Pset1{result.SyncOverallStatus}{result.VIN.PadRight(40)}02";
            return new Message(106, 1, data);
        }

        public static Message CreateMultiSpindleBoltData(MultiSpindleResult result)
        {
            var data = $"02{result.NumberOfSpindles:D2}02{result.SyncTighteningId:D10}";
            foreach (var spindle in result.Spindles)
            {
                long torque = (long)(spindle.Torque * 100);
                data += $"{spindle.SpindleNumber:D2}{spindle.Status}{torque:D6}{spindle.Angle:D5}";
            }
            return new Message(107, 1, data);
        }
    }
}