using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Timers;

namespace OrgStructureSync
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Reentrant)]
    class OrgSyncServer : IOrgSyncService
    {
        public class OrgSyncServerCallbackHandler : IOrgSyncServiceCallback
        {
            private class CallbackHolder
            {
                public IOrgSyncServiceCallback callback;
                public Queue<Action<IOrgSyncServiceCallback>> unprocessed = new Queue<Action<IOrgSyncServiceCallback>>();
                bool unavailable = false;

                public CallbackHolder(IOrgSyncServiceCallback callbackChannel)
                {
                    this.callback = callbackChannel;
                }

                public void Notify(Action<IOrgSyncServiceCallback> action)
                {
                    if (unavailable) return;
                    try
                    {
                        lock (unprocessed)
                        {
                            while (unprocessed.Count > 0)
                            {
                                unprocessed.Peek().Invoke(callback);
                                unprocessed.Dequeue();
                            }
                        }
                        action.Invoke(callback);
                    }
                    catch (CommunicationException)
                    {
                        lock (unprocessed)
                        {
                            unprocessed.Enqueue(action);
                            if (unprocessed.Count > 5)
                            {
                                unavailable = true;
                                unprocessed.Clear();
                            }
                        }
                    }
                }
            }

            public static readonly OrgSyncServerCallbackHandler INSTANCE = new OrgSyncServerCallbackHandler();

            private ConcurrentBag<CallbackHolder> callbackChannels = new ConcurrentBag<CallbackHolder>();

            private Timer heartBeatTimer = new Timer();

            private OrgSyncServerCallbackHandler()
            {
                heartBeatTimer.Interval = 2000;
                heartBeatTimer.Elapsed += new ElapsedEventHandler((s, o) => Heartbeat());
                heartBeatTimer.Start();
            }

            public void RegisterCallbackCannel(IOrgSyncServiceCallback channel)
            {
                callbackChannels.Add(new CallbackHolder(channel));
            }

            private void NotifyAll(Action<IOrgSyncServiceCallback> action)
            {
                Task.Run(() =>
                {
                    foreach (var callback in callbackChannels)
                    {
                        callback.Notify(action);
                    }
                });
            }

            public void OnRoleAdded(Guid roleId, string roleName)
            {
                NotifyAll(callback => callback.OnRoleAdded(roleId, roleName));
            }

            public void OnRoleRemoved(Guid roleId)
            {
                NotifyAll(callback => callback.OnRoleRemoved(roleId));
            }

            public void OnUserAdded(Guid userId, string userName)
            {
                NotifyAll(callback => callback.OnUserAdded(userId, userName));
            }

            public void OnUserRemoved(Guid userId)
            {
                NotifyAll(callback => callback.OnUserRemoved(userId));
            }

            public void OnUserRoleAdded(Guid userId, Guid roleId)
            {
                NotifyAll(callback => callback.OnUserRoleAdded(userId, roleId));
            }

            public void OnUserRoleRemoved(Guid userId, Guid roleId)
            {
                NotifyAll(callback => callback.OnUserRoleRemoved(userId, roleId));
            }

            public void Heartbeat()
            {
                NotifyAll(callback => callback.Heartbeat());
            }
        }

        public OrgSyncServer()
        {
            int i = 0;
        }

        public Guid CreateRole(string role)
        {
            return DataModel.INSTANCE.AddRole(role).Value;
        }

        public Guid CreateUser(string user)
        {
            return DataModel.INSTANCE.AddUser(user).Value;
        }

        public ActionResult DeleteRole(Guid roleId)
        {
            return DataModel.INSTANCE.DeleteRole(roleId) ? ActionResult.SUCCESS : ActionResult.UNKNOWN_ROLE;
        }

        public ActionResult DeleteUser(Guid userId)
        {
            return DataModel.INSTANCE.DeleteUser(userId) ? ActionResult.SUCCESS : ActionResult.UNKNOWN_USER;
        }

        public List<KeyValuePair<Guid, string>> FetchRoles()
        {
            return DataModel.INSTANCE.FetchRoles();
        }

        public List<KeyValuePair<Guid, Guid>> FetchUserRoles()
        {
            return DataModel.INSTANCE.FetchUserRoles();
        }

        public List<KeyValuePair<Guid, string>> FetchUsers()
        {
            OrgSyncServerCallbackHandler.INSTANCE.RegisterCallbackCannel(OperationContext.Current.GetCallbackChannel<IOrgSyncServiceCallback>());
            return DataModel.INSTANCE.FetchUsers();
        }

        public ActionResult AddRole(Guid userId, Guid roleId)
        {
            return DataModel.INSTANCE.AddRole(userId, roleId);
        }

        public ActionResult RemoveRole(Guid userId, Guid roleId)
        {
            return DataModel.INSTANCE.RemoveRole(userId, roleId);
        }
    }
}
