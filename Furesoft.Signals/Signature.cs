using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Furesoft.Signals
{
    public class Signature
    {
        public static Signature Empty => new Signature();
        public string Description { get; set; }
        public int ID { get; set; }
        public SignatureParameter[] Parameters { get; set; } = new SignatureParameter[0];
        public string ReturnType { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (Parameters.Any())
            {
                foreach (var p in Parameters)
                {
                    sb.Append(p.ToString());
                }
            }

            if (!string.IsNullOrEmpty(Description))
            {
                sb.AppendLine($"@Description {Description}");
            }

            sb.Append($"{ReturnType} 0x{ID.ToString("x").ToUpper()}(");

            var tmpParm = new List<string>();

            if (Parameters.Any())
            {
                foreach (var p in Parameters)
                {
                    tmpParm.Add($"{p.Type} {p.Name}");
                }
                sb.Append(string.Join(", ", tmpParm.ToArray()));
            }
            else
            {
                sb.Append("void");
            }

            sb.Append(")");

            return sb.ToString();
        }
    }

    public class SignatureParameter
    {
        public string Description { get; set; }
        public bool IsOptional { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            //@param name optional
            // - description
            //void pong(type name)
            if (IsOptional)
                sb.AppendLine($"@param {Name} [optional]");
            if (!string.IsNullOrEmpty(Description))
            {
                sb.AppendLine($"\t-{Description}");
            }

            return sb.ToString();
        }
    }
}