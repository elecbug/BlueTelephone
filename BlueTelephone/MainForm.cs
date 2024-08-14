using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BlueTelephone
{
    public partial class MainForm : Form
    {
        private TextBox? GroupTextBox { get; set; }
        private TextBox? NameTextBox { get; set; }
        private RichTextBox? InfoLabel { get; set; }
        private ListBox? PeerListBox { get; set; }

        private TcpListener? Listener { get; set; }
        private Process? Process { get; set; }

        private List<string> Multiaddrs { get; set; } = new List<string>();
        private string PeerID { get; set; } = "";

        private List<List<string>> Closers { get; set; } = new List<List<string>> { };

        public MainForm()
        {
            InitializeComponent();
            Designer();
        }

        public void Designer()
        {
            FormClosing += (s, e) =>
            {
                Process?.Kill();
            };

            TableLayoutPanel panel = new TableLayoutPanel()
            {
                Parent = this,
                Visible = true,
                Dock = DockStyle.Fill,
            };

            panel.RowStyles.Add(new RowStyle() { Height = 50, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 50, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 50, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 50, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 1, SizeType = SizeType.Percent });

            panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 150, SizeType = SizeType.Absolute });

            panel.Controls.Add(new Label()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Group",
                TextAlign = ContentAlignment.MiddleCenter,
            }, 0, 0);

            panel.Controls.Add(new Label()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Name",
                TextAlign = ContentAlignment.MiddleCenter,
            }, 0, 1);

            panel.Controls.Add(new Label()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Info",
                TextAlign = ContentAlignment.MiddleCenter,
            }, 0, 2);

            panel.Controls.Add(new Label()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Closers",
                TextAlign = ContentAlignment.MiddleCenter,
            }, 0, 3);

            panel.Controls.Add(GroupTextBox = new TextBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
            }, 1, 0);

            panel.Controls.Add(NameTextBox = new TextBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
            }, 1, 1);

            panel.Controls.Add(InfoLabel = new RichTextBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
            }, 1, 2);

            panel.Controls.Add(PeerListBox = new ListBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                FormattingEnabled = true,
            }, 0, 4);

            PeerListBox.Format += (s, e) =>
            {
                List<string> strs = (e.ListItem as List<string>)!;
                e.Value = strs.Last() + " (" + strs[1][0..15] + "...)";
            };

            Button button = new Button()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Connect",
            };

            panel.Controls.Add(button, 2, 0);
            panel.SetRowSpan(button, 2);

            button.Click += ConnectButtonClick;
        }

        private void ConnectButtonClick(object? sender, EventArgs e)
        {
            (sender as Button)!.Enabled = false;
            GroupTextBox!.Enabled = false;
            NameTextBox!.Enabled = false;

            int port = new Random().Next(0, 1 << 16);

            Listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            Listener.Start();

            Thread t = new Thread(async () =>
            {
                Task<TcpClient> task = Listener.AcceptTcpClientAsync();

                Process = Process.Start(new ProcessStartInfo()
                {
                    FileName = "../../../../background/blue-telephone-d.exe",
                    Arguments = $"--group {GroupTextBox.Text} --name {NameTextBox.Text} --port {port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });

                TcpClient client = await task;

                try
                {
                    while (true)
                    {
                        byte[] buffer = new byte[1024];
                        client.GetStream().Read(buffer, 0, 1024);

                        string json = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                        Debug.WriteLine(json);

                        Packet? packet = JsonSerializer.Deserialize<Packet>(json);

                        if (packet == null)
                        {
                            continue;
                        }

                        switch ((MsgCode)packet.MsgCode)
                        {
                            case MsgCode.PanicError:
                                MessageBox.Show(string.Join("\r\n", packet.Msg), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Close();

                                break;

                            case MsgCode.DeniedError:
                                MessageBox.Show(string.Join("\r\n", packet.Msg), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                                break;

                            case MsgCode.Success:

                                break;

                            case MsgCode.CreateHost:
                                Multiaddrs = packet.Msg[0..(packet.Msg.Count - 1)];
                                Multiaddrs.Add("");
                                PeerID = packet.Msg.Last();

                                InfoLabel!.Invoke(() => InfoLabel!.Text = string.Join($"/{PeerID}\r\n", Multiaddrs));

                                break;

                            case MsgCode.FoundPeer:
                                Closers.Add(packet.Msg);
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Add(packet.Msg));

                                break;

                            case MsgCode.RemovePeer:
                                object closer = Closers.First(x => x[1] == packet.Msg.First());
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Remove(closer));

                                break;
                        }
                    }
                }
                catch { }
            });

            t.Start();
        }
    }

    public class Packet
    {
        public required string TS { get; set; }
        public required int MsgCode { get; set; }
        public required List<string> Msg { get; set; }
    }

    public enum MsgCode
    {
        PanicError = -1,
        DeniedError = 0,
        Success = 1,
        CreateHost = 2,
        FoundPeer = 3,
        RemovePeer = 4,
    }
}
