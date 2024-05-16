using Client.ViewModel.Helpers;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Windows;
using System;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using Client.View;
using System.Linq;
using System.Threading;

namespace Client.ViewModel
{
    internal class TcpClientViewModel : BindingHelper
    {
        private Socket _socket;
        private bool _isConnected;
        private bool _isAuthWindowOpened; 
        private CancellationTokenSource _cancellationTokenSource;
        public string InputText { get; set; }
        public string Username { get; }

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

        public BindableCommand SendMessageCommand { get; }
        public BindableCommand DisconnectCommand { get; }

        public TcpClientViewModel(string username, string serverIp, int port)
        {
            Username = username;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _isConnected = true;
            _isAuthWindowOpened = false;
            _cancellationTokenSource = new CancellationTokenSource();
            ConnectAsync(serverIp, port);
            SendMessageCommand = new BindableCommand(_ => ProcessMessage(InputText));
            DisconnectCommand = new BindableCommand(_ => Disconnect());
        }

        private async void ConnectAsync(string serverIp, int port)
        {
            try
            {
                await _socket.ConnectAsync(serverIp, port);
                await SendMessageAsync(Username); 
                Users.Add(Username); 
                ReceiveMessages(_cancellationTokenSource.Token);
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Ошибка подключения к серверу: {ex.Message}");
                OpenAuthWindow();
                CloseClientWindow();
            }
        }

        private async void ReceiveMessages(CancellationToken cancellationToken)
        {
            try
            {
                while (_isConnected && !cancellationToken.IsCancellationRequested)
                {
                    byte[] buffer = new byte[65536];
                    int received = await _socket.ReceiveAsync(buffer, SocketFlags.None);
                    if (received == 0) break; 

                    string message = Encoding.UTF8.GetString(buffer, 0, received);

                    
                    if (message.Trim().Equals("/disconnect", StringComparison.OrdinalIgnoreCase))
                    {
                        Disconnect();
                        continue;
                    }

                    
                    if (message.StartsWith("USERS:"))
                    {
                        var users = message.Substring(6).Split(',');
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Users.Clear();
                            foreach (var user in users)
                            {
                                Users.Add(user);
                            }
                        });
                        continue;
                    }

                    Application.Current.Dispatcher.Invoke(() => Messages.Add(message));
                }
            }
            catch (SocketException ex)
            {
                if (_isConnected) 
                {
                    MessageBox.Show($"Ошибка получения сообщения: {ex.Message}");
                }
            }
            finally
            {
                OpenAuthWindow();
                CloseClientWindow();
            }
        }

        private async void SendMessage(string message)
        {
            string formattedMessage = $"{DateTime.Now:G} [{Username}]: {message}";
            byte[] buffer = Encoding.UTF8.GetBytes(formattedMessage);
            await _socket.SendAsync(buffer, SocketFlags.None);
        }

        private async Task SendMessageAsync(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(buffer, SocketFlags.None);
        }

        private void ProcessMessage(string message)
        {
            if (message.Trim().Equals("/disconnect", StringComparison.OrdinalIgnoreCase))
            {
                Disconnect();
            }
            else
            {
                SendMessage(message);
            }
        }

        private void Disconnect()
        {
            _isConnected = false;
            _cancellationTokenSource.Cancel(); 
            
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();

            OpenAuthWindow();
            CloseClientWindow();
        }

        private void OpenAuthWindow()
        {
            if (!_isAuthWindowOpened)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var authWindow = new MainWindow();
                    authWindow.Show();
                });
                _isAuthWindowOpened = true;
            }
        }

        private void CloseClientWindow()
        {
            Application.Current.Windows
                .OfType<ClientWindow>()
                .FirstOrDefault()?.Close();
        }
    }
}
