namespace Furesoft.Signals
{
    internal class FunctionCallRequest
    {
        public int ID { get; set; }
        public byte[][] ParameterRaw { get; set; }
        public object[] Parameter { get; set; }
    }
}