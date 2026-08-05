using System;

namespace DesoutterSimulator.Protocol
{
    public static class MessageParser
    {
        public static Message Parse(string messageStr)
        {
            if (string.IsNullOrEmpty(messageStr) || messageStr.Length < 20)
                throw new ArgumentException("消息长度不足");

            var header = Header.Parse(messageStr);
            var dataField = messageStr.Length > 20 ? messageStr.Substring(20) : "";

            return new Message
            {
                Header = header,
                DataField = dataField,
                MessageEnd = '\0'
            };
        }
    }
}