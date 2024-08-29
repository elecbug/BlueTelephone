using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlueTelephone
{
    internal class GossipTabPage : TabPage
    {
        public RichTextBox Output { get; private set; }
        public TextBox Input { get; private set; }
        public string Topic { get; private set; }
        public TcpClient Client { get; private set; }

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
            panel.ColumnStyles.Add(new ColumnStyle() { Width = 30, SizeType = SizeType.Absolute});

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

        private void InputKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                ButtonClick(sender, e);
                e.Handled = true;
            }
        }

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

            using (StreamWriter sw = new StreamWriter(".log", true))
            {
                sw.WriteLine("W: " + json);
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
