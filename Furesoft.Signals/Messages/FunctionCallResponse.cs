namespace Furesoft.Signals.Messages
{
    internal class FunctionCallResponse
    {
        public int ID { get; set; }
        public byte[] ReturnValue { get; set; }

        public string ErrorMessage { get; set; }
    }
}