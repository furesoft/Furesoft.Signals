namespace Furesoft.Signals
{
    public class Signature
    {
        public string ReturnType { get; set; }
        public SignatureParameter[] Parameters { get; set; }
        public string Description { get; set; }
    }

    public class SignatureParameter
    {
        public bool IsOptional { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
    }
}