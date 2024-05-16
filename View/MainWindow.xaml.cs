using Client.View;
using Client.ViewModel;
using System;
using System.Net;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new AuthViewModel();
            DataContext = viewModel;
            viewModel.CloseWindowAction = new Action(this.Close); 
        }
    }
}
