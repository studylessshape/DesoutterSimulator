using System;

namespace DesoutterSimulator.Protocol
{
    public class Header
    {
        public int Length { get; set; }
        public int MID { get; set; }
        public int Revision { get; set; }
        public int NoAckFlag { get; set; }
        public int StationID { get; set; }
        public int SpindleID { get; set; }
        public string Spare { get; set; } = "";

        public Header()
        {
            Revision = 1;
            StationID = 1;
            SpindleID = 1;
            Spare = "    ";
        }

        public string ToMessageString()
        {
            return $"{Length:D4}{MID:D4}{Revision:D3}{NoAckFlag}{StationID:D2}{SpindleID:D2}{Spare}";
        }

        public static Header Parse(string data)
        {
            if (data.Length < 20)
                throw new ArgumentException("Header 长度不足 20 字节");

            int ParseIntOrDefault(string s, int defaultValue)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return defaultValue;
                if (int.TryParse(s, out int result))
                    return result;
                return defaultValue;
            }

            return new Header
            {
                Length = int.Parse(data.Substring(0, 4)),
                MID = int.Parse(data.Substring(4, 4)),
                Revision = ParseIntOrDefault(data.Substring(8, 3), 1),
                NoAckFlag = ParseIntOrDefault(data.Substring(11, 1), 0),
                StationID = ParseIntOrDefault(data.Substring(12, 2), 1),
                SpindleID = ParseIntOrDefault(data.Substring(14, 2), 1),
                Spare = data.Substring(16, 4)
            };
        }

        public override string ToString()
        {
            return $"MID={MID:D4}, Rev={Revision}, Len={Length}";
        }
    }
}