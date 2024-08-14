using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace BlueTelephone
{
    public partial class MainForm : Form
    {
        TextBox

        public MainForm()
        {
            InitializeComponent();
            Designer();

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

        public void Designer()
        {
            TableLayoutPanel panel = new TableLayoutPanel()
            {
                Parent = this,
                Visible = true,
            };

            panel.RowStyles.Add(new RowStyle() { Height = 50, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 50, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 1, SizeType = SizeType.Percent });

            panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 150, SizeType = SizeType.Absolute });
        }
    }
}
