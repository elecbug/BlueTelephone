using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BlueTelephone.Background;
using BlueTelephone.Foreground;

namespace BlueTelephone.Foreground
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// ������ �׷�� �Է��ϴ� �ؽ�Ʈ �ڽ�
        /// </summary>
        private TextBox? GroupTextBox { get; set; }
        /// <summary>
        /// �г��� �Է��ϴ� �ؽ�Ʈ �ڽ�
        /// </summary>
        private TextBox? NameTextBox { get; set; }
        /// <summary>
        /// ä�ù� �̸�(Gossip Topic) �Է��ϴ� �ؽ�Ʈ �ڽ�
        /// </summary>
        private TextBox? GossipTextBox { get; set; }
        /// <summary>
        /// ����� Peer ID ǥ�õǴ� �ؽ�Ʈ �ڽ�
        /// </summary>
        private RichTextBox? InfoTextBox { get; set; }
        /// <summary>
        /// ���� �Ǿ� ����Ʈ ǥ�õǴ� �ڽ�
        /// </summary>
        private ListBox? PeerListBox { get; set; }
        /// <summary>
        /// ä�ù���� ǥ�õǴ� ��
        /// </summary>
        private TabControl? GossipTabControl { get; set; }
        /// <summary>
        /// libp2p ��带 �����ϴ� ���� ��ư
        /// </summary>
        private Button? ConnectButton { get; set; }
        /// <summary>
        /// ä�ù濡 ����/��Ż�ϴ� Gossip ���� ��ư
        /// </summary>
        private Button? GossipButton { get; set; }
        /// <summary>
        /// ���� Multiaddr ��� �Ǿ� �˻��� �����ϴ� �ؽ�Ʈ �ڽ�
        /// </summary>
        private TextBox? PeerFoundTextBox { get; set; }
        /// <summary>
        /// ���� �Ǿ� �˻��� ���� ��ư
        /// </summary>
        private Button? PeerFoundButton { get; set; }

        /// <summary>
        /// ��׶��� Go ���μ����� ����Ǵ� (blue-telephone-d.exe) TCP ������
        /// </summary>
        private TcpListener? Listener { get; set; }
        /// <summary>
        /// ��׶��� Go ���μ����� ����Ǵ� (blue-telephone-d.exe) TCP Ŭ���̾�Ʈ
        /// </summary>
        private TcpClient? Client { get; set; }
        /// <summary>
        /// ��׶��� Go ���μ���(blue-telephone-d.exe)
        /// </summary>
        private Process? Process { get; set; }

        /// <summary>
        /// �ڽ��� Muliaddr ���
        /// * Peer ID�� ���Ե��� ������, �������� ���Ǹ� ���� �� ���ڿ��� �ϳ� ���� ��
        /// </summary>
        private List<string> Multiaddrs { get; set; } = new List<string>();
        /// <summary>
        /// �ڽ��� libp2p Peer ID
        /// </summary>
        private string PeerID { get; set; } = "";

        /// <summary>
        /// �߰��� ���� �Ǿ� ���, ���� ������ {Multiaddr, Peer ID, Peer Name} ����
        /// </summary>
        private List<List<string>> Closers { get; set; } = new List<List<string>>();

        /// <summary>
        /// �׷�� �̸��� ����� �� �⺻ �ɼ����� ����ߴ��� ����
        /// </summary>
        private (bool, bool) UsingDefaultSetting { get; set; } = (false, false);

        /// <summary>
        /// �����̳� ȣ��
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            Designer();
            UserSetting();
        }

        /// <summary>
        /// ���� ������ �ҷ����� �޼���
        /// </summary>
        private void UserSetting()
        {
            GroupTextBox!.Text = User.Default.DefaultGroup;
            NameTextBox!.Text = User.Default.DefaultName;
        }

        /// <summary>
        /// ���� ���¸� �����ϴ� �����̳�
        /// </summary>
        private void Designer()
        {
            FormClosing += MainFormClosing;

            /// ��ü ���� �г� ����
            TabControl container = new TabControl()
            {
                Parent = this,
                Visible = true,
                Dock = DockStyle.Fill,
            };

            {
                TabPage page = new TabPage()
                {
                    Text = "Login",
                };

                container.TabPages.Add(page);

                /// �α��� ��Ʈ�ѵ��� ���� �г� ����
                TableLayoutPanel panel = new TableLayoutPanel()
                {
                    Parent = page,
                    Visible = true,
                    Dock = DockStyle.Fill,
                };

                panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
                panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
                panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
                panel.RowStyles.Add(new RowStyle() { Height = 70, SizeType = SizeType.Absolute });

                panel.ColumnStyles.Add(new ColumnStyle() { Width = 50, SizeType = SizeType.Absolute });
                panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
                panel.ColumnStyles.Add(new ColumnStyle() { Width = 80, SizeType = SizeType.Absolute });

                /// �Ʒ��� �гο� ��Ʈ�ѵ� �߰�
                /// �ʵ�� ������ �ʿ� ���� ��Ʈ���� ��� �߰�

                Label mainLabel = new Label()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Login",
                    TextAlign = ContentAlignment.MiddleCenter,
                };

                panel.Controls.Add(mainLabel, 0, 0);

                panel.SetColumnSpan(mainLabel, 3);

                panel.Controls.Add(new Label()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Group",
                    TextAlign = ContentAlignment.MiddleRight,
                }, 0, 1);

                panel.Controls.Add(new Label()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Name",
                    TextAlign = ContentAlignment.MiddleRight,
                }, 0, 2);

                panel.Controls.Add(new Label()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Info",
                    TextAlign = ContentAlignment.MiddleRight,
                }, 0, 3);

                panel.Controls.Add(GroupTextBox = new TextBox()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                }, 1, 1);

                panel.Controls.Add(NameTextBox = new TextBox()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                }, 1, 2);

                panel.Controls.Add(InfoTextBox = new RichTextBox()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    WordWrap = false,
                }, 1, 3);

                panel.SetColumnSpan(InfoTextBox, 2);

                panel.Controls.Add(ConnectButton = new Button()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Connect",
                }, 2, 1);

                panel.SetRowSpan(ConnectButton, 2);

                ConnectButton.Click += ConnectButtonClick;
            }

            {
                TabPage page = new TabPage()
                {
                    Text = "Peers",
                };

                container.TabPages.Add(page);

                /// ģ��â ��Ʈ�ѵ��� ���� �г� ����
                TableLayoutPanel panel = new TableLayoutPanel()
                {
                    Parent = page,
                    Visible = true,
                    Dock = DockStyle.Fill,
                };

                panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
                panel.RowStyles.Add(new RowStyle() { Height = 1, SizeType = SizeType.Percent });
                panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });

                panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
                panel.ColumnStyles.Add(new ColumnStyle() { Width = 70, SizeType = SizeType.Absolute });

                Label peerLabel = new Label()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Found Peers",
                    TextAlign = ContentAlignment.MiddleCenter,
                };

                panel.Controls.Add(peerLabel, 0, 0);
                panel.SetColumnSpan(peerLabel, 2);

                panel.Controls.Add(PeerListBox = new ListBox()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    FormattingEnabled = true,
                }, 0, 1);

                panel.SetColumnSpan(PeerListBox, 2);

                /// �Ǿ� ����Ʈ ���� ������
                /// [PEER_NAME] ([PEER_ID 15�ڸ�]...)���� ����
                PeerListBox.Format += (s, e) =>
                {
                    List<string> strs = (e.ListItem as List<string>)!;
                    e.Value = strs.Last() + " (" + strs[1][0..15] + "...)";
                };

                panel.Controls.Add(PeerFoundTextBox = new TextBox()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Enabled = false,
                }, 0, 2);


                panel.Controls.Add(PeerFoundButton = new Button()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Enabled = false,
                    Text = "Dial"
                }, 1, 2);

                PeerFoundTextBox.KeyPress += PeerFoundTextBoxKeyPress;
                PeerFoundButton.Click += PeerFoundButtonClick;
            }

            {
                TabPage page = new TabPage()
                {
                    Text = "Chatting",
                };

                container.TabPages.Add(page);

                /// ���� ��Ʈ�ѵ��� ���� �г� ����
                TableLayoutPanel panel = new TableLayoutPanel()
                {
                    Parent = page,
                    Visible = true,
                    Dock = DockStyle.Fill,
                };

                panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });
                panel.RowStyles.Add(new RowStyle() { Height = 1, SizeType = SizeType.Percent });
                panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });

                panel.ColumnStyles.Add(new ColumnStyle() { Width = 50, SizeType = SizeType.Absolute });
                panel.ColumnStyles.Add(new ColumnStyle() { Width = 6, SizeType = SizeType.Percent });
                panel.ColumnStyles.Add(new ColumnStyle() { Width = 100, SizeType = SizeType.Absolute });

                Label mainLabel = new Label()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Chatting",
                    TextAlign = ContentAlignment.MiddleCenter,
                };

                panel.Controls.Add(mainLabel, 0, 0);

                panel.SetColumnSpan(mainLabel, 3);

                panel.Controls.Add(GossipTabControl = new TabControl()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                }, 0, 1);

                panel.SetColumnSpan(GossipTabControl, 3);

                panel.Controls.Add(new Label()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Room",
                    TextAlign = ContentAlignment.MiddleCenter,
                }, 0, 2);

                panel.Controls.Add(GossipTextBox = new TextBox()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Enabled = false,
                }, 1, 2);

                GossipTextBox.TextChanged += GossipTextBoxTextChanged;
                GossipTextBox.KeyPress += GossipTextBoxKeyPress;

                panel.Controls.Add(GossipButton = new Button()
                {
                    Visible = true,
                    Dock = DockStyle.Fill,
                    Text = "Join/Exit",
                    Enabled = false,
                }, 2, 2);

                GossipButton.Click += GossipButtonClick;
            }
        }

        /// <summary>
        /// �� ����� ����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainFormClosing(object? sender, EventArgs e)
        {
            /// ���� ���� �� ����
            /// TCP �ݱ� -> EOF �߻� -> �Ʒ����� ���� ó�� -> ��׶��� ����
            
            /// ���� ���� ����
            if (UsingDefaultSetting.Item1 == false)
                User.Default.DefaultGroup = GroupTextBox!.Text;
            else
                User.Default.DefaultGroup = "";
            if (UsingDefaultSetting.Item2 == false)
                User.Default.DefaultName = NameTextBox!.Text;
            else
                User.Default.DefaultName = "";

            User.Default.Save();

            Client?.Close();
            Process?.Kill();
        }

        /// <summary>
        /// ä�ù� �������� ���� �Է½� ��ư Ŭ���� ������ ȿ��
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GossipTextBoxKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                GossipButtonClick(sender, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// ���� �Ǿ�� Ŀ�ؼ� ������ ����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PeerFoundButtonClick(object? sender, EventArgs e)
        {
            /// ��׶���� ������ �����ϰ�
            string json = JsonSerializer.Serialize(new Packet()
            {
                TS = DateTime.Now.ToString(),
                MsgCode = (int)MsgCode.PlzFindPeer,
                Msg = new List<string> { PeerFoundTextBox!.Text },
            });

            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] buf = new byte[1024];

            using (StreamWriter sw = new StreamWriter("log", true))
            {
                sw.WriteLine("F: " + json);
            }

            for (int i = 0; i < data.Length; i++)
            {
                buf[i] = data[i];
            }

            Client!.GetStream().Write(buf);

            /// �ؽ�Ʈ �ڽ��� ���� �������� ����
            PeerFoundTextBox!.Text = "";
        }

        /// <summary>
        /// �Ǿ� ã�� �ڽ����� ���� �Է½� ��ư Ŭ���� ������ ȿ��
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PeerFoundTextBoxKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                PeerFoundButtonClick(sender, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Gossip ä�ù� �̸� �Է�â �ؽ�Ʈ ���� �� �߻�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GossipTextBoxTextChanged(object? sender, EventArgs e)
        {
            /// �ƹ��͵� �ԷµǾ� ���� �ʴٸ� ǥ��
            if (GossipTextBox!.Text == "")
            {
                GossipButton!.Text = "Join/Exit";
            }
            /// �ƴ� ��, �� ���� �ԷµǾ� �ִٸ�, �̹� ������ �ִ� ä�ù��̶�� Exit��, �ƴ϶�� Join�� �ڿ������� �����
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

        /// <summary>
        /// ä�ù� ����/��Ż ��ư Ŭ���� �߻�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GossipButtonClick(object? sender, EventArgs e)
        {
            /// �ؽ�Ʈ â�� ����ִٸ� �ƹ��ϵ� ����
            if (GossipTextBox!.Text == "")
            {
                return;
            }

            foreach (TabPage page in GossipTabControl!.TabPages)
            {
                /// ã�� �ôµ� �̹� ������ �ִ� ä�ù��̶�� ��Ż ���μ����� �۵�
                if (page.Text == GossipTextBox!.Text)
                {
                    /// �������� ����� Yes�� ���� ��
                    if (MessageBox.Show("Exits the room?", "Warning", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        /// ��׶���� Gossip ��Ż ������ �����ϰ�
                        string json1 = JsonSerializer.Serialize(new Packet()
                        {
                            TS = DateTime.Now.ToString(),
                            MsgCode = (int)MsgCode.ExitGossip,
                            Msg = new List<string> { page.Text }
                        });

                        byte[] data1 = Encoding.UTF8.GetBytes(json1);
                        byte[] buf1 = new byte[1024];

                        using (StreamWriter sw = new StreamWriter("log", true))
                        {
                            sw.WriteLine("F: " + json1);
                        }

                        for (int i = 0; i < data1.Length; i++)
                        {
                            buf1[i] = data1[i];
                        }

                        Client!.GetStream().Write(buf1);

                        /// �ؽ�Ʈ �ڽ��� ���� �������� ����
                        GossipTextBox!.Text = "";
                        GossipTabControl!.TabPages.Remove(page);
                    }

                    /// ���� ���μ����� �۵��� ��Ȳ �� ����
                    return;
                }
            }

            /// �׷��� �ʴٸ�, �� ���� �����Ϸ��� ��
            /// Gossip ���� �޽����� ��׶���� ����
            string json = JsonSerializer.Serialize(new Packet()
            {
                TS = DateTime.Now.ToString(),
                MsgCode = (int)MsgCode.JoinGossip,
                Msg = new List<string> { GossipTextBox!.Text }
            });

            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] buf = new byte[1024];

            using (StreamWriter sw = new StreamWriter("log", true))
            {
                sw.WriteLine("F: " + json);
            }

            for (int i = 0; i < data.Length; i++)
            {
                buf[i] = data[i];
            }

            Client!.GetStream().Write(buf);

            GossipTabPage gossipTabPage = new GossipTabPage(Client, GossipTextBox!.Text)
            {
                Text = GossipTextBox!.Text,
            };

            /// ä�� �������� �߰�
            GossipTabControl!.TabPages.Add(gossipTabPage);
            GossipTabControl!.SelectTab(GossipTabControl!.TabPages.Count - 1);

            gossipTabPage.Input.Focus();

            /// ���� �ؽ�Ʈ �ڽ��� ���
            GossipTextBox!.Text = "";
        }

        /// <summary>
        /// libp2p ���� ��ư Ŭ�� �� �߻�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectButtonClick(object? sender, EventArgs e)
        {
            /// Group/Name �ڽ� � �Է��� ���ٸ� �⺻ �� ����
            /// �⺻ ������ ����ߴٸ� �÷��� �س��� User ���ÿ� ���� X
            if (GroupTextBox?.Text == "")
            {
                GroupTextBox.Text = "default";
                UsingDefaultSetting = (true, UsingDefaultSetting.Item2);
            }
            if (NameTextBox?.Text == "")
            {
                NameTextBox.Text = $"BT-{new Random().Next()}";
                UsingDefaultSetting = (UsingDefaultSetting.Item1, true);
            }

            /// ���� ���� ���� ��Ʈ�ѵ��� ���
            ConnectButton!.Enabled = false;
            GroupTextBox!.Enabled = false;
            NameTextBox!.Enabled = false;

            /// ä�� ���� ��Ʈ�ѵ��� �ع�
            GossipTextBox!.Enabled = true;
            GossipButton!.Enabled = true;
            PeerFoundTextBox!.Enabled = true;
            PeerFoundButton!.Enabled = true;

            /// ��Ʈ �����ϰ� ������ ������ ����
            int port = new Random().Next(3000, 1 << 16);

            Listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            Listener.Start();

            /// ��׶��� ���μ����� ���� ������ �۵�
            Thread t = new Thread(async () =>
            {
                /// �� �������� ��׶��� �۵����� ����
                /// �񵿱�� ����� �Ʒ����� Ȯ��
                Task<TcpClient> task = Listener.AcceptTcpClientAsync();

                string background = "";

                /// ������� ���� ���¿� ��׶��� ���μ��� �˻�
                /// ���� ���¿��� �켱�� ��
                if (File.Exists("blue-telephone-d.exe") == true)
                {
                    background = "blue-telephone-d.exe";
                }
                if (File.Exists("../../../../background/blue-telephone-d.exe") == true)
                {
                    background = "../../../../background/blue-telephone-d.exe";
                }

                /// ��׶��� ���μ��� ����
                /// �׷�, �̸�, ��Ʈ�� �Ѱ���
                Process = Process.Start(new ProcessStartInfo()
                {
                    FileName = background,
                    Arguments = $"--group {GroupTextBox.Text} --name {NameTextBox.Text} --port {port}",
                    CreateNoWindow = true,
                });

                /// �Ʊ� ���� ���� ������ Ȯ��
                /// �� �������� ��׶���� �������� ������ �õ� ��
                /// ���� ���εǸ�, ��� �� ���μ����� �����
                Client = await task;

                /// catch �� == ��׶��� ���
                /// ���� �ɰ��� ���� -> �ش� ���α׷� ����
                try
                {
                    while (true)
                    {
                        /// ��׶��忡�� ��Ŷ �ް� �м�
                        byte[] buffer = new byte[1024];
                        Client.GetStream().Read(buffer, 0, 1024);

                        string json = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                        using (StreamWriter sw = new StreamWriter("log", true))
                        {
                            sw.WriteLine("B: " + json);
                        }

                        Debug.WriteLine(json);

                        Packet? packet = JsonSerializer.Deserialize<Packet>(json);

                        /// ���а����� �ϴ� �ѱ�
                        if (packet == null)
                        {
                            continue;
                        }

                        /// �޽��� �ڵ忡 ���� �м�
                        switch ((MsgCode)packet.MsgCode)
                        {
                            /// �ɰ��� ������ ��� �� ����
                            /// �ش� ���� �߻��� �̹� ��׶���� ���� Ȯ���� ����
                            case MsgCode.PanicError:
                                MessageBox.Show(string.Join("\n", packet.Msg), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Close();

                                break;

                            /// �츱���� ������ ����� ǥ��
                            /// ��׶���� ������� ����
                            case MsgCode.GoodError:
                                /// ���������� TCP�� ������ �� �߻��ϴ� EOF ������ ���
                                /// ��, ���α׷��� ���� �� �ѹ� �߻��ϴ� ������ ������� �߻���Ű�Ƿ� �׳� �Լ� ����
                                if (packet.Msg[0] == "EOF")
                                {
                                    return;
                                }

                                MessageBox.Show(string.Join("\n", packet.Msg), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                                break;

                            /// ���⼭ ��׶���� ���� �����ϸ� ��׶��尡 ������ ���� �޽���
                            /// �̰͵� �����ؼ� ���� ���� �Ǵ��ϴ� ������ �ʿ�
                            case MsgCode.Success:

                                break;

                            /// ��׶��尡 libp2p ȣ��Ʈ�� �����ϰ� �޴� �޽���
                            /// Msg ����: {Multiaddr[0], Multiaddr[1], ... Multiaddr[n], Peer ID}
                            case MsgCode.CreateHost:
                                Multiaddrs = packet.Msg[0..(packet.Msg.Count - 1)];
                                Multiaddrs.Add("");
                                PeerID = packet.Msg.Last();

                                /// Invoke�� ���� â�� �����ؾ� ��
                                InfoTextBox!.Invoke(() => InfoTextBox!.Text = string.Join($"/{PeerID}\n", Multiaddrs).TrimEnd('\n'));

                                break;

                            /// ��׶��尡 libp2p Peer�� ã���� �� �޴� �޽���
                            /// Msg ����: {Multiaddr, Peer ID, Peer Name}
                            case MsgCode.FoundPeer:
                                Closers.Add(packet.Msg);
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Add(packet.Msg));

                                break;

                            /// ��׶��尡 libp2p Peer�� ���Ḧ Ȯ���ϰ� ������ �����ϴ� �޽���
                            /// Msg ����: {Peer ID}
                            case MsgCode.RemovePeer:
                                object closer = Closers.First(x => x[1] == packet.Msg.First());
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Remove(closer));

                                break;

                            /// ��׶��尡 Gossip Topic���κ��� �޽����� �޾��� �� �޴� �޽���
                            /// Msg ����: {Topic, Peer ID, Msg}
                            case MsgCode.GotGossip:
                                /// �������� Topic ã�Ƽ�
                                foreach (GossipTabPage item in GossipTabControl!.TabPages)
                                {
                                    if (item.Text == packet.Msg[0])
                                    {
                                        /// �ش� �Ǿ ���� ������ ���
                                        List<string>? strs = Closers.FirstOrDefault(x => x[1] == packet.Msg[1]);

                                        /// ���ٸ�, �� �۽��ڰ� �����̶��
                                        if (strs == null)
                                        {
                                            item.Output.Invoke(() =>
                                            {
                                                /// ä��â�� ���� �߰�
                                                item.Output.Text += "You: " + packet.Msg[2] + "\n";

                                                item.Output.SelectionStart = item.Output.Text.Length - 1;
                                                item.Output.ScrollToCaret();
                                            });
                                        }
                                        /// �ƴ϶��, �� �˰� �ִ� Peer ���
                                        else
                                        {
                                            item.Output.Invoke(() =>
                                            {
                                                /// ä��â�� ���� PeerListBox ���˰� ���� ���·� �߰�
                                                item.Output.Text += strs.Last() + " ("
                                                    + strs[1][0..15] + "...)" + ": " + packet.Msg[2] + "\n";

                                                item.Output.SelectionStart = item.Output.Text.Length - 1;
                                                item.Output.ScrollToCaret();
                                            });
                                        }

                                        /// �� ä�ù濡 �߰������� ����
                                        break;
                                    }
                                }

                                break;
                        }
                    }
                }
                /// �ɰ��� ���� �߻� -> ���μ��� ����
                catch (Exception ex) 
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                }
            });

            t.Start();
        }
    }
}