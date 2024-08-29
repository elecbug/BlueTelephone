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
        /// <summary>
        /// 랑데뷰 그룹명 입력하는 텍스트 박스
        /// </summary>
        private TextBox? GroupTextBox { get; set; }
        /// <summary>
        /// 닉네임 입력하는 텍스트 박스
        /// </summary>
        private TextBox? NameTextBox { get; set; }
        /// <summary>
        /// 채팅방 이름(Gossip Topic) 입력하는 텍스트 박스
        /// </summary>
        private TextBox? GossipTextBox { get; set; }
        /// <summary>
        /// 사용자 Peer ID 표시되는 텍스트 박스
        /// </summary>
        private RichTextBox? InfoTextBox { get; set; }
        /// <summary>
        /// 주위 피어 리스트 표시되는 박스
        /// </summary>
        private ListBox? PeerListBox { get; set; }
        /// <summary>
        /// 채팅방들이 표시되는 탭
        /// </summary>
        private TabControl? GossipTabControl { get; set; }
        /// <summary>
        /// libp2p 노드를 시작하는 연결 버튼
        /// </summary>
        private Button? ConnectButton { get; set; }
        /// <summary>
        /// 채팅방에 참여/이탈하는 Gossip 관련 버튼
        /// </summary>
        private Button? GossipButton { get; set; }

        /// <summary>
        /// 백그라운드 Go 프로세스와 연결되는 (blue-telephone-d.exe) TCP 리스너
        /// </summary>
        private TcpListener? Listener { get; set; }
        /// <summary>
        /// 백그라운드 Go 프로세스와 연결되는 (blue-telephone-d.exe) TCP 클라이언트
        /// </summary>
        private TcpClient? Client { get; set; }
        /// <summary>
        /// 백그라운드 Go 프로세스(blue-telephone-d.exe)
        /// </summary>
        private Process? Process { get; set; }

        /// <summary>
        /// 자신의 Muliaddr 목록
        /// * Peer ID는 포함되지 않으며, 마지막은 편의를 위한 빈 문자열이 하나 포함 됨
        /// </summary>
        private List<string> Multiaddrs { get; set; } = new List<string>();
        /// <summary>
        /// 자신의 libp2p Peer ID
        /// </summary>
        private string PeerID { get; set; } = "";

        /// <summary>
        /// 발견한 주위 피어 목록, 내부 구조는 {Multiaddr, Peer ID, Peer Name} 순서
        /// </summary>
        private List<List<string>> Closers { get; set; } = new List<List<string>>();

        /// <summary>
        /// 디자이너 호출
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            Designer();
        }

        /// <summary>
        /// 폼의 형태를 정의하는 디자이너
        /// </summary>
        public void Designer()
        {
            /// 폼이 닫힐 때 순서
            /// TCP 닫기 -> EOF 발생 -> 아래에서 별도 처리 -> 백그라운드 종료
            FormClosing += (s, e) =>
            {
                Client?.Close();
                Process?.Kill();
            };

            /// 메인 컨트롤들이 들어가는 패널 정의
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

            /// 아래는 패널에 컨트롤들 추가
            /// 필드로 선언할 필요 없는 컨트롤은 즉시 추가

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

            /// 피어 리스트 포맷 재정의
            /// [PEER_NAME] ([PEER_ID 15자리]...)으로 나옴
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

        /// <summary>
        /// Gossip 채팅방 이름 입력창 텍스트 변경 시 발생
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GossipTextBoxTextChanged(object? sender, EventArgs e)
        {
            /// 아무것도 입력되어 있지 않다면 표시
            if (GossipTextBox!.Text == "")
            {
                GossipButton!.Text = "Join/Exit";
            }
            /// 아닐 시, 즉 뭔가 입력되어 있다면, 이미 참여해 있는 채팅방이라면 Exit를, 아니라면 Join이 자연스럽게 변경됨
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
        /// 채팅방 참여/이탈 버튼 클릭시 발생
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GossipButtonClick(object? sender, EventArgs e)
        {
            /// 텍스트 창이 비어있다면 아무일도 없음
            if (GossipTextBox!.Text == "")
            {
                return;
            }

            foreach (TabPage page in GossipTabControl!.TabPages)
            {
                /// 찾아 봤는데 이미 참여해 있는 채팅방이라면 이탈 프로세스가 작동
                if (page.Text == GossipTextBox!.Text)
                {
                    /// 나갈건지 물어보고 Yes를 선택 시
                    if (MessageBox.Show("Exits the room?", "Warning", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        /// 백그라운드로 Gossip 이탈 명령을 전달하고
                        string json1 = JsonSerializer.Serialize(new Packet()
                        {
                            TS = DateTime.Now.ToString(),
                            MsgCode = (int)MsgCode.ExitGossip,
                            Msg = new List<string> { page.Text }
                        });

                        byte[] data1 = Encoding.UTF8.GetBytes(json1);
                        byte[] buf1 = new byte[1024];

                        using (StreamWriter sw = new StreamWriter(".log", true))
                        {
                            sw.WriteLine("W: " + json1);
                        }

                        for (int i = 0; i < data1.Length; i++)
                        {
                            buf1[i] = data1[i];
                        }

                        Client!.GetStream().Write(buf1);

                        /// 텍스트 박스를 비우고 페이지를 제거
                        GossipTextBox!.Text = "";
                        GossipTabControl!.TabPages.Remove(page);
                    }

                    /// 제거 프로세스가 작동한 상황 시 종료
                    return;
                }
            }

            /// 그렇지 않다면, 즉 새로 가입하려는 것
            /// Gossip 가입 메시지를 백그라운드로 전달
            string json = JsonSerializer.Serialize(new Packet()
            {
                TS = DateTime.Now.ToString(),
                MsgCode = (int)MsgCode.JoinGossip,
                Msg = new List<string> { GossipTextBox!.Text }
            });

            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] buf = new byte[1024];

            using (StreamWriter sw = new StreamWriter(".log", true))
            {
                sw.WriteLine("W: " + json);
            }

            for (int i = 0; i < data.Length; i++)
            {
                buf[i] = data[i];
            }

            Client!.GetStream().Write(buf);

            /// 채팅 페이지도 추가
            GossipTabControl!.TabPages.Add(new GossipTabPage(Client, GossipTextBox!.Text)
            {
                Text = GossipTextBox!.Text,
            });

            GossipTabControl!.SelectTab(GossipTabControl!.TabPages.Count - 1);

            /// 역시 텍스트 박스는 비움
            GossipTextBox!.Text = "";
        }

        /// <summary>
        /// libp2p 연결 버튼 클릭 시 발생
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectButtonClick(object? sender, EventArgs e)
        {
            /// Group/Name 박스 등에 입력이 없다면 기본 값 삽입
            if (GroupTextBox?.Text == "")
            {
                GroupTextBox.Text = "default";
            }
            if (NameTextBox?.Text == "")
            {
                NameTextBox.Text = $"BT-{new Random().Next()}";
            }

            /// 연결 설정 관련 컨트롤들은 잠금
            ConnectButton!.Enabled = false;
            GroupTextBox!.Enabled = false;
            NameTextBox!.Enabled = false;

            /// 채팅 관련 컨트롤들을 해방
            GossipTextBox!.Enabled = true;
            GossipButton!.Enabled = true;

            /// 포트 설정하고 루프백 리스너 설정
            int port = new Random().Next(3000, 1 << 16);

            Listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            Listener.Start();

            /// 백그라운드 프로세스를 위한 스레드 작동
            Thread t = new Thread(async () =>
            {
                /// 이 시점에서 백그라운드 작동하지 않음
                /// 비동기로 만들고 아래에서 확인
                Task<TcpClient> task = Listener.AcceptTcpClientAsync();

                string background = "";

                /// 배포용과 개발 상태용 백그라운드 프로세스 검색
                /// 개발 상태용이 우선시 됨
                if (File.Exists("blue-telephone-d.exe") == true)
                {
                    background = "blue-telephone-d.exe";
                }
                if (File.Exists("../../../../background/blue-telephone-d.exe") == true)
                {
                    background = "../../../../background/blue-telephone-d.exe";
                }

                /// 백그라운드 프로세스 시작
                /// 그룹, 이름, 포트를 넘겨줌
                Process = Process.Start(new ProcessStartInfo()
                {
                    FileName = background,
                    Arguments = $"--group {GroupTextBox.Text} --name {NameTextBox.Text} --port {port}",
                    CreateNoWindow = true,
                });

                /// 아까 만든 연결 승인을 확인
                /// 이 시점에서 백그라운드는 이쪽으로 연결을 시도 중
                /// 따라서 승인되며, 즉시 두 프로세스는 연결됨
                Client = await task;

                /// catch 됨 == 백그라운드 사망
                /// 따라서 심각한 오류 -> 해당 프로그램 종료
                try
                {
                    while (true)
                    {
                        /// 백그라운드에서 패킷 받고 분석
                        byte[] buffer = new byte[1024];
                        Client.GetStream().Read(buffer, 0, 1024);

                        string json = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                        using (StreamWriter sw = new StreamWriter(".log", true))
                        {
                            sw.WriteLine("R: " + json);
                        }

                        Debug.WriteLine(json);

                        Packet? packet = JsonSerializer.Deserialize<Packet>(json);

                        /// 못읽겠으면 일단 넘김
                        if (packet == null)
                        {
                            continue;
                        }

                        /// 메시지 코드에 따라서 분석
                        switch ((MsgCode)packet.MsgCode)
                        {
                            /// 심각한 에러면 경고 후 종료
                            /// 해당 에러 발생시 이미 백그라운드는 높은 확률로 종료
                            case MsgCode.PanicError:
                                MessageBox.Show(string.Join("\n", packet.Msg), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Close();

                                break;

                            /// 살릴만한 에러면 경고만 표시
                            /// 백그라운드는 사망하지 않음
                            case MsgCode.DeniedError:
                                /// 예외적으로 TCP가 끊어질 때 발생하는 EOF 에러의 경우
                                /// 즉, 프로그램이 꺼질 때 한번 발생하는 에러는 경고음만 발생시키므로 그냥 함수 종료
                                if (packet.Msg[0] == "EOF")
                                {
                                    return;
                                }

                                MessageBox.Show(string.Join("\n", packet.Msg), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                                break;

                            /// 여기서 백그라운드로 뭔가 명령하면 백그라운드가 보내는 성공 메시지
                            /// 이것도 연계해서 성공 여부 판단하는 로직이 필요
                            case MsgCode.Success:

                                break;

                            /// 백그라운드가 libp2p 호스트를 생성하고 받는 메시지
                            /// Msg 구조: {Multiaddr[0], Multiaddr[1], ... Multiaddr[n], Peer ID}
                            case MsgCode.CreateHost:
                                Multiaddrs = packet.Msg[0..(packet.Msg.Count - 1)];
                                Multiaddrs.Add("");
                                PeerID = packet.Msg.Last();

                                /// Invoke로 정보 창을 변경해야 함
                                InfoTextBox!.Invoke(() => InfoTextBox!.Text = string.Join($"/{PeerID}\n", Multiaddrs).TrimEnd('\n'));

                                break;

                            /// 백그라운드가 libp2p Peer를 찾았을 때 받는 메시지
                            /// Msg 구조: {Multiaddr, Peer ID, Peer Name}
                            case MsgCode.FoundPeer:
                                Closers.Add(packet.Msg);
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Add(packet.Msg));

                                break;

                            /// 백그라운드가 libp2p Peer의 종료를 확인하고 삭제를 명령하는 메시지
                            /// Msg 구조: {Peer ID}
                            case MsgCode.RemovePeer:
                                object closer = Closers.First(x => x[1] == packet.Msg.First());
                                PeerListBox!.Invoke(() => PeerListBox!.Items.Remove(closer));

                                break;

                            /// 백그라운드가 Gossip Topic으로부터 메시지를 받았을 때 받는 메시지
                            /// Msg 구조: {Topic, Peer ID, Msg}
                            case MsgCode.GotGossip:
                                /// 참여중인 Topic 찾아서
                                foreach (GossipTabPage item in GossipTabControl!.TabPages)
                                {
                                    if (item.Text == packet.Msg[0])
                                    {
                                        /// 해당 피어에 대한 정보를 얻고
                                        List<string>? strs = Closers.FirstOrDefault(x => x[1] == packet.Msg[1]);

                                        /// 없다면, 즉 송신자가 본인이라면
                                        if (strs == null)
                                        {
                                            item.Output.Invoke(() =>
                                            {
                                                /// 채팅창에 나로 추가
                                                item.Output.Text += "You: " + packet.Msg[2] + "\n";

                                                item.Output.SelectionStart = item.Output.Text.Length - 1;
                                                item.Output.ScrollToCaret();
                                            });
                                        }
                                        /// 아니라면, 즉 알고 있는 Peer 라면
                                        else
                                        {
                                            item.Output.Invoke(() =>
                                            {
                                                /// 채팅창에 기존 PeerListBox 포맷과 같은 형태로 추가
                                                item.Output.Text += strs.Last() + " ("
                                                    + strs[1][0..15] + "...)" + ": " + packet.Msg[2] + "\n";

                                                item.Output.SelectionStart = item.Output.Text.Length - 1;
                                                item.Output.ScrollToCaret();
                                            });
                                        }

                                        /// 한 채팅방에 추가했으면 종료
                                        break;
                                    }
                                }

                                break;
                        }
                    }
                }
                /// 심각한 에러 발생 -> 프로세스 종료
                catch (Exception ex) 
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                }
            });

            t.Start();
        }
    }

    /// <summary>
    /// 패킷 형태
    /// </summary>
    public class Packet
    {
        /// <summary>
        /// 타임스탬프,
        /// 활용 방안은 미정
        /// </summary>
        public required string TS { get; set; }
        /// <summary>
        /// 메시지 코드,
        /// 백그라운드와 공유
        /// </summary>
        public required int MsgCode { get; set; }
        /// <summary>
        /// 메시지,
        /// 문자열의 리스트로 이루어짐
        /// </summary>
        public required List<string> Msg { get; set; }
    }

    /// <summary>
    /// 메시지 코드
    /// </summary>
    public enum MsgCode
    {
        /// <summary>
        /// 이건 못살리는 에러(수)
        /// </summary>
        PanicError = -1,
        /// <summary>
        /// 살릴만한 에러(수)
        /// </summary>
        DeniedError = 0,
        /// <summary>
        /// 백그라운드로 전달한 명령 성공(수)
        /// </summary>
        Success = 1,
        /// <summary>
        /// 호스트가 생성되었음(수)
        /// </summary>
        CreateHost = 2,
        /// <summary>
        /// 주위 피어를 찾았음(수)
        /// </summary>
        FoundPeer = 3,
        /// <summary>
        /// 주위 피어가 사라짐(수)
        /// </summary>
        RemovePeer = 4,
        /// <summary>
        /// 채팅방에 참여하고 싶음(송)
        /// </summary>
        JoinGossip = 5,
        /// <summary>
        /// 채팅방에서 나가고 싶음(송)
        /// </summary>
        ExitGossip = 6,
        /// <summary>
        /// 메시지를 게시하고 싶음(송)
        /// </summary>
        Publish = 7,
        /// <summary>
        /// 메시지가 왔음(수)
        /// </summary>
        GotGossip = 8,
    }
}
