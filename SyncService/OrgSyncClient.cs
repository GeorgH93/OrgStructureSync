using System;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using System.Timers;

namespace OrgStructureSync
{
    class OrgSyncClient : IOrgSyncServiceCallback
    {
        private IOrgSyncService syncService;
        private readonly Timer isAliveTimer = new Timer();
        private int heartbeatsCount = 0;

        public static EndpointAddress FindServer()
        {
            DiscoveryClient discoveryClient = new DiscoveryClient(new UdpDiscoveryEndpoint());
            FindCriteria criteria = new FindCriteria(typeof(IOrgSyncService));
            criteria.Duration = TimeSpan.FromSeconds(1);
            FindResponse response = discoveryClient.Find(criteria);
            if (response.Endpoints.Count > 0)
            {
                return response.Endpoints[0].Address;
            }
            return null;
        }

        public OrgSyncClient(EndpointAddress address)
        {
            WSDualHttpBinding binding = new WSDualHttpBinding();
            binding.OpenTimeout = new TimeSpan(0, 0, 10);
            binding.CloseTimeout = new TimeSpan(0, 0, 10);
            binding.SendTimeout = new TimeSpan(0, 0, 10);
            binding.ReceiveTimeout = new TimeSpan(0, 0, 10);
            var factory = new DuplexChannelFactory<IOrgSyncService>(new InstanceContext(this), binding, address);
            syncService = factory.CreateChannel();
            DataModel.INSTANCE.SetClientMode(syncService);

            // Load data from master
            var users = syncService.FetchUsers();
            foreach(var entry in users)
            {
                DataModel.INSTANCE.AddUser(entry.Value, entry.Key);
            }
            var roles = syncService.FetchRoles();
            foreach (var entry in roles)
            {
                DataModel.INSTANCE.AddRole(entry.Value, entry.Key);
            }
            var userRoles = syncService.FetchUserRoles();
            foreach (var entry in userRoles)
            {
                DataModel.INSTANCE.AddRole(entry.Key, entry.Value);
            }

            isAliveTimer.Interval = 3000;
            isAliveTimer.Elapsed += new ElapsedEventHandler((s,o) => {
                if (++heartbeatsCount > 2)
                {
                    isAliveTimer.Stop();
                    OnConnectionLostEvent?.Invoke();
                }
            });
            isAliveTimer.Start();
        }

        public void OnRoleAdded(Guid roleId, string roleName)
        {
            DataModel.INSTANCE.AddRole(roleName, roleId);
        }

        public void OnRoleRemoved(Guid roleId)
        {
            DataModel.INSTANCE.DeleteRole(roleId);
        }

        public void OnUserAdded(Guid userId, string userName)
        {
            DataModel.INSTANCE.AddUser(userName, userId);
        }

        public void OnUserRemoved(Guid userId)
        {
            DataModel.INSTANCE.DeleteUser(userId);
        }

        public void OnUserRoleAdded(Guid userId, Guid roleId)
        {
            DataModel.INSTANCE.AddRole(userId, roleId);
        }

        public void OnUserRoleRemoved(Guid userId, Guid roleId)
        {
            DataModel.INSTANCE.RemoveRole(userId, roleId);
        }

        public void Heartbeat()
        {
            heartbeatsCount = 0;
        }

        public delegate void ConnectionLost();
        public event ConnectionLost OnConnectionLostEvent;
    }
}
