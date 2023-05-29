using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrgStructureSync
{
        class ModelHolder<T> where T : IIdAble
        {
            public Object lockObject = new Object();
            public Dictionary<Guid, T> idMap = new Dictionary<Guid, T>();
            public Dictionary<string, T> nameMap = new Dictionary<string, T>();
            public ObservableCollection<T> elements = new ObservableCollection<T>();

            public List<KeyValuePair<Guid, string>> FetchAll()
            {
                lock (lockObject)
                {
                    List<KeyValuePair<Guid, string>> result = new List<KeyValuePair<Guid, string>>();
                    foreach (var entry in nameMap)
                    {
                        result.Add(new KeyValuePair<Guid, string>(entry.Value.ID.Value, entry.Key));
                    }
                    return result;
                }
            }
        }

    class DataModel
    {
        static public DataModel INSTANCE { get; private set; } = new DataModel();
        public bool IsMaster { get { return clientApi == null; } }
        public ModelHolder<User> Users { get; } = new ModelHolder<User>();
        public ModelHolder<Role> Roles { get; } = new ModelHolder<Role>();

        IOrgSyncService clientApi;
        IOrgSyncServiceCallback serverApi = OrgSyncServer.OrgSyncServerCallbackHandler.INSTANCE;

        public void SetClientMode(IOrgSyncService api)
        {
            clientApi = api;
        }

        public Guid? AddUser(string userName, Guid? guid = null)
        {
            return Add(userName, guid, Users, (name) => clientApi.CreateUser(name), (id, name) => serverApi.OnUserAdded(id, name));
        }

        public Guid? AddRole(string roleName, Guid? guid = null)
        {
            return Add(roleName, guid, Roles, (name) => clientApi.CreateRole(name), (id, name) => serverApi.OnRoleAdded(id, name));
        }

        private Guid? Add<T>(string name, Guid? id, ModelHolder<T> holder, Func<string, Guid> createOnRemote, Action<Guid, string> notifyClient) where T : IIdAble
        {
            lock (holder.lockObject)
            {
                T entry;
                if (!holder.nameMap.ContainsKey(name))
                {
                    if (IsMaster) id = Guid.NewGuid();
                    entry = (T)Activator.CreateInstance(typeof(T), new object[] { name, id });
                    holder.nameMap.Add(name, entry);
                    if (!id.HasValue)
                    { //Create on host
                        Task.Run(() =>
                        {
                            entry.RegisterId(createOnRemote(name));
                            lock (holder.lockObject)
                            {
                                holder.idMap.Add(entry.ID.Value, entry);
                            }
                        });
                    }
                    else
                    {
                        holder.idMap.Add(id.Value, entry);
                        if (IsMaster)
                        {
                            notifyClient.Invoke(id.Value, name);
                        }
                    }
                    holder.elements.Add(entry);
                }
                else if (id.HasValue)
                {
                    entry = holder.nameMap[name];
                    entry.RegisterId(id.Value);
                }
                return id;
            }
        }

        public void DeleteUser(string userName)
        {
            lock(Users.lockObject)
            {
                if (!Users.nameMap.ContainsKey(userName)) return;
                User user = Users.nameMap[userName];
                if (!user.ID.HasValue) return;
                DeleteUser(user.ID.Value);
                if (!IsMaster)
                { //TODO handle case that user doesn't have a guid yet
                    Task.Run(() => clientApi.DeleteUser(user.ID.Value));
                }
            }
        }

        public bool DeleteUser(Guid guid)
        {
            lock (Users.lockObject)
            {
                if (!Users.idMap.ContainsKey(guid)) return false;
                User user = Users.idMap[guid];
                Users.idMap.Remove(guid);
                Users.nameMap.Remove(user.Name);
                Users.elements.Remove(user);
                if (IsMaster)
                {
                    serverApi.OnUserRemoved(guid);
                }
            }
            return true;
        }

        public void DeleteRole(string roleName)
        {
            lock (Roles.lockObject)
            {
                if (!Roles.nameMap.ContainsKey(roleName)) return;
                Role role = Roles.nameMap[roleName];
                if (!role.ID.HasValue) return;
                DeleteRole(role.ID.Value);
                if (!IsMaster)
                { //TODO handle case that role doesn't have a guid yet
                    Task.Run(() => clientApi.DeleteRole(role.ID.Value));
                }
            }
        }

        public bool DeleteRole(Guid guid)
        {
            lock (Roles.lockObject)
            {
                if (!Roles.idMap.ContainsKey(guid)) return false;
                Role role = Roles.idMap[guid];
                Roles.idMap.Remove(guid);
                Roles.nameMap.Remove(role.Name);
                Roles.elements.Remove(role);
                if (IsMaster)
                {
                    serverApi.OnRoleRemoved(guid);
                }
                lock (Users.lockObject)
                {
                    foreach (var user in Users.nameMap)
                    {
                        user.Value.Roles.Remove(role);
                    }
                }
            }
            return true;
        }

        public void AddRole(string userName, string roleName)
        {
            lock(Roles.lockObject)
            {
                if (!Roles.nameMap.ContainsKey(roleName)) return;
                lock(Users.lockObject)
                {
                    if (!Users.nameMap.ContainsKey(userName)) return;
                    Guid? userId = Users.nameMap[userName].ID;
                    Guid? roleId = Roles.nameMap[roleName].ID;
                    if (!userId.HasValue || !roleId.HasValue) return;
                    AddRole(userId.Value, roleId.Value);
                    if (!IsMaster)
                    {
                        Task.Run(() => clientApi.AddRole(userId.Value, roleId.Value));
                    }
                }
            }
        }

        public ActionResult AddRole(Guid userId, Guid roleId)
        {
            lock(Roles.lockObject)
            {
                if (!Roles.idMap.ContainsKey(roleId)) return ActionResult.UNKNOWN_ROLE;
                Role role = Roles.idMap[roleId];
                lock (Users.lockObject)
                {
                    if (!Users.idMap.ContainsKey(userId)) return ActionResult.UNKNOWN_USER;
                    User user = Users.idMap[userId];

                    if (user.AddRole(role))
                    {
                        if (IsMaster)
                        {
                            serverApi.OnUserRoleAdded(userId, roleId);
                        }
                    }

                    return ActionResult.SUCCESS;
                }
            }
        }

        public void RemoveRole(string userName, string roleName)
        {
            lock (Roles.lockObject)
            {
                if (!Roles.nameMap.ContainsKey(roleName)) return;
                lock (Users.lockObject)
                {
                    if (!Users.nameMap.ContainsKey(userName)) return;
                    Guid? userId = Users.nameMap[userName].ID;
                    Guid? roleId = Roles.nameMap[roleName].ID;
                    if (!userId.HasValue || !roleId.HasValue) return;
                    RemoveRole(userId.Value, roleId.Value);
                    if (!IsMaster)
                    {
                        Task.Run(() => clientApi.RemoveRole(userId.Value, roleId.Value));
                    }
                }
            }
        }

        public ActionResult RemoveRole(Guid userId, Guid roleId)
        {
            lock (Roles.lockObject)
            {
                Role role;
                if (!Roles.idMap.TryGetValue(roleId, out role)) return ActionResult.UNKNOWN_ROLE;
                lock (Users.lockObject)
                {
                    User user;
                    if (!Users.idMap.TryGetValue(userId, out user)) return ActionResult.UNKNOWN_USER;

                    user.RemoveRole(role);

                    if (IsMaster)
                    {
                        serverApi.OnUserRoleRemoved(userId, roleId);
                    }

                    return ActionResult.SUCCESS;
                }
            }
        }

        public List<KeyValuePair<Guid, string>> FetchUsers()
        {
            return Users.FetchAll();
        }

        public List<KeyValuePair<Guid, string>> FetchRoles()
        {
            return Roles.FetchAll();
        }

        public List<KeyValuePair<Guid, Guid>> FetchUserRoles()
        {
            var result = new List<KeyValuePair<Guid, Guid>>();
            if (!IsMaster) return result;
            lock (Roles.lockObject)
            {
                lock (Users.lockObject)
                {
                    foreach (var entry in Users.idMap)
                    {
                        foreach(Role role in entry.Value.Roles)
                        {
                            result.Add(new KeyValuePair<Guid, Guid>(entry.Key, role.ID.Value));
                        }
                    }
                }
            }
            return result;
        }

        public User GetUser(string userName)
        {
            lock(Users.lockObject)
            {
                return Users.nameMap.TryGetValue(userName, out var user) ? user : null;
            }
        }
    }
}
