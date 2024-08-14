using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace BlueTelephone
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 12000);
            listener.Start();

            Process? process = Process.Start(new ProcessStartInfo()
            {
                FileName = "blue-telephone-d.exe",
                Arguments = "",
                UseShellExecute = false,
            });

            listener.AcceptTcpClient();
        }
    }
}
