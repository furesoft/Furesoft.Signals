using System;

namespace Furesoft.Signals.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class NoSignatureAttribute : Attribute
    {
    }
}