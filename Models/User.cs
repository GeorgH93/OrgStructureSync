using System;
using System.Collections.ObjectModel;

namespace OrgStructureSync
{
    class User : IIdAble
    {
        public string Name { get; }
        public Guid? ID { get; private set; }

        public ObservableCollection<Role> Roles { get; } = new ObservableCollection<Role>();

        public User(string name, Guid? id = null)
        {
            Name = name;
            ID = id;
        }

        public void RegisterId(Guid id)
        {
            ID = id;
        }

        public override string ToString()
        {
            return Name;
        }

        public bool AddRole(Role role)
        {
            if (Roles.Contains(role)) return false;
            Roles.Add(role);
            return true;
        }

        public bool RemoveRole(Role role)
        {
            return Roles.Remove(role);
        }
    }
}
