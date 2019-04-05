namespace Furesoft.Signals
{
    internal class FunctionCallRequest
    {
        public int ID { get; set; }
        public string[] ParameterJson { get; set; }
        public object[] Parameter { get; set; }
    }
}