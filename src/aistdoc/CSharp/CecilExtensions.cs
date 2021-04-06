using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mono.Cecil
{
    public static class CecilExtensions
    {
        public static bool IsDelegate(this TypeDefinition type)
        {
            if (type.BaseType != null && type.BaseType.Namespace == "System")
            {
                if (type.BaseType.Name == "MulticastDelegate")
                    return true;
                if (type.BaseType.Name == "Delegate" && type.Name != "MulticastDelegate")
                    return true;
            }
            return false;
        }

        public static bool HasObsoleteAttribute(this ICustomAttributeProvider provider)
        {
            return provider.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Obsolete");
        }

        public static bool HasExtensionAttribute(this ICustomAttributeProvider provider)
        {
            return provider.CustomAttributes.Any(x => x.AttributeType.Name == "ExtensionAttribute"
            && x.AttributeType.Namespace == "System.Runtime.CompilerServices");
        }
    }
}
