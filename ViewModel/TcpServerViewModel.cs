using Client.ViewModel.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using Client.View;

namespace Client.ViewModel
{
    internal class TcpServerViewModel : BindingHelper
    {
        private ObservableCollection<string> _messages = new ObservableCollection<string>();
        public ObservableCollection<string> Messages
        {
            get { return _messages; }
            set
            {
                _messages = value;
                onPropertyChanged();
            }
        }

        private ObservableCollection<string> _log = new ObservableCollection<string>();
        public ObservableCollection<string> Log
        {
            get { return _log; }
            set
            {
                _log = value;
                onPropertyChanged();
            }
        }

        private ObservableCollection<string> _users = new ObservableCollection<string>();
        public ObservableCollection<string> Users
        {
            get { return _users; }
            set
            {
                _users = value;
                onPropertyChanged();
            }
        }

        private Visibility _userListVisibility = Visibility.Visible;
        public Visibility UserListVisibility
        {
            get { return _userListVisibility; }
            set
            {
                _userListVisibility = value;
                onPropertyChanged();
            }
        }

        private Visibility _logListVisibility = Visibility.Collapsed;
        public Visibility LogListVisibility
        {
            get { return _logListVisibility; }
            set
            {
                _logListVisibility = value;
                onPropertyChanged();
            }
        }

        private string _messageText;
        public string MessageText
        {
            get { return _messageText; }
            set
            {
                _messageText = value;
                onPropertyChanged();
            }
        }

        private string _serverName;
        public string ServerName
        {
            get { return _serverName; }
            set
            {
                _serverName = value;
                onPropertyChanged();
            }
        }

        private Dictionary<Socket, string> _clientNames = new Dictionary<Socket, string>();
        private List<Socket> _clients = new List<Socket>();
        private Socket _serverSocket;
        private CancellationTokenSource _cancellationTokenSource;

        public BindableCommand SendMessageCommand { get; }
        public BindableCommand ToggleLogsCommand { get; }
        public BindableCommand DisconnectCommand { get; }

        public TcpServerViewModel(string serverName)
        {
            ServerName = serverName;

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, 7000));
            _serverSocket.Listen(100);
            Users.Add(ServerName);  
            Log.Add($"[{DateTime.Now:G}] Чат начал: {ServerName}");

            _cancellationTokenSource = new CancellationTokenSource();

            ListenToClients(_cancellationTokenSource.Token);

            SendMessageCommand = new BindableCommand(ExecuteSendMessage);
            ToggleLogsCommand = new BindableCommand(ToggleLogs);
            DisconnectCommand = new BindableCommand(Disconnect);
        }

        private async void ListenToClients(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _serverSocket.AcceptAsync();
                    _clients.Add(client);
                    ReceiveClientName(client, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Log.Add($"[{DateTime.Now:G}] Ошибка: {ex.Message}");
            }
        }

        private async void ReceiveClientName(Socket client, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[65536];
            int received = await client.ReceiveAsync(buffer, SocketFlags.None);
            if (received == 0) return;

            string clientName = Encoding.UTF8.GetString(buffer, 0, received);
            _clientNames[client] = clientName;
            Users.Add(clientName);
            Log.Add($"[{DateTime.Now:G}] Пользователь {clientName} подключён по ip: ({client.RemoteEndPoint})");

            
            await BroadcastUsersList();

            ReceiveMessages(client, cancellationToken);
        }

        private async void ReceiveMessages(Socket client, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] buffer = new byte[65536];
                    int received = await client.ReceiveAsync(buffer, SocketFlags.None);
                    if (received == 0) break; 

                    string message = Encoding.UTF8.GetString(buffer, 0, received);

                    // Обрабатываем команду /disconnect
                    if (message.Trim().Equals("/disconnect", StringComparison.OrdinalIgnoreCase))
                    {
                        DisconnectClient(client);
                        continue;
                    }

                    
                    foreach (var item in _clients)
                    {
                        await SendMessage(message, item);
                    }

                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Messages.Add(message);
                        string senderName = _clientNames[client];
                        Log.Add($"[{DateTime.Now:G}] [{senderName}] отправил сообщение");
                    });
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Log.Add($"[{DateTime.Now:G}] Ошибка: {ex.Message}");
                }
            }
            finally
            {
                if (_clientNames.TryGetValue(client, out string clientName))
                {
                    Log.Add($"[{DateTime.Now:G}] Пользователь {clientName} отключился от ip: ({client.RemoteEndPoint})");
                    Users.Remove(clientName);
                    _clientNames.Remove(client);
                }
                _clients.Remove(client);
                client.Close();

                await BroadcastUsersList();
            }
        }

        private async Task SendMessage(string message, Socket client)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await client.SendAsync(buffer, SocketFlags.None);
        }

        private void ExecuteSendMessage(object parameter)
        {
            string formattedMessage = $"{DateTime.Now:G} [{ServerName}]: {MessageText}";
            foreach (var client in _clients)
            {
                SendMessage(formattedMessage, client);
            }
            Messages.Add(formattedMessage);
            Log.Add($"[{DateTime.Now:G}] [{ServerName}] отправил сообщение");
            MessageText = string.Empty;
            onPropertyChanged(nameof(MessageText));
        }

        private void ToggleLogs(object parameter)
        {
            if (LogListVisibility == Visibility.Visible)
            {
                LogListVisibility = Visibility.Collapsed;
                UserListVisibility = Visibility.Visible;
            }
            else
            {
                LogListVisibility = Visibility.Visible;
                UserListVisibility = Visibility.Collapsed;
            }
        }

        private void Disconnect(object parameter)
        {
            _cancellationTokenSource.Cancel(); 
            StopServer();
            Application.Current.Dispatcher.Invoke(() =>
            {
                var authWindow = new MainWindow();
                authWindow.Show();
            });
            CloseServerWindow();
        }

        private void CloseServerWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w.DataContext == this)
                    ?.Close();
            });
        }

        public void DisconnectClient(Socket client)
        {
            string clientName = _clientNames[client];
            Users.Remove(clientName);
            Log.Add($"[{DateTime.Now:G}] Пользователь {clientName} отключился");
            _clientNames.Remove(client);
            _clients.Remove(client);
            client.Shutdown(SocketShutdown.Both);
            client.Close();

            
            _ = BroadcastUsersList();
        }

        public void StopServer()
        {
            foreach (var client in _clients)
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
            _serverSocket.Close();
        }

        private async Task BroadcastUsersList()
        {
            string userListMessage = $"USERS:{string.Join(",", Users)}";
            foreach (var client in _clients)
            {
                await SendMessage(userListMessage, client);
            }
        }
    }

}
