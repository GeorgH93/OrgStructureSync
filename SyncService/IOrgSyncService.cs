using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace OrgStructureSync
{
    enum ActionResult : int
    {
        SUCCESS = 0, UNKNOWN_USER, UNKNOWN_ROLE, 
    }

    [ServiceContract(CallbackContract = typeof(IOrgSyncServiceCallback))]
    interface IOrgSyncService
    {
        [OperationContract]
        Guid CreateUser(string user);

        [OperationContract]
        Guid CreateRole(string role);

        [OperationContract]
        ActionResult AddRole(Guid userId, Guid roleId);

        [OperationContract]
        ActionResult RemoveRole(Guid userId, Guid roleId);

        [OperationContract]
        ActionResult DeleteUser(Guid userId);

        [OperationContract]
        ActionResult DeleteRole(Guid roleId);

        [OperationContract]
        List<KeyValuePair<Guid, string>> FetchUsers();

        [OperationContract]
        List<KeyValuePair<Guid, string>> FetchRoles();

        [OperationContract]
        List<KeyValuePair<Guid, Guid>> FetchUserRoles();
    }

    //[ServiceContract]
    interface IOrgSyncServiceCallback
    {
        [OperationContract]
        void OnUserAdded(Guid userId, string userName);
        
        [OperationContract]
        void OnRoleAdded(Guid roleId, string roleName);

        [OperationContract]
        void OnUserRoleAdded(Guid userId, Guid roleId);

        [OperationContract]
        void OnUserRemoved(Guid userId);

        [OperationContract]
        void OnRoleRemoved(Guid roleId);

        [OperationContract]
        void OnUserRoleRemoved(Guid userId, Guid roleId);

        [OperationContract]
        void Heartbeat();
    }
}
