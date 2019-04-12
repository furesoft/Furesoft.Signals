using System.Reflection;

namespace Furesoft.Signals.Attributes
{
    public interface IFuncFilter
    {
        FuncFilterResult BeforeCall(MethodInfo mi, int id);

        object AfterCall(MethodInfo mi, int id, object returnValue);
    }

    public class FuncFilterResult
    {
        public bool Success { get; set; }
        public Optional<string> ErrorMessage { get; set; } = false;

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