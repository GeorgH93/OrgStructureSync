using System;

namespace OrgStructureSync
{
    interface IIdAble
    {
        Guid? ID { get; }

        void RegisterId(Guid id);
    }
}
