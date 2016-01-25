using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClepsCompiler.CompilerHelpers
{
    /// <summary>
    /// Defines the various properties of a cleps class
    /// </summary>
    class ClepsClass
    {
        public string FullyQualifiedName { get; private set; }
        public Dictionary<string, ClepsType> MemberVariables { get; private set; }
        public Dictionary<string, ClepsType> MemberMethods { get; private set; }

        public ClepsClass()
        {
            MemberVariables = new Dictionary<string, ClepsType>();
            MemberMethods = new Dictionary<string, ClepsType>();
        }

        public bool DoesClassContainMember(string memberName)
        {
            return MemberVariables.ContainsKey(memberName) || MemberMethods.ContainsKey(memberName);
        }

        public void AddNewMember(string memberName, ClepsType memberType)
        {
            bool memberNameExists = MemberVariables.ContainsKey(memberName) || MemberMethods.ContainsKey(memberName);
            Debug.Assert(!memberNameExists);

            if (memberType.IsBasicType)
            {
                MemberVariables.Add(memberName, memberType);
            }
            else
            {
                MemberMethods.Add(memberName, memberType);
            }
        }
    }
}
