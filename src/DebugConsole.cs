using System;
using System.Windows.Forms;
using System.Drawing;

namespace CatCraft
{
    public class DebugConsole : Form
    {
        private TextBox _textBox;
        private static DebugConsole _instance;
        private static bool _isVisible = false;

        private DebugConsole()
        {
            this.Text = "CatCraft 调试控制台";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            
            _textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9),
                WordWrap = false
            };
            
            this.Controls.Add(_textBox);
            this.FormClosing += (s, e) => { this.Hide(); e.Cancel = true; _isVisible = false; };
        }

        public static void ShowConsole()
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new DebugConsole();
            }
            _instance.Show();
            _instance.BringToFront();
            _isVisible = true;
        }

        public static void HideConsole()
        {
            if (_instance != null && !_instance.IsDisposed)
            {
                _instance.Hide();
                _isVisible = false;
            }
        }

        public static void ToggleConsole()
        {
            if (_isVisible)
                HideConsole();
            else
                ShowConsole();
        }

        public static void Log(string message)
        {
            if (_instance != null && !_instance.IsDisposed && _isVisible)
            {
                if (_instance._textBox.InvokeRequired)
                {
                    _instance._textBox.Invoke(new Action(() => Log(message)));
                    return;
                }
                _instance._textBox.AppendText($"[{DateTime.Now.ToString("HH:mm:ss")}] {message}\r\n");
                _instance._textBox.ScrollToCaret();
            }
        }

        public static void Clear()
        {
            if (_instance != null && !_instance.IsDisposed && _isVisible)
            {
                if (_instance._textBox.InvokeRequired)
                {
                    _instance._textBox.Invoke(new Action(() => Clear()));
                    return;
                }
                _instance._textBox.Clear();
            }
        }
    }
}