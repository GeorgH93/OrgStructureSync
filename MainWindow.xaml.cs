using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ServiceModel;
using System.ServiceModel.Discovery;

namespace OrgStructureSync
{
    public partial class MainWindow : Window
    {
        private ServiceHost server = null;
        private OrgSyncClient client = null;

        public MainWindow()
        {
            InitializeComponent();

            ConnectToServer();

            UpdateSelectedUser();
            ListBoxUsers.ItemsSource = DataModel.INSTANCE.Users.elements;
            ListBoxRoles.ItemsSource = DataModel.INSTANCE.Roles.elements;
        }

        private void StartServer()
        {
            server = new ServiceHost(typeof(OrgSyncServer));
            server.Description.Behaviors.Add(new ServiceDiscoveryBehavior());
            server.AddServiceEndpoint(new UdpDiscoveryEndpoint());
            server.Open();
        }

        private void ConnectToServer()
        {
            while (true)
            {
                var host = OrgSyncClient.FindServer();
                string hostAddress = "";
                if (host != null)
                {
                    hostAddress = host.ToString();
                }
                string s = Microsoft.VisualBasic.Interaction.InputBox("Please enter the address of the server to connect to.", "Server Adress", hostAddress);
                if (s == "")
                {
                    var result = MessageBox.Show("No Server Address given, should this instance start as a master instance?", "Start as master?", MessageBoxButton.YesNoCancel);
                    if (result == MessageBoxResult.Cancel)
                    {
                        Close();
                        return;
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        StartServer();
                        break;
                    }
                }
                else
                {
                    try
                    {
                        client = new OrgSyncClient(new EndpointAddress(s));
                        client.OnConnectionLostEvent += () => {
                            MessageBox.Show("Lost connection to server!");
                            Application.Current.Dispatcher.Invoke(() => Close());
                        };
                        break;
                    }
                    catch
                    {}
                }
            }
        }

        private void ListBoxUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBoxUsers.SelectedItems.Count == 1)
            {
                UpdateSelectedUser(ListBoxUsers.SelectedItem.ToString());
            }
            else
            {
                UpdateSelectedUser();
            }
        }

        private void UpdateSelectedUser(string selectedUserName = "")
        {
            LabelSelectedUser.Content = selectedUserName;
            User user = DataModel.INSTANCE.GetUser(selectedUserName);
            ListBoxUserRoles.ItemsSource = user != null ? user.Roles : null;
        }

        private void ButtonAddUser_Click(object sender, RoutedEventArgs e)
        {
            if (TextboxAddUser.Text.Length > 0)
            {
                string userName = TextboxAddUser.Text;
                TextboxAddUser.Text = "";
                DataModel.INSTANCE.AddUser(userName);
            }
        }

        private void ButtonDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var selection = ListBoxUsers.SelectedItems;
            if (selection.Count == 0) return; // Nothing selected = nothing to delete
            if (selection.Count == 1)
            {
                string userName = selection[0].ToString();
                if (MessageBox.Show($"Are you sure that you would like to delete user '{userName}'?", "Delete user?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DataModel.INSTANCE.DeleteUser(userName);
                }
            }
            else
            {
                List<string> toDelete = SelectionToStringList(selection);
                string userNames = String.Join(", ", toDelete);
                if (MessageBox.Show($"Are you sure that you would like to delete the following users?\nUsers to be deleted:\n{userNames}", "Delete users?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    foreach (object user in toDelete)
                    {
                        DataModel.INSTANCE.DeleteUser(user.ToString());
                    }
                }
            }
        }

        private void ButtonAddRole_Click(object sender, RoutedEventArgs e)
        {
            if (TextboxAddRole.Text.Length > 0)
            {
                string roleName = TextboxAddRole.Text;
                TextboxAddRole.Text = "";
                DataModel.INSTANCE.AddRole(roleName);
            }

        }

        private void ButtonDeleteRole_Click(object sender, RoutedEventArgs e)
        {
            var selection = ListBoxRoles.SelectedItems;
            if (selection.Count == 0) return; // Nothing selected = nothing to delete
            if (selection.Count == 1)
            {
                string roleName = selection[0].ToString();
                if (MessageBox.Show($"Are you sure that you would like to delete the role '{roleName}'? This will also remove the role from any user having it.", "Delete role?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DataModel.INSTANCE.DeleteRole(roleName);
                }
            }
            else
            {
                List<string> toDelete = SelectionToStringList(selection);
                string roleNames = String.Join(", ", toDelete);
                if (MessageBox.Show($"Are you sure that you would like to delete the following roles?\nThis will also remove the roles from any user having it.\nRoles to be deleted:\n{roleNames}", "Delete roles?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    foreach (object role in toDelete)
                    {
                        DataModel.INSTANCE.DeleteRole(role.ToString());
                    }
                }
            }
        }

        private void ButtonRemoveRoleFromUser_Click(object sender, RoutedEventArgs e)
        {
            List<string> toRemove = SelectionToStringList(ListBoxUserRoles.SelectedItems);
            foreach (string item in toRemove)
            {
                DataModel.INSTANCE.RemoveRole(LabelSelectedUser.Content.ToString(), item);
            }
        }

        private void ButtonAddRoleToUser_Click(object sender, RoutedEventArgs e)
        {
            List<string> toAdd = SelectionToStringList(ListBoxRoles.SelectedItems);
            foreach (string item in toAdd)
            {
                DataModel.INSTANCE.AddRole(LabelSelectedUser.Content.ToString(), item);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (server != null)
            {
                server.Abort();
            }
        }

        private static List<string> SelectionToStringList(System.Collections.IList selection)
        {
            List<string> stringSelection = new List<string>();

            foreach(object item in selection)
            {
                stringSelection.Add(item.ToString());
            }

            return stringSelection;
        }

        private void TextboxAddUser_GotFocus(object sender, RoutedEventArgs e)
        {
            ButtonAddRole.IsDefault = false;
            ButtonAddUser.IsDefault = true;
        }

        private void TextboxAddUser_LostFocus(object sender, RoutedEventArgs e)
        {
            ButtonAddUser.IsDefault = false;
        }

        private void TextboxAddRole_GotFocus(object sender, RoutedEventArgs e)
        {
            ButtonAddUser.IsDefault = false;
            ButtonAddRole.IsDefault = true;
        }

        private void TextboxAddRole_LostFocus(object sender, RoutedEventArgs e)
        {
            ButtonAddRole.IsDefault = false;
        }
    }
}
