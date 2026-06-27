using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;

namespace CatCraft
{
    public partial class MainForm : Form
    {
        private Game _game;
        private Timer _gameTimer;
        private DateTime _lastTime;
        private float _deltaTime;
        
        // UI 控件
        private Label _statsLabel;
        private Button _sellBtn;
        private Button _sellAtSpawnBtn;
        private Button _shopToggle;
        private Panel _shopPanel;
        private Label _bagFullWarning;
        private Panel _mainMenuPanel;
        private Panel _worldManagerPanel;
        private Panel _pauseOverlayPanel;
        private PictureBox _pickaxeCursor;
        private PictureBox _gameCanvas;
        
        // 商店控件
        private Label _pickaxeName;
        private Label _pickaxeLv;
        private Label _pickaxePrice;
        private Button _buyPickaxeBtn;
        
        // 主菜单控件
        private Button _btnSingleplayer;
        private Button _btnQuit;
        private Label _versionInfo;
        private Label _copyrightInfo;
        
        // 世界管理器控件
        private FlowLayoutPanel _worldListContainer;
        private Label _noWorldsMsg;
        private Button _btnDeleteWorld;
        private Button _btnCreateWorld;
        private Button _btnReturnFromWorldMgr;
        
        // 创建世界对话框控件
        private Panel _createWorldDialog;
        private TextBox _txtWorldName;
        private Button _btnConfirmCreate;
        private Button _btnCancelCreate;
        
        // 暂停菜单控件
        private Button _btnPauseContinue;
        private Button _btnPauseSaveQuit;
        
        // 按键状态
        private Dictionary<Keys, bool> _keys;
        
        // 鼠标位置
        private Point _mousePosition;
        private bool _isMouseInCanvas;
        
        public bool IsPaused => _pauseOverlayPanel?.Visible == true;
        public bool IsMenuVisible => _mainMenuPanel?.Visible == true;
        
        public MainForm()
        {
            _keys = new Dictionary<Keys, bool>();
            InitializeComponent();
            SetupUI();
            InitializeGame();
            SetupEvents();
            
            this.Icon = LoadIcon();
            this.Text = $"小猫挖矿 - CatCraft {Config.VERSION}";
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // 控制台快捷键
            this.KeyPreview = true;
        }
        
        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.Black;
        }
        
        private Icon LoadIcon()
        {
            try
            {
                if (File.Exists("icon.ico"))
                    return new Icon("icon.ico");
                if (File.Exists("src/img/icon.ico"))
                    return new Icon("src/img/icon.ico");
            }
            catch { }
            return SystemIcons.Application;
        }
        
        private void SetupUI()
        {
            // 游戏画布
            _gameCanvas = new PictureBox
            {
                Size = new Size(800, 600),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(135, 206, 235),
                TabStop = false
            };
            _gameCanvas.Paint += GameCanvas_Paint;
            _gameCanvas.MouseMove += GameCanvas_MouseMove;
            _gameCanvas.MouseDown += GameCanvas_MouseDown;
            _gameCanvas.MouseUp += GameCanvas_MouseUp;
            _gameCanvas.MouseEnter += GameCanvas_MouseEnter;
            _gameCanvas.MouseLeave += GameCanvas_MouseLeave;
            this.Controls.Add(_gameCanvas);
            
            // 统计标签
            _statsLabel = new Label
            {
                Text = "金钱:0 | 生命:20/20",
                Location = new Point(8, 8),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(128, 0, 0, 0),
                Padding = new Padding(6, 4, 6, 4),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(_statsLabel);
            _statsLabel.BringToFront();
            
            // 出售按钮（原地出售，扣损50%）
            _sellBtn = new Button
            {
                Text = "原地出售",
                Location = new Point(800 - 100, 600 - 42),
                Size = new Size(88, 34),
                BackColor = Color.FromArgb(139, 90, 43),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _sellBtn.FlatAppearance.BorderSize = 0;
            _sellBtn.Click += (s, e) => _game?.SellBag();
            this.Controls.Add(_sellBtn);
            _sellBtn.BringToFront();
            
            // 回城出售按钮（传送到出生点，100%价值）
            _sellAtSpawnBtn = new Button
            {
                Text = "回城出售",
                Location = new Point(800 - 195, 600 - 42),
                Size = new Size(88, 34),
                BackColor = Color.FromArgb(46, 139, 87),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _sellAtSpawnBtn.FlatAppearance.BorderSize = 0;
            _sellAtSpawnBtn.Click += (s, e) => _game?.SellBagAtSpawn();
            this.Controls.Add(_sellAtSpawnBtn);
            _sellAtSpawnBtn.BringToFront();
            
            // 商店按钮
            _shopToggle = new Button
            {
                Text = "商店",
                Location = new Point(12, 600 - 42),
                Size = new Size(70, 34),
                BackColor = Color.FromArgb(139, 90, 43),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _shopToggle.FlatAppearance.BorderSize = 0;
            _shopToggle.Click += (s, e) => ToggleShop();
            this.Controls.Add(_shopToggle);
            _shopToggle.BringToFront();
            
            // 商店面板
            CreateShopPanel();
            
            // 背包满警告
            _bagFullWarning = new Label
            {
                Text = "背包已满",
                ForeColor = Color.Red,
                BackColor = Color.FromArgb(200, 0, 0, 0),
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = true,
                Visible = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(20, 10, 20, 10)
            };
            this.Controls.Add(_bagFullWarning);
            
            // 主菜单
            CreateMainMenu();
            
            // 世界管理器
            CreateWorldManager();
            
            // 暂停覆盖层
            CreatePauseOverlay();
            
            // 镐子光标
            _pickaxeCursor = new PictureBox
            {
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.Zoom,
                Visible = false,
                BackColor = Color.Transparent
            };
            this.Controls.Add(_pickaxeCursor);
            _pickaxeCursor.BringToFront();
        }
        
        private void CreateShopPanel()
        {
            _shopPanel = new Panel
            {
                Size = new Size(320, 260),
                Location = new Point(240, 170),
                BackColor = Color.FromArgb(0, 0, 0, 230),
                Visible = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var pickaxePanel = new Panel
            {
                Size = new Size(280, 100),
                Location = new Point(20, 20),
                BackColor = Color.FromArgb(60, 60, 60)
            };
            
            // 移除了预览图标，只保留文字信息
            _pickaxeName = new Label
            {
                Text = "木镐",
                Location = new Point(20, 15),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            
            _pickaxeLv = new Label
            {
                Text = "(Lv 1)",
                Location = new Point(20, 40),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            
            _pickaxePrice = new Label
            {
                Text = "价格: 500",
                Location = new Point(20, 65),
                ForeColor = Color.Gold,
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            
            _buyPickaxeBtn = new Button
            {
                Text = "购买",
                Location = new Point(200, 35),
                Size = new Size(65, 30),
                BackColor = Color.FromArgb(74, 144, 217),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _buyPickaxeBtn.Click += (s, e) => _game?.UpgradePickaxe();
            
            pickaxePanel.Controls.AddRange(new Control[] { _pickaxeName, _pickaxeLv, _pickaxePrice, _buyPickaxeBtn });
            
            _shopPanel.Controls.AddRange(new Control[] { pickaxePanel });
            this.Controls.Add(_shopPanel);
            _shopPanel.BringToFront();
        }
        
        private void CreateMainMenu()
        {
            _mainMenuPanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Black,
                Visible = true
            };
            
            _mainMenuPanel.Paint += (s, e) =>
            {
                try
                {
                    if (File.Exists("src/img/background.png"))
                    {
                        using var bg = Image.FromFile("src/img/background.png");
                        e.Graphics.DrawImage(bg, 0, 0, _mainMenuPanel.Width, _mainMenuPanel.Height);
                    }
                    else
                    {
                        e.Graphics.Clear(Color.FromArgb(26, 26, 26));
                    }
                }
                catch { }
            };
            
            // 单人游戏按钮
            _btnSingleplayer = new Button
            {
                Text = "单人游戏",
                Size = new Size(600, 80),
                Location = new Point(100, 200),
                BackColor = Color.FromArgb(60, 150, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 18, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSingleplayer.FlatAppearance.BorderSize = 0;
            _btnSingleplayer.Click += (s, e) => ShowWorldManager();
            
            // 退出游戏按钮
            _btnQuit = new Button
            {
                Text = "离开游戏",
                Size = new Size(600, 80),
                Location = new Point(100, 310),
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 18, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnQuit.FlatAppearance.BorderSize = 0;
            _btnQuit.Click += (s, e) => 
            {
                DebugConsole.Log("退出游戏按钮被点击");
                var result = MessageBox.Show("确定要退出游戏吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    DebugConsole.Log("用户确认退出");
                    _game?.SaveCurrentWorld();
                    Application.Exit();
                }
            };
            
            _versionInfo = new Label
            {
                Text = $"CatCraft/{Config.VERSION}",
                Location = new Point(15, 600 - 35),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9)
            };
            
            _copyrightInfo = new Label
            {
                Text = "Copyright catcraft.",
                Location = new Point(800 - 150, 600 - 35),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9)
            };
            
            _mainMenuPanel.Controls.AddRange(new Control[] { _btnSingleplayer, _btnQuit, _versionInfo, _copyrightInfo });
            this.Controls.Add(_mainMenuPanel);
            _mainMenuPanel.BringToFront();
        }
        
        private void CreateWorldManager()
		{
			DebugConsole.Log("创建世界管理器...");
			
			_worldManagerPanel = new Panel
			{
				Name = "WorldManagerPanel",
				Size = this.ClientSize,
				Location = new Point(0, 0),
				BackColor = Color.FromArgb(220, 0, 0, 0),  // 更深的黑色背景
				Visible = false
			};
			
			// 中央面板 - 确保可见
			var mainPanel = new Panel
			{
				Size = new Size(550, 450),
				Location = new Point((this.Width - 550) / 2, (this.Height - 450) / 2),
				BackColor = Color.FromArgb(30, 30, 50),
				BorderStyle = BorderStyle.FixedSingle,
				Anchor = AnchorStyles.None
			};
			
			// 标题
			var titleLabel = new Label
			{
				Text = "世界管理器",
				Font = new Font("微软雅黑", 18, FontStyle.Bold),
				ForeColor = Color.White,
				TextAlign = ContentAlignment.MiddleCenter,
				Dock = DockStyle.Top,
				Height = 50,
				BackColor = Color.FromArgb(50, 50, 70)
			};
			mainPanel.Controls.Add(titleLabel);
			
			// 世界列表容器
			var listBox = new Panel
			{
				Location = new Point(15, 65),
				Size = new Size(520, 250),
				BackColor = Color.FromArgb(20, 20, 40),
				BorderStyle = BorderStyle.FixedSingle
			};
			
			_worldListContainer = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = Color.Transparent,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				Padding = new Padding(5)
			};
			listBox.Controls.Add(_worldListContainer);
			mainPanel.Controls.Add(listBox);
			
			// 提示信息
			_noWorldsMsg = new Label
			{
				Text = "暂无存档\n\n点击下方按钮创建新世界",
				ForeColor = Color.Gray,
				TextAlign = ContentAlignment.MiddleCenter,
				Dock = DockStyle.Fill,
				Font = new Font("微软雅黑", 11),
				Visible = true  // 默认显示
			};
			listBox.Controls.Add(_noWorldsMsg);
			_noWorldsMsg.BringToFront();
			
			// 按钮区域
			var btnPanel = new Panel
			{
				Location = new Point(15, 325),
				Size = new Size(520, 110),
				BackColor = Color.Transparent
			};
			
			// 测试用：添加一个明显的测试标签
			var testLabel = new Label
			{
				Text = "世界管理器已加载",
				Location = new Point(15, 250),
				Size = new Size(520, 30),
				ForeColor = Color.Yellow,
				TextAlign = ContentAlignment.MiddleCenter,
				Font = new Font("微软雅黑", 12, FontStyle.Bold),
				BackColor = Color.Transparent
			};
			mainPanel.Controls.Add(testLabel);
			
			// 创建按钮
			_btnCreateWorld = new Button
			{
				Text = "创建新世界",
				Location = new Point(0, 5),
				Size = new Size(520, 32),
				BackColor = Color.FromArgb(60, 150, 80),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("微软雅黑", 11, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			_btnCreateWorld.FlatAppearance.BorderSize = 0;
			_btnCreateWorld.Click += (s, e) => 
			{
				DebugConsole.Log("创建新世界按钮被点击");
				ShowCreateWorldDialog();
			};
			
			// 删除按钮
			_btnDeleteWorld = new Button
			{
				Text = "删除世界",
				Location = new Point(0, 42),
				Size = new Size(520, 32),
				BackColor = Color.FromArgb(180, 50, 50),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("微软雅黑", 11, FontStyle.Bold),
				Enabled = false,
				Cursor = Cursors.Hand
			};
			_btnDeleteWorld.FlatAppearance.BorderSize = 0;
			_btnDeleteWorld.Click += (s, e) =>
			{
				DebugConsole.Log("删除世界按钮被点击");
				_game?.DeleteSelectedWorld();
			};
			
			// 返回按钮
			_btnReturnFromWorldMgr = new Button
			{
				Text = "← 返回主菜单",
				Location = new Point(0, 79),
				Size = new Size(520, 32),
				BackColor = Color.FromArgb(80, 80, 100),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("微软雅黑", 11, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			_btnReturnFromWorldMgr.FlatAppearance.BorderSize = 0;
			_btnReturnFromWorldMgr.Click += (s, e) =>
			{
				DebugConsole.Log("返回按钮被点击");
				HideWorldManager();
			};
			
			btnPanel.Controls.AddRange(new Control[] { _btnCreateWorld, _btnDeleteWorld, _btnReturnFromWorldMgr });
			mainPanel.Controls.Add(btnPanel);
			
			_worldManagerPanel.Controls.Add(mainPanel);
			this.Controls.Add(_worldManagerPanel);
			_worldManagerPanel.BringToFront();
			
			DebugConsole.Log("世界管理器创建完成");
		}
        
        private void CreatePauseOverlay()
        {
            _pauseOverlayPanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(180, 0, 0, 0),
                Visible = false
            };
            
            var pauseContent = new Panel
            {
                Size = new Size(400, 200),
                Location = new Point(200, 200),
                BackColor = Color.FromArgb(0, 0, 0, 220),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var titleLabel = new Label
            {
                Text = "游戏暂停",
                Location = new Point(150, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = true
            };
            pauseContent.Controls.Add(titleLabel);
            
            _btnPauseContinue = new Button
            {
                Text = "继续游戏",
                Location = new Point(50, 80),
                Size = new Size(130, 40),
                BackColor = Color.FromArgb(74, 144, 217),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPauseContinue.Click += (s, e) => HidePauseOverlay();
            pauseContent.Controls.Add(_btnPauseContinue);
            
            _btnPauseSaveQuit = new Button
            {
                Text = "保存并退出",
                Location = new Point(220, 80),
                Size = new Size(130, 40),
                BackColor = Color.FromArgb(196, 48, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnPauseSaveQuit.Click += (s, e) => _game?.SaveAndQuitToTitle();
            pauseContent.Controls.Add(_btnPauseSaveQuit);
            
            var btnPauseAdvancements = new Button
            {
                Text = "成就",
                Location = new Point(150, 130),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(180, 150, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPauseAdvancements.Click += (s, e) => ShowAdvancements();
            pauseContent.Controls.Add(btnPauseAdvancements);
            
            _pauseOverlayPanel.Controls.Add(pauseContent);
            this.Controls.Add(_pauseOverlayPanel);
            _pauseOverlayPanel.BringToFront();
        }
        
        private void InitializeGame()
        {
            _game = new Game(this);
            _gameTimer = new Timer { Interval = 16 };
            _gameTimer.Tick += GameTimer_Tick;
            _gameTimer.Start();
            _lastTime = DateTime.Now;
        }
        
        private void SetupEvents()
        {
            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;
        }
        
        private void GameTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            _deltaTime = (float)(now - _lastTime).TotalSeconds;
            if (_deltaTime > 0.1f) _deltaTime = 0.1f;
            _lastTime = now;
            
            if (!_mainMenuPanel.Visible && !_worldManagerPanel.Visible && !_pauseOverlayPanel.Visible)
            {
                _game?.Update(_deltaTime);
                UpdateStats();
                _gameCanvas.Invalidate();
            }
        }
        
        private void GameCanvas_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            _game?.Draw(e.Graphics);
        }
        
        private void GameCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            _mousePosition = e.Location;
            if (_game != null && !IsMenuVisible)
            {
                _game.UpdateMousePosition(e.X + (int)_game.CamX, e.Y + (int)_game.CamY, e.X, e.Y);
            }
            
            // 隐藏 PictureBox 镐子光标，改用游戏内绘制
            _pickaxeCursor.Visible = false;
        }
        
        private void GameCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (IsMenuVisible || IsPaused) return;
            
            if (_advancementsOpen)
            {
                _advancementsOpen = false;
                return;
            }
            
            if (e.Button == MouseButtons.Left)
            {
                // 如果背包打开，处理背包点击
                if (_game != null && _game.IsBagOpen)
                {
                    _game.HandleBagClick(e.X, e.Y, false);
                }
                else
                {
                    _game?.StartDigging();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 如果背包打开，处理右键出售
                if (_game != null && _game.IsBagOpen)
                {
                    _game.HandleBagClick(e.X, e.Y, true);
                }
                else
                {
                    _game?.PlaceBlock();
                }
            }
        }
        
        private void GameCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            _game?.StopDigging();
        }
        
        private void GameCanvas_MouseEnter(object sender, EventArgs e)
        {
            _isMouseInCanvas = true;
            if (!IsMenuVisible && !IsPaused && _game?.IsGameActive == true)
            {
                _pickaxeCursor.Visible = true;
                _pickaxeCursor.BringToFront();
                UpdatePickaxeCursor();
            }
        }
        
        private void GameCanvas_MouseLeave(object sender, EventArgs e)
        {
            _isMouseInCanvas = false;
            _pickaxeCursor.Visible = false;
        }
        
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			// 控制台快捷键 Ctrl+Shift+D
			if (e.Control && e.Shift && e.KeyCode == Keys.D)
			{
				DebugConsole.ToggleConsole();
				DebugConsole.Log("调试控制台已打开/关闭");
				
				// 如果游戏已经存在，初始化图片
				if (_game != null)
				{
					_game.InitImages();
				}
				return;
			}
			
			_keys[e.KeyCode] = true;
			
			if (!IsMenuVisible && !IsPaused)
			{
				_game?.KeyDown(e.KeyCode);
			}
			
			if (e.KeyCode == Keys.Escape)
			{
				if (IsMenuVisible) return;
				if (_shopPanel.Visible)
				{
					_shopPanel.Visible = false;
					return;
				}
				if (_worldManagerPanel.Visible)
				{
					HideWorldManager();
					return;
				}
				if (IsPaused)
				{
					HidePauseOverlay();
				}
				else if (!IsMenuVisible && _game?.IsGameActive == true)
				{
					ShowPauseOverlay();
				}
			}
		}
        
        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            _keys[e.KeyCode] = false;
            _game?.KeyUp(e.KeyCode);
        }
        
        private void UpdateStats()
        {
            if (_game == null) return;
            _statsLabel.Text = $"金钱:{_game.PlayerMoney} | 生命:{_game.PlayerHealth}/{_game.PlayerMaxHealth} | {_game.GetTimeDisplay()}";
        }
        
        private void ToggleShop()
        {
            if (!IsMenuVisible && !IsPaused && _game?.IsGameActive == true)
            {
                _shopPanel.Visible = !_shopPanel.Visible;
                _game?.UpdateShopDisplay();
                UpdateShopUI();
            }
        }
        
        public void UpdateShopUI()
        {
            if (_game == null) return;
            
            var pickaxeInfo = _game.GetPickaxeInfo();
            _pickaxeName.Text = pickaxeInfo.Name;
            _pickaxeLv.Text = pickaxeInfo.IsMaxed ? "(已满级)" : $"(Lv {pickaxeInfo.NextLevel})";
            _pickaxePrice.Text = pickaxeInfo.IsMaxed ? "" : $"价格: {pickaxeInfo.Price}";
            _buyPickaxeBtn.Visible = !pickaxeInfo.IsMaxed;
        }
        
        public void UpdatePickaxeCursor()
        {
            if (_game == null || !_game.IsGameActive) return;
            
            try
            {
                var img = _game.GetPickaxeCursorImage();
                if (img != null)
                {
                    _pickaxeCursor.Image = img;
                    _pickaxeCursor.Location = new Point(_mousePosition.X - 16, _mousePosition.Y - 16);
                    _pickaxeCursor.Visible = _isMouseInCanvas;
                    _pickaxeCursor.BringToFront();
                }
                else
                {
                    _pickaxeCursor.Visible = false;
                }
            }
            catch { }
        }
        
        public void ShowBagWarning()
        {
            _bagFullWarning.Visible = true;
            _bagFullWarning.BringToFront();
            var timer = new Timer { Interval = 2000 };
            timer.Tick += (s, e) => { _bagFullWarning.Visible = false; timer.Stop(); };
            timer.Start();
        }
        
        public void ShowWorldManager()
		{
			DebugConsole.Log("=== 显示世界管理器 ===");
			DebugConsole.Log($"Game对象存在: {_game != null}");
			
			if (_game == null)
			{
				DebugConsole.Log("错误: Game对象为空");
				return;
			}
			
			_mainMenuPanel.Visible = false;
			_worldManagerPanel.Visible = true;
			_worldManagerPanel.BringToFront();
			_worldManagerPanel.Refresh();
			
			// 刷新世界列表
			_game.RefreshWorldList();
			
			DebugConsole.Log($"世界列表容器控件数: {_worldListContainer.Controls.Count}");
		}
		
		public void StartGame()
		{
			DebugConsole.Log("StartGame 被调用 - 开始游戏");
			
			// 确保所有菜单都隐藏
			_mainMenuPanel.Visible = false;
			_worldManagerPanel.Visible = false;
			_pauseOverlayPanel.Visible = false;
			_shopPanel.Visible = false;
			
			// 确保游戏画布可见
			_gameCanvas.Visible = true;
			_gameCanvas.Focus();
			
			// 显示游戏UI元素并确保在最前面
			_statsLabel.Visible = true;
			_statsLabel.BringToFront();
			_sellBtn.Visible = true;
			_sellBtn.BringToFront();
			_sellAtSpawnBtn.Visible = true;
			_sellAtSpawnBtn.BringToFront();
			_shopToggle.Visible = true;
			_shopToggle.BringToFront();
			
			// 显示镐子光标
			if (_isMouseInCanvas)
			{
				_pickaxeCursor.Visible = true;
				UpdatePickaxeCursor();
			}
			
			// 刷新统计显示
			UpdateStats();
			
			DebugConsole.Log("游戏界面已准备就绪");
		}
        
        public void HideWorldManager()
        {
            DebugConsole.Log("隐藏世界管理器");
            _worldManagerPanel.Visible = false;
            _mainMenuPanel.Visible = true;
        }
        
        public void ShowPauseOverlay()
        {
            if (IsMenuVisible || _game?.IsGameActive == false) return;
            _pauseOverlayPanel.Visible = true;
            _game?.SetPaused(true);
            _pickaxeCursor.Visible = false;
        }
        
        public void HidePauseOverlay()
        {
            _pauseOverlayPanel.Visible = false;
            _game?.SetPaused(false);
            if (_isMouseInCanvas && _game?.IsGameActive == true)
                _pickaxeCursor.Visible = true;
        }
        
        public void HideMainMenu()
        {
            _mainMenuPanel.Visible = false;
        }
        
        public void ShowMainMenu()
        {
            _mainMenuPanel.Visible = true;
            _worldManagerPanel.Visible = false;
            _pauseOverlayPanel.Visible = false;
            _shopPanel.Visible = false;
            _pickaxeCursor.Visible = false;
            
            // 隐藏游戏UI元素
            _statsLabel.Visible = false;
            _sellBtn.Visible = false;
            _sellAtSpawnBtn.Visible = false;
            _shopToggle.Visible = false;
        }
        
        public void AddWorldToList(string id, string name, string lastPlayed, bool isSelected)
        {
            DebugConsole.Log($"添加世界: {name}");
            
            _noWorldsMsg.Visible = false;
            
            var worldItem = new Panel
            {
                Width = 500,
                Height = 50,
                Margin = new Padding(0, 0, 0, 5),
                BackColor = isSelected ? Color.FromArgb(74, 144, 217, 100) : Color.FromArgb(255, 255, 255, 20),
                Cursor = Cursors.Hand,
                Tag = id
            };
            
            var nameLabel = new Label
            {
                Text = name,
                Location = new Point(10, 8),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 11, FontStyle.Bold),
                AutoSize = true
            };
            
            var dateLabel = new Label
            {
                Text = lastPlayed,
                Location = new Point(10, 28),
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("微软雅黑", 8),
                AutoSize = true
            };
            
            worldItem.Controls.AddRange(new Control[] { nameLabel, dateLabel });
            worldItem.Click += (s, e) =>
			{
				DebugConsole.Log($"点击世界: {name}, ID: {id}");
				
				// 清除其他项高亮
				foreach (Control control in _worldListContainer.Controls)
				{
					if (control is Panel p)
						p.BackColor = Color.FromArgb(255, 255, 255, 20);
				}
				worldItem.BackColor = Color.FromArgb(74, 144, 217, 100);
				
				// 调用 SelectWorld
				_game?.SelectWorld(id);
				
				// 启用删除按钮
				_btnDeleteWorld.Enabled = true;
				_btnDeleteWorld.Text = $"🗑️ 删除 {name}";
				_btnDeleteWorld.BackColor = Color.FromArgb(200, 60, 60);
			};
            
            worldItem.DoubleClick += (s, e) =>
			{
				DebugConsole.Log($"双击开始游戏: {name}, ID: {id}");
				_game?.StartGameWithWorld(id);
			};
            
            _worldListContainer.Controls.Add(worldItem);
            DebugConsole.Log($"世界列表现有 {_worldListContainer.Controls.Count} 个世界");
        }
        
        public void ClearWorldList()
        {
            _worldListContainer.Controls.Clear();
            _noWorldsMsg.Visible = true;
            _btnDeleteWorld.Enabled = false;
            _btnDeleteWorld.Text = "🗑️ 删除世界";
        }
        
        public void SetNoWorldsMessageVisible(bool visible)
        {
            _noWorldsMsg.Visible = visible;
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
		{
			DebugConsole.Log("游戏正在关闭，保存世界...");
			_game?.SaveCurrentWorld();
			_gameTimer?.Stop();
			base.OnFormClosing(e);
		}
		
		private void ShowCreateWorldDialog()
		{
			if (_createWorldDialog == null)
			{
				// 创建对话框面板
				_createWorldDialog = new Panel
				{
					Name = "CreateWorldDialog",
					Size = this.ClientSize,
					Location = new Point(0, 0),
					BackColor = Color.FromArgb(180, 0, 0, 0),
					Visible = false
				};
				
				// 对话框内容面板
				var dialogPanel = new Panel
				{
					Size = new Size(400, 220),
					Location = new Point((this.Width - 400) / 2, (this.Height - 220) / 2),
					BackColor = Color.FromArgb(30, 30, 50),
					BorderStyle = BorderStyle.FixedSingle
				};
				
				// 标题
				var titleLabel = new Label
				{
					Text = "✨ 创建新世界",
					Font = new Font("微软雅黑", 16, FontStyle.Bold),
					ForeColor = Color.White,
					TextAlign = ContentAlignment.MiddleCenter,
					Dock = DockStyle.Top,
					Height = 45,
					BackColor = Color.FromArgb(50, 50, 70)
				};
				dialogPanel.Controls.Add(titleLabel);
				
				// 标签
				var nameLabel = new Label
				{
					Text = "世界名称:",
					Font = new Font("微软雅黑", 12),
					ForeColor = Color.White,
					Location = new Point(30, 65),
					Size = new Size(100, 25)
				};
				dialogPanel.Controls.Add(nameLabel);
				
				// 输入框
				_txtWorldName = new TextBox
				{
					Location = new Point(130, 62),
					Size = new Size(240, 28),
					Font = new Font("微软雅黑", 12),
					BackColor = Color.FromArgb(20, 20, 40),
					ForeColor = Color.White,
					BorderStyle = BorderStyle.FixedSingle,
					PlaceholderText = "请输入世界名称"
				};
				dialogPanel.Controls.Add(_txtWorldName);
				
				// 确认按钮
				_btnConfirmCreate = new Button
				{
					Text = "确认创建",
					Location = new Point(60, 140),
					Size = new Size(140, 35),
					BackColor = Color.FromArgb(60, 150, 80),
					ForeColor = Color.White,
					FlatStyle = FlatStyle.Flat,
					Font = new Font("微软雅黑", 11, FontStyle.Bold),
					Cursor = Cursors.Hand
				};
				_btnConfirmCreate.FlatAppearance.BorderSize = 0;
				_btnConfirmCreate.Click += (s, e) =>
				{
					string worldName = _txtWorldName.Text.Trim();
					if (!string.IsNullOrEmpty(worldName))
					{
						_game?.CreateNewWorld(worldName);
						HideCreateWorldDialog();
					}
				};
				dialogPanel.Controls.Add(_btnConfirmCreate);
				
				// 取消按钮
				_btnCancelCreate = new Button
				{
					Text = "取消",
					Location = new Point(200, 140),
					Size = new Size(140, 35),
					BackColor = Color.FromArgb(100, 100, 120),
					ForeColor = Color.White,
					FlatStyle = FlatStyle.Flat,
					Font = new Font("微软雅黑", 11, FontStyle.Bold),
					Cursor = Cursors.Hand
				};
				_btnCancelCreate.FlatAppearance.BorderSize = 0;
				_btnCancelCreate.Click += (s, e) =>
				{
					HideCreateWorldDialog();
				};
				dialogPanel.Controls.Add(_btnCancelCreate);
				
				_createWorldDialog.Controls.Add(dialogPanel);
				this.Controls.Add(_createWorldDialog);
			}
			
			// 重置输入框
			_txtWorldName.Text = "";
			
			// 显示对话框
			_createWorldDialog.Visible = true;
			_createWorldDialog.BringToFront();
			_txtWorldName.Focus();
			
			DebugConsole.Log("显示创建世界对话框");
		}
		
		private void HideCreateWorldDialog()
		{
			if (_createWorldDialog != null)
			{
				_createWorldDialog.Visible = false;
				DebugConsole.Log("隐藏创建世界对话框");
			}
		}
		
		private bool _advancementsOpen = false;
		
		public void ShowAdvancements()
		{
			_advancementsOpen = true;
			_pauseOverlayPanel.Visible = false;
			_gameCanvas.Focus();
		}
		
		public void HideAdvancements()
		{
			_advancementsOpen = false;
		}
		
		public bool IsAdvancementsOpen => _advancementsOpen;
	}
}