using System.Text;

namespace DesoutterSimulator.Protocol
{
    public class Message
    {
        public Header Header { get; set; }
        public string DataField { get; set; } = "";
        public char MessageEnd { get; set; } = '\0';

        public int MID => Header?.MID ?? 0;
        public int Revision => Header?.Revision ?? 1;
        public int NoAckFlag => Header?.NoAckFlag ?? 0;

        public Message()
        {
            Header = new Header();
        }

        public Message(int mid, int revision = 1, string dataField = "")
        {
            Header = new Header
            {
                MID = mid,
                Revision = revision
            };
            DataField = dataField;
            UpdateLength();
        }

        public void UpdateLength()
        {
            Header.Length = 20 + DataField.Length;
        }

        public byte[] ToByteArray()
        {
            UpdateLength();
            var msg = Header.ToMessageString() + DataField + MessageEnd;
            return Encoding.ASCII.GetBytes(msg);
        }

        public override string ToString()
        {
            return $"MID {Header.MID:D4}, Rev {Header.Revision}, Len {Header.Length}, Data: {(string.IsNullOrEmpty(DataField) ? "empty" : DataField)}";
        }
    }
}