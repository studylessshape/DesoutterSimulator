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
            var timestamp = result.TimeStamp.ToString("yyyy-MM-dd:HH:mm:ss");
            var psetChangeTime = result.PsetChangeTime.ToString("yyyy-MM-dd:HH:mm:ss");

            // 浮点数转整数（取绝对值确保无符号）
            long torqueMin = Math.Abs((long)(result.TorqueMin * 100));
            long torqueMax = Math.Abs((long)(result.TorqueMax * 100));
            long torqueTarget = Math.Abs((long)(result.TorqueTarget * 100));
            long torque = Math.Abs((long)(result.Torque * 100));
            long selftapMin = Math.Abs((long)(result.SelftapMin * 100));
            long selftapMax = Math.Abs((long)(result.SelftapMax * 100));
            long selftapTorque = Math.Abs((long)(result.SelftapTorque * 100));
            long prevailMin = Math.Abs((long)(result.PrevailTorqueMin * 100));
            long prevailMax = Math.Abs((long)(result.PrevailTorqueMax * 100));
            long prevailTorque = Math.Abs((long)(result.PrevailTorque * 100));

            // 辅助函数：确保字符串精确长度
            string PadField(string value, int length, bool padLeft = false, char padChar = ' ')
            {
                if (value == null) value = "";
                if (value.Length > length) value = value.Substring(0, length);
                return padLeft ? value.PadLeft(length, padChar) : value.PadRight(length, padChar);
            }

            // 固定长度字符串字段
            string controllerName = PadField("Airbag1", 25);
            string vin = PadField(result.VIN, 25);
            string toolSerial = PadField(result.ToolSerialNumber, 14);
            string strategyOptions = PadField(result.StrategyOptions, 5, true, '0');
            string tighteningErrors = PadField(result.TighteningErrors, 10, true, '0');
            string psetName = PadField(result.ParameterSetName, 25);
            string idPart2 = PadField(result.IdentifierResultPart2, 25);
            string idPart3 = PadField(result.IdentifierResultPart3, 25);
            string idPart4 = PadField(result.IdentifierResultPart4, 25);
            string customerErrorCode = PadField(result.CustomerTighteningErrorCode, 4, true, '0');
            string errorStatus2 = PadField(result.TighteningErrorStatus2, 10, true, '0');

            // ========== 修订版本 7：完整键值对（01~57） ==========
            if (revision == 7)
            {
                var sb = new System.Text.StringBuilder(700);

                // 01~46（修订版 2）
                sb.Append("01").AppendFormat("{0:D4}", result.CellId);
                sb.Append("02").AppendFormat("{0:D2}", result.ChannelId);
                sb.Append("03").Append(controllerName);
                sb.Append("04").Append(vin);
                sb.Append("05").AppendFormat("{0:D4}", result.JobId);
                sb.Append("06").AppendFormat("{0:D3}", result.PsetId);
                sb.Append("07").AppendFormat("{0:D2}", result.Strategy);
                sb.Append("08").Append(strategyOptions);
                sb.Append("09").AppendFormat("{0:D4}", result.BatchSize);
                sb.Append("10").AppendFormat("{0:D4}", result.BatchCounter);
                sb.Append("11").Append(result.Status);
                sb.Append("12").Append(result.BatchStatus);
                sb.Append("13").Append(result.TorqueStatus);
                sb.Append("14").Append(result.AngleStatus);
                sb.Append("15").Append(result.RundownAngleStatus);
                sb.Append("16").Append(result.CurrentMonitoringStatus);
                sb.Append("17").Append(result.SelftapStatus);
                sb.Append("18").Append(result.PrevailTorqueStatus);
                sb.Append("19").Append(result.CompensateStatus);
                sb.Append("20").Append(tighteningErrors);
                sb.Append("21").AppendFormat("{0:D6}", torqueMin);
                sb.Append("22").AppendFormat("{0:D6}", torqueMax);
                sb.Append("23").AppendFormat("{0:D6}", torqueTarget);
                sb.Append("24").AppendFormat("{0:D6}", torque);
                sb.Append("25").AppendFormat("{0:D5}", result.AngleMin);
                sb.Append("26").AppendFormat("{0:D5}", result.AngleMax);
                sb.Append("27").AppendFormat("{0:D5}", result.AngleTarget);
                sb.Append("28").AppendFormat("{0:D5}", result.Angle);
                sb.Append("29").AppendFormat("{0:D5}", result.RundownAngleMin);
                sb.Append("30").AppendFormat("{0:D5}", result.RundownAngleMax);
                sb.Append("31").AppendFormat("{0:D5}", result.RundownAngle);
                sb.Append("32").AppendFormat("{0:D3}", result.CurrentMonitoringMin);
                sb.Append("33").AppendFormat("{0:D3}", result.CurrentMonitoringMax);
                sb.Append("34").AppendFormat("{0:D3}", result.CurrentMonitoringValue);
                sb.Append("35").AppendFormat("{0:D6}", selftapMin);
                sb.Append("36").AppendFormat("{0:D6}", selftapMax);
                sb.Append("37").AppendFormat("{0:D6}", selftapTorque);
                sb.Append("38").AppendFormat("{0:D6}", prevailMin);
                sb.Append("39").AppendFormat("{0:D6}", prevailMax);
                sb.Append("40").AppendFormat("{0:D6}", prevailTorque);
                sb.Append("41").AppendFormat("{0:D10}", result.TighteningId);
                sb.Append("42").AppendFormat("{0:D5}", result.JobSequence);
                sb.Append("43").AppendFormat("{0:D5}", result.SyncTighteningId);
                sb.Append("44").Append(toolSerial);
                sb.Append("45").Append(timestamp);
                sb.Append("46").Append(psetChangeTime);

                // 47~49（修订版 3）
                sb.Append("47").Append(psetName);
                sb.Append("48").AppendFormat("{0:D1}", result.TorqueValuesUnit);
                sb.Append("49").AppendFormat("{0:D2}", result.ResultType);

                // 50~52（修订版 4）
                sb.Append("50").Append(idPart2);
                sb.Append("51").Append(idPart3);
                sb.Append("52").Append(idPart4);

                // 53（修订版 5）
                sb.Append("53").Append(customerErrorCode);

                // 54~55（修订版 6）使用绝对值
                long pvtComp = Math.Abs(result.PrevailTorqueCompensateValue);
                sb.Append("54").AppendFormat("{0:D6}", pvtComp);
                sb.Append("55").Append(errorStatus2);

                // 56~57（修订版 7）使用绝对值
                int compAngle = Math.Abs(result.CompensatedAngle);
                sb.Append("56").AppendFormat("{0:D7}", compAngle);
                int finalAngleDec = Math.Abs(result.FinalAngleDecimal);
                sb.Append("57").AppendFormat("{0:D7}", finalAngleDec);

                string data = sb.ToString();

                // 长度校验（期望值 = 每个字段 2 字节 key + 值长度）
                int[] lengths = { 4,2,25,25,4,3,2,5,4,4,1,1,1,1,1,1,1,1,1,10,
                          6,6,6,6,5,5,5,5,5,5,5,3,3,3,6,6,6,6,6,6,
                          10,5,5,14,19,19,   // 01~46
                          25,1,2,           // 47~49
                          25,25,25,         // 50~52
                          4,                // 53
                          6,10,             // 54~55
                          7,7 };            // 56~57
                int expected = 0;
                for (int i = 0; i < lengths.Length; i++) expected += 2 + lengths[i];
                if (data.Length != expected)
                    Logger.Warning($"修订版7数据长度异常：期望{expected}，实际{data.Length}");

                return data;
            }

            // ========== 标准修订版本 1~6、998、999 ==========
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