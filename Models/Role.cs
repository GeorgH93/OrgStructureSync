using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrgStructureSync
{
    class Role : IIdAble
    {
        public string Name { get; }
        public Guid? ID { get; private set; }
        
        public Role(string name, Guid? guid = null)
        {
            Name = name;
            ID = guid;
        }

        public void RegisterId(Guid id)
        {
            ID = id;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
