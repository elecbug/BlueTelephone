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
        private TextBox? GossipTextBox { get; set; }
        private RichTextBox? InfoTextBox { get; set; }
        private ListBox? PeerListBox { get; set; }
        private TabControl? GossipTabControl { get; set; }
        private Button? ConnectButton { get; set; }
        private Button? GossipButton { get; set; }

        private TcpListener? Listener { get; set; }
        private TcpClient? Client { get; set; }
        private Process? Process { get; set; }

        private List<string> Multiaddrs { get; set; } = new List<string>();
        private string PeerID { get; set; } = "";

        private List<List<string>> Closers { get; set; } = new List<List<string>>();

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

            panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 50, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
            panel.RowStyles.Add(new RowStyle() { Height = 1, SizeType = SizeType.Percent });
            panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });

            panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 6, SizeType = SizeType.Percent });
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 100, SizeType = SizeType.Absolute });

            panel.Controls.Add(new Label()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Group",
                TextAlign = ContentAlignment.MiddleRight,
            }, 0, 0);

            panel.Controls.Add(new Label()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Name",
                TextAlign = ContentAlignment.MiddleRight,
            }, 0, 1);

            panel.Controls.Add(new Label()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Info",
                TextAlign = ContentAlignment.MiddleRight,
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

            panel.Controls.Add(InfoTextBox = new RichTextBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
            }, 1, 2);

            panel.SetColumnSpan(InfoTextBox, 2);

            panel.Controls.Add(PeerListBox = new ListBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                FormattingEnabled = true,
            }, 0, 4);
            panel.SetRowSpan(PeerListBox, 2);

            PeerListBox.Format += (s, e) =>
            {
                List<string> strs = (e.ListItem as List<string>)!;
                e.Value = strs.Last() + " (" + strs[1][0..15] + "...)";
            };

            panel.Controls.Add(ConnectButton = new Button()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Connect",
            }, 2, 0);

            panel.SetRowSpan(ConnectButton, 2);

            ConnectButton.Click += ConnectButtonClick;

            panel.Controls.Add(GossipTabControl = new TabControl()
            {
                Visible = true,
                Dock = DockStyle.Fill,
            }, 1, 3);

            panel.SetRowSpan(GossipTabControl, 2);
            panel.SetColumnSpan(GossipTabControl, 2);

            panel.Controls.Add(GossipTextBox = new TextBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Enabled = false,
            }, 1, 5);

            GossipTextBox.TextChanged += GossipTextBoxTextChanged;

            panel.Controls.Add(GossipButton = new Button()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "Join/Exit",
                Enabled = false,
            }, 2, 5);

            GossipButton.Click += GossipButtonClick;
        }

        private void GossipTextBoxTextChanged(object? sender, EventArgs e)
        {
            if (GossipTextBox!.Text == "")
            {
                GossipButton!.Text = "Join/Exit";
            }
            else
            {
                foreach (GossipTabPage page in GossipTabControl!.TabPages)
                {
                    if (GossipTextBox!.Text == page.Text)
                    {
                        GossipButton!.Text = "Exit";
                        return;
                    }
                }

                GossipButton!.Text = "Join";
            }
        }

        private void GossipButtonClick(object? sender, EventArgs e)
        {
            if (GossipTextBox!.Text == "")
            {
                return;
            }

            foreach (TabPage page in GossipTabControl!.TabPages)
            {
                if (page.Text == GossipTextBox!.Text)
                {
                    if (MessageBox.Show("Exits the room?", "Warning", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        string json1 = JsonSerializer.Serialize(new Packet()
                        {
                            TS = DateTime.Now.ToString(),
                            MsgCode = (int)MsgCode.ExitGossip,
                            Msg = new List<string> { page.Text }
                        });

                        byte[] data1 = Encoding.UTF8.GetBytes(json1);
                        byte[] buf1 = new byte[1024];

                        for (int i = 0; i < data1.Length; i++)
                        {
                            buf1[i] = data1[i];
                        }

                        Client!.GetStream().Write(buf1);

                        GossipTextBox!.Text = "";
                        GossipTabControl!.TabPages.Remove(page);
                    }

                    return;
                }
            }

            string json = JsonSerializer.Serialize(new Packet()
            {
                TS = DateTime.Now.ToString(),
                MsgCode = (int)MsgCode.JoinGossip,
                Msg = new List<string> { GossipTextBox!.Text }
            });

            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] buf = new byte[1024];

            for (int i = 0; i < data.Length; i++)
            {
                buf[i] = data[i];
            }

            Client!.GetStream().Write(buf);

            GossipTabControl!.TabPages.Add(new GossipTabPage(Client, GossipTextBox!.Text)
            {
                Text = GossipTextBox!.Text,
            });

            GossipTabControl!.SelectTab(GossipTabControl!.TabPages.Count - 1);

            GossipTextBox!.Text = "";
        }

        private void ConnectButtonClick(object? sender, EventArgs e)
        {
            if (GroupTextBox?.Text == "")
            {
                GroupTextBox.Text = "default";
            }
            if (NameTextBox?.Text == "")
            {
                NameTextBox.Text = $"BT-{new Random().Next()}";
            }

            (sender as Button)!.Enabled = false;
            GroupTextBox!.Enabled = false;
            NameTextBox!.Enabled = false;

            GossipTextBox!.Enabled = true;
            GossipButton!.Enabled = true;

            int port = new Random().Next(0, 1 << 16);

            Listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            Listener.Start();

            Thread t = new Thread(async () =>
            {
                Task<TcpClient> task = Listener.AcceptTcpClientAsync();

                string background = "";

                if (File.Exists("blue-telephone-d.exe") == true)
                {
                    background = "blue-telephone-d.exe";
                }
                if (File.Exists("../../../../background/blue-telephone-d.exe") == true)
                {
                    background = "../../../../background/blue-telephone-d.exe";
                }

                Process = Process.Start(new ProcessStartInfo()
                {
                    FileName = background,
                    Arguments = $"--group {GroupTextBox.Text} --name {NameTextBox.Text} --port {port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });

                Client = await task;

                try
                {
                    while (true)
                    {
                        byte[] buffer = new byte[1024];
                        Client.GetStream().Read(buffer, 0, 1024);

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
                                MessageBox.Show(string.Join("\n", packet.Msg), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Close();

                                break;

                            case MsgCode.DeniedError:
                                MessageBox.Show(string.Join("\n", packet.Msg), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                                break;

                            case MsgCode.Success:

                                break;

                            case MsgCode.CreateHost:
                                Multiaddrs = packet.Msg[0..(packet.Msg.Count - 1)];
                                Multiaddrs.Add("");
                                PeerID = packet.Msg.Last();

                                InfoTextBox!.Invoke(() => InfoTextBox!.Text = string.Join($"/{PeerID}\n", Multiaddrs).TrimEnd('\n'));

                                break;

                            case MsgCode.FoundPeer:
                                Closers.Add(packet.Msg);
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Add(packet.Msg));

                                break;

                            case MsgCode.RemovePeer:
                                object closer = Closers.First(x => x[1] == packet.Msg.First());
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Remove(closer));

                                break;

                            case MsgCode.GotGossip:
                                foreach (GossipTabPage item in GossipTabControl!.TabPages)
                                {
                                    if (item.Text == packet.Msg[0])
                                    {
                                        List<string>? strs = Closers.FirstOrDefault(x => x[1] == packet.Msg[1]);

                                        if (strs == null)
                                        {
                                            item.Output.Invoke(() =>
                                            {
                                                item.Output.Text += "You: " + packet.Msg[2] + "\n";

                                                item.Output.SelectionStart = item.Output.Text.Length - 1;
                                                item.Output.ScrollToCaret();
                                            });
                                        }
                                        else
                                        {
                                            item.Output.Invoke(() =>
                                            {
                                                item.Output.Text += strs.Last() + " ("
                                                    + strs[1][0..15] + "...)" + ": " + packet.Msg[2] + "\n";

                                                item.Output.SelectionStart = item.Output.Text.Length - 1;
                                                item.Output.ScrollToCaret();
                                            });
                                        }

                                        break;
                                    }
                                }

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
        JoinGossip = 5,
        ExitGossip = 6,
        Publish = 7,
        GotGossip = 8,
    }
}
