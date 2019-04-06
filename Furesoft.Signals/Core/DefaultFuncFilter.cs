using System.Reflection;
using Furesoft.Signals.Attributes;

namespace Furesoft.Signals.Core
{
    internal class DefaultFuncFilter : IFuncFilter
    {
        public object AfterCall(MethodInfo mi, int id, object returnValue)
        {
            return returnValue;
        }

        public FuncFilterResult BeforeCall(MethodInfo mi, int id)
        {
            return true;
        }
    }
}