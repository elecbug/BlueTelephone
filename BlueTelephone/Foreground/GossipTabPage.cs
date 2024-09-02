using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlueTelephone.Background;

namespace BlueTelephone.Foreground
{
    /// <summary>
    /// 채팅 페이지 하나
    /// </summary>
    internal class GossipTabPage : TabPage
    {
        /// <summary>
        /// 채팅창 전체가 출력되는 텍스트 박스
        /// </summary>
        public RichTextBox Output { get; private set; }
        /// <summary>
        /// 보낼 메시지를 입력하는 텍스트 박스
        /// </summary>
        public TextBox Input { get; private set; }
        /// <summary>
        /// 채팅방 제목
        /// </summary>
        public string Topic { get; private set; }
        /// <summary>
        /// 백그라운드용 TCP 클라이언트 
        /// </summary>
        public TcpClient Client { get; private set; }

        /// <summary>
        /// 새로운 채팅페이지를 등록
        /// </summary>
        /// <param name="client">백그라운드용 TCP 클라이언트</param>
        /// <param name="topic">채팅방 제목</param>
        public GossipTabPage(TcpClient client, string topic)
        {
            TableLayoutPanel panel = new TableLayoutPanel()
            {
                Parent = this,
                Visible = true,
                Dock = DockStyle.Fill,
            };

            Client = client;
            Topic = topic;

            panel.RowStyles.Add(new RowStyle() { Height = 1, SizeType = SizeType.Percent });
            panel.RowStyles.Add(new RowStyle() { Height = 30, SizeType = SizeType.Absolute });

            panel.ColumnStyles.Add(new ColumnStyle() { Width = 1, SizeType = SizeType.Percent });
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 30, SizeType = SizeType.Absolute });

            Input = new TextBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
            };
            Output = new RichTextBox()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
            };

            panel.Controls.Add(Input, 0, 1);
            panel.Controls.Add(Output, 0, 0);
            panel.SetColumnSpan(Output, 2);

            Button button = new Button()
            {
                Visible = true,
                Dock = DockStyle.Fill,
                Text = "↲",
            };

            panel.Controls.Add(button, 1, 1);

            button.Click += ButtonClick;
            Input.KeyPress += InputKeyPress;
        }

        /// <summary>
        /// 엔터치면 전송 버튼 누른것과 같음
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                ButtonClick(sender, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 토픽으로 메시지 전송 요청
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonClick(object? sender, EventArgs e)
        {
            if (Input.Text == "")
            {
                return;
            }

            string json = JsonSerializer.Serialize(new Packet()
            {
                TS = DateTime.Now.ToString(),
                MsgCode = (int)MsgCode.Publish,
                Msg = new List<string> { Topic, Input.Text }
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

            Client.GetStream().Write(buf);

            Input.Text = "";
        }
    }
}
