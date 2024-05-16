using Client.Model;
using Client.View;
using Client.ViewModel.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Client.ViewModel
{
    internal class AuthViewModel : BindingHelper
    {
        private LoginModel _loginModel = new LoginModel();
        public string Username
        {
            get => _loginModel.Username;
            set
            {
                _loginModel.Username = value;
                onPropertyChanged(nameof(Username));
            }
        }

        public string IpAddress
        {
            get => _loginModel.IpAddress;
            set
            {
                _loginModel.IpAddress = value;
                onPropertyChanged(nameof(IpAddress));
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                onPropertyChanged(nameof(ErrorMessage));
            }
        }

        public BindableCommand CreateChatCommand { get; private set; }
        public BindableCommand ConnectToChatCommand { get; private set; }

        public Action CloseWindowAction { get; set; } 

        public AuthViewModel()
        {
            CreateChatCommand = new BindableCommand(CreateChat);
            ConnectToChatCommand = new BindableCommand(ConnectToChat);
        }

        private void CreateChat(object parameter)
        {
            if (ValidateUsername())
            {
                var serverWindow = new ServerWindow(Username);
                serverWindow.Show();
                CloseWindowAction?.Invoke(); 
            }
        }

        private void ConnectToChat(object parameter)
        {
            if (ValidateUsername() && ValidateIpAddress())
            {
                var clientWindow = new ClientWindow(Username, IpAddress);
                clientWindow.Show();
                CloseWindowAction?.Invoke(); 
            }
        }

        private bool ValidateUsername()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Имя пользователя не должно быть пустым.";
                return false;
            }
            ErrorMessage = null;
            return true;
        }

        private bool ValidateIpAddress()
        {
            if (string.IsNullOrWhiteSpace(IpAddress))
            {
                ErrorMessage = "IP-адрес не должен быть пустым.";
                return false;
            }

            if (!IPAddress.TryParse(IpAddress, out _))
            {
                ErrorMessage = "Неверный формат IP-адреса.";
                return false;
            }

            ErrorMessage = null;
            return true;
        }
    }
}