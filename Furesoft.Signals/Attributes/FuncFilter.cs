using System.Reflection;

namespace Furesoft.Signals.Attributes
{
    public interface IFuncFilter
    {
        object AfterCall(MethodInfo mi, int id, object returnValue);

        FuncFilterResult BeforeCall(MethodInfo mi, int id);
    }

    public class FuncFilterResult
    {
        public Optional<string> ErrorMessage { get; set; } = false;
        public bool Success { get; set; }

        public static implicit operator bool(FuncFilterResult r)
        {
            return r.Success;
        }

        public static implicit operator FuncFilterResult(bool r)
        {
            return new FuncFilterResult { Success = r };
        }

        public static implicit operator FuncFilterResult(string r)
        {
            return new FuncFilterResult { Success = false, ErrorMessage = r.ToOptional() };
        }
    }
}