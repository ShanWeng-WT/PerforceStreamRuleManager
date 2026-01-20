using System.Windows;

namespace PerforceStreamManager.Views
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow(string message = "Please wait...")
        {
            InitializeComponent();
            MessageText.Text = message;
        }
    }
}
