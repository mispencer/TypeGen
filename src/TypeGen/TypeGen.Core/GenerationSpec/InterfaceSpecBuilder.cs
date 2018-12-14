using System;
using TypeGen.Core.TypeAnnotations;

namespace TypeGen.Core.GenerationSpec
{
    public class InterfaceSpecBuilder<T> : ClassOrInterfaceSpecBuilder<T>
    {
        internal InterfaceSpecBuilder(TypeSpec spec) : base(spec)
        {
        }
        
        public InterfaceSpecBuilder<T> Member(Func<T, string> memberNameFunc)
        {
            return Member(memberNameFunc(_instance));
        }
        
        public InterfaceSpecBuilder<T> Member(string memberName)
        {
            SetActiveMember(memberName);
            return this;
        }
        
        public ClassOrInterfaceSpecBuilder<T> Optional()
        {
            AddActiveMemberAttribute(new TsOptionalAttribute());
            return this;
        }
    }
}