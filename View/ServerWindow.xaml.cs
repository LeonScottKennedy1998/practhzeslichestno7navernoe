using Client.ViewModel;
using System.Windows;

namespace Client.View
{
    public partial class ServerWindow : Window
    {
        public ServerWindow(string username)
        {
            InitializeComponent();
            DataContext = new TcpServerViewModel(username);
        }
    }
}
