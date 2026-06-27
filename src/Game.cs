using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Windows.Forms;

namespace CatCraft
{
    public partial class Game
    {
        private const int TILE_SIZE = 40;
        private const int WORLD_H = 120;
        
        private World _world;
        private Player _player;
        private MainForm _form;
        private LightEngine _lightEngine;
        
        private float _camX, _camY;
        private float _mouseWorldX, _mouseWorldY;
        private int _mouseScreenX, _mouseScreenY; // 新增：屏幕坐标
        private float _dayNightCycle;
        private bool _paused;
        private bool _gameActive;
        private string _currentWorldId;
        private string _selectedWorldId;
        
        // 调试模式
        private bool _debugMode;
        private int _frameCount;
        private float _fpsTimer;
        private float _currentFps;
        
        // 游戏刚激活的标志
        private bool _justActivated;
        
        private Dictionary<Keys, bool> _keys;
        private List<WorldSaveData> _worlds;
        
        // 背包系统
        private bool _bagOpen;
        private int _selectedBagSlot = -1;
        private ItemStack _mouseItem = null; // 鼠标持有的物品
        
        private readonly string SAVE_DIR = "saves";
        private readonly string WORLDS_INDEX_FILE = "worlds_index.json";
        
        // 成就系统
        private List<Advancement> _advancements = new List<Advancement>();
        private HashSet<string> _completedAdvancements = new HashSet<string>();
        private string _pendingNotification = null;
        private float _notificationTimer = 0;
        private string _hoveredAdvancementId = null; // 当前悬浮的成就ID
        private readonly string ADVANCEMENTS_DIR = "src/game/advancements";
        
        // 图片资源
        private Dictionary<TileType, Image> _tileImages;
        private Image _catImage;
        private Dictionary<int, Image> _heartImages;
        private Dictionary<int, Image> _digImages;
        
        // 镐子数据
        private static readonly int[] PICKAXE_SPEEDS = { 25, 75, 150, 300, 450, 625 };
        private static readonly int[] PICKAXE_COSTS = { 500, 2000, 5000, 10000, 25000 };
        private static readonly string[] PICKAXE_NAMES = { "木镐", "石镐", "铁镐", "金镐", "钻石镐", "下界合金镐" };
        private static readonly string[] PICKAXE_IMAGES = 
        {
            "src/img/wooden_pickaxe.png", "src/img/stone_pickaxe.png",
            "src/img/iron_pickaxe.png", "src/img/golden_pickaxe.png",
            "src/img/diamond_pickaxe.png", "src/img/nether_pickaxe.png"
        };
        
        private static readonly int[] BAG_COSTS = { 500, 1500, 2500, 5000 };
        private static readonly int[] BAG_CAPACITIES = { 10, 20, 30, 40, 50 };
        
        public int PlayerMoney => _player?.Money ?? 0;
        public int PlayerProgress => Math.Min(100, (_player?.Money ?? 0) / 150);
        public int PlayerHealth => _player?.Health ?? 0;
        public int PlayerMaxHealth => _player?.MaxHealth ?? 20;
        public int BagCount => _player?.Bag?.Count ?? 0;
        public int BagMax => Player.BAG_ROWS * Player.BAG_COLS;
        public float CamX => _camX;
        public float CamY => _camY;
        public bool IsGameActive => _gameActive;
        public bool IsBagOpen => _bagOpen;
        
        public Game(MainForm form)
		{
			_form = form;
			_world = new World();
			_player = new Player();
			_lightEngine = new LightEngine();
			_keys = new Dictionary<Keys, bool>();
			
			// 初始化图片字典
			_tileImages = new Dictionary<TileType, Image>();
			_heartImages = new Dictionary<int, Image>();
			_digImages = new Dictionary<int, Image>();
			
			// 直接加载所有图片，不等待调试控制台
			LoadAllImages();
			
			Directory.CreateDirectory(SAVE_DIR);
			LoadWorldsIndex();
			LoadAdvancements();
			
			_gameActive = false;
		}
		
		private void LoadAllImages()
		{
			try
			{
				string currentDir = AppDomain.CurrentDomain.BaseDirectory;
				
				// 加载方块图片
				var imagePaths = new Dictionary<TileType, string>
				{
					{ TileType.Grass, "grass.png" },
					{ TileType.Dirt, "dirt.png" },
					{ TileType.Stone, "stone.png" },
					{ TileType.Coal, "coal_ore.png" },
					{ TileType.Iron, "iron_ore.png" },
					{ TileType.Gold, "gold_ore.png" },
					{ TileType.Emerald, "emerald_ore.png" },
					{ TileType.Redstone, "redstone_ore.png" },
					{ TileType.Diamond, "diamond_ore.png" },
					{ TileType.Lava, "lava.png" },
					{ TileType.Bedrock, "bedrock.png" },
					{ TileType.TNT, "tnt.png" },
					{ TileType.Sand, "sand.png" },
					{ TileType.Cactus, "cactas.png" },
					{ TileType.Wood, "wood.png" },
					{ TileType.Leaves, "leaves.png" },
					{ TileType.GrassDecor, "grasses.png" }
				};
				
				foreach (var kv in imagePaths)
				{
					string fullPath = Path.Combine(currentDir, "src/img", kv.Value);
					if (File.Exists(fullPath))
					{
						try
						{
							_tileImages[kv.Key] = Image.FromFile(fullPath);
						}
						catch { }
					}
				}
				
				// 加载心脏图片
				for (int i = 0; i <= 20; i++)
				{
					string path = Path.Combine(currentDir, "src/img", $"heart_{i}.png");
					if (File.Exists(path))
					{
						try
						{
							_heartImages[i] = Image.FromFile(path);
						}
						catch { }
					}
				}
				
				// 加载挖掘动画
				for (int i = 0; i <= 10; i++)
				{
					string path = Path.Combine(currentDir, "src/img", $"dig_{i}.png");
					if (File.Exists(path))
					{
						try
						{
							_digImages[i] = Image.FromFile(path);
						}
						catch { }
					}
				}
				
				// 加载猫咪图片
				string[] catPaths = {
					Path.Combine(currentDir, "src/img/cat.png"),
					Path.Combine(currentDir, "src/img/cat_a.png"),
					Path.Combine(currentDir, "src/img/cat_b.png")
				};
				foreach (var path in catPaths)
				{
					if (File.Exists(path))
					{
						try
						{
							_catImage = Image.FromFile(path);
							break;
						}
						catch { }
					}
				}
			}
			catch
			{
				// 忽略错误，使用颜色块替代
			}
		}
		
		// 添加一个初始化图片的方法，在控制台打开后调用
		public void InitImages()
		{
			DebugConsole.Log("开始加载图片资源...");
			LoadTileImages();
			LoadHeartImages();
			LoadDigImages();
			LoadCatImage();
			DebugConsole.Log("图片资源加载完成");
		}
        
        private void LoadTileImages()
        {
            _tileImages = new Dictionary<TileType, Image>();
            
            var imagePaths = new Dictionary<TileType, string>
            {
                { TileType.Grass, "src/img/grass.png" },
                { TileType.Dirt, "src/img/dirt.png" },
                { TileType.Stone, "src/img/stone.png" },
                { TileType.Coal, "src/img/coal_ore.png" },
                { TileType.Iron, "src/img/iron_ore.png" },
                { TileType.Gold, "src/img/gold_ore.png" },
                { TileType.Emerald, "src/img/emerald_ore.png" },
                { TileType.Redstone, "src/img/redstone_ore.png" },
                { TileType.Diamond, "src/img/diamond_ore.png" },
                { TileType.Lava, "src/img/lava.png" },
                { TileType.Bedrock, "src/img/bedrock.png" },
                { TileType.TNT, "src/img/tnt.png" },
                { TileType.Sand, "src/img/sand.png" },
                { TileType.Cactus, "src/img/cactas.png" },
                { TileType.Wood, "src/img/wood.png" },
                { TileType.Leaves, "src/img/leaves.png" },
                { TileType.GrassDecor, "src/img/grasses.png" }
            };
            
            foreach (var kv in imagePaths)
            {
                try
                {
                    if (File.Exists(kv.Value))
                    {
                        _tileImages[kv.Key] = Image.FromFile(kv.Value);
                    }
                }
                catch { }
            }
        }
        
        private void LoadHeartImages()
        {
            _heartImages = new Dictionary<int, Image>();
            for (int i = 0; i <= 20; i++)
            {
                try
                {
                    var path = $"src/img/heart_{i}.png";
                    if (File.Exists(path))
                        _heartImages[i] = Image.FromFile(path);
                }
                catch { }
            }
        }
        
        private void LoadDigImages()
        {
            _digImages = new Dictionary<int, Image>();
            for (int i = 0; i <= 10; i++)
            {
                try
                {
                    var path = $"src/img/dig_{i}.png";
                    if (File.Exists(path))
                        _digImages[i] = Image.FromFile(path);
                }
                catch { }
            }
        }
        
        private void LoadCatImage()
		{
			try
			{
				string currentDir = AppDomain.CurrentDomain.BaseDirectory;
				DebugConsole.Log($"当前目录: {currentDir}");
				
				// 优先使用 PNG 格式，SVG 在 .NET 中支持不好
				string[] possiblePaths = {
					Path.Combine(currentDir, "src/img/cat.png"),
					Path.Combine(currentDir, "src/img/cat_a.png"),
					Path.Combine(currentDir, "src/img/cat_b.png"),
					Path.Combine(currentDir, "src/img/svg/cat_b.png"),
					"src/img/cat.png",
					"src/img/cat_a.png",
					"src/img/cat_b.png",
					// 最后尝试 SVG（可能失败）
					Path.Combine(currentDir, "src/img/svg/cat_b.svg"),
					"src/img/svg/cat_b.svg"
				};
				
				foreach (var path in possiblePaths)
				{
					DebugConsole.Log($"尝试加载: {path}");
					if (File.Exists(path))
					{
						try
						{
							_catImage = Image.FromFile(path);
							DebugConsole.Log($"✅ 猫咪图片加载成功: {path} (大小: {_catImage.Width}x{_catImage.Height})");
							return;
						}
						catch (Exception ex)
						{
							DebugConsole.Log($"加载失败: {ex.Message}");
						}
					}
				}
				
				DebugConsole.Log("❌ 未找到可用的猫咪图片，使用默认绘制");
			}
			catch (Exception ex)
			{
				DebugConsole.Log($"加载猫咪图片失败: {ex.Message}");
			}
		}
        
        public void Update(float dt)
        {
            if (!_gameActive || _paused) return;
            
            // 游戏刚激活时，重置速度防止飞出去
            if (_justActivated)
            {
                DebugConsole.Log($"[Game.Update] 重置速度前 - Vx={_player.Vx:F2}, Vy={_player.Vy:F2}");
                _player.Vx = 0;
                _player.Vy = 0;
                _justActivated = false;
                DebugConsole.Log($"[Game.Update] 重置速度后 - Vx={_player.Vx:F2}, Vy={_player.Vy:F2}");
                DebugConsole.Log($"[Game.Update] 玩家位置 - X={_player.X:F2}, Y={_player.Y:F2}");
            }
            
            // 计算 FPS
            _frameCount++;
            _fpsTimer += dt;
            if (_fpsTimer >= 1.0f)
            {
                _currentFps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0;
            }
            
            _dayNightCycle = (_dayNightCycle + dt / 120f) % 1f;
            
            _player.Update(dt, _world, _keys);
            _world.Update(dt, _player);
            UpdateDigging(dt);
            UpdateCamera();
            _lightEngine.Update(_world, _player, _dayNightCycle);
            CheckWorldBounds();
        }
        
        private void UpdateDigging(float dt)
        {
            if (!_player.IsDigging) return;
            
            var tile = _world.GetTile(_player.DigX, _player.DigY);
            if (tile == TileType.Air || tile == TileType.Bedrock)
            {
                _player.StopDigging();
                return;
            }
            
            int health = _world.GetTileHealth(_player.DigX, _player.DigY);
            _player.DigProgress += PICKAXE_SPEEDS[_player.PickaxeLevel - 1] * dt;
            
            if (_player.DigProgress >= health)
            {
                if (tile == TileType.TNT)
                {
                    Explode(_player.DigX, _player.DigY);
                }
                else
                {
                    if (tile != TileType.Lava)
                    {
                        bool added = _player.AddToBag((byte)tile);
                        if (!added)
                        {
                            _form.ShowBagWarning();
                        }
                    }
                    TriggerMine(tile);
                    _world.SetTile(_player.DigX, _player.DigY, TileType.Air);
                }
                _player.StopDigging();
            }
        }
        
        private void Explode(int centerX, int centerY)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    var tile = _world.GetTile(x, y);
                    if (tile != TileType.Air && tile != TileType.Bedrock)
                    {
                        _world.SetTile(x, y, TileType.Air);
                    }
                }
            }
            
            float distance = MathF.Sqrt(
                MathF.Pow(_player.X - (centerX * TILE_SIZE + TILE_SIZE / 2), 2) +
                MathF.Pow(_player.Y - (centerY * TILE_SIZE + TILE_SIZE / 2), 2));
            
            if (distance < 3 * TILE_SIZE)
            {
                _player.Damage(8);
            }
        }
        
        public void PlaceBlock()
        {
            if (!_gameActive || _paused || _bagOpen) return;
            
            int tileX = (int)(_mouseWorldX / TILE_SIZE);
            int tileY = (int)(_mouseWorldY / TILE_SIZE);
            
            _player.PlaceBlock(_world, tileX, tileY);
        }
        
        private void UpdateCamera()
        {
            float targetCamX = _player.X - 400;
            float targetCamY = _player.Y - 300;
            
            targetCamX = Math.Max(0, Math.Min(targetCamX, _world.WorldWidth * TILE_SIZE - 800));
            targetCamY = Math.Max(-200, Math.Min(targetCamY, WORLD_H * TILE_SIZE - 600));
            
            _camX = targetCamX;
            _camY = targetCamY;
            
            if (_camX < 0) _camX = 0;
            if (_camY < 0) _camY = 0;
        }
        
        private void CheckWorldBounds()
        {
            if (_player.X > _world.WorldWidth * TILE_SIZE - 400)
            {
                _world.ExpandRight();
            }
            if (_player.X < 400)
            {
                float oldOffset = _world.WorldOffset;
                _world.ExpandLeft();
                // 世界向左扩展后，玩家位置需要向右移动以保持在世界中的相对位置
                _player.X += (_world.WorldOffset - oldOffset) * TILE_SIZE;
            }
        }
        
        public void Draw(Graphics g)
        {
            // 绘制天空
            g.Clear(GetSkyColor());
            
            // 计算可见区域
            int startX = Math.Max(0, (int)(_camX / TILE_SIZE));
            int endX = Math.Min(_world.WorldWidth, (int)((_camX + 800) / TILE_SIZE) + 2);
            int startY = Math.Max(0, (int)(_camY / TILE_SIZE));
            int endY = Math.Min(WORLD_H, (int)((_camY + 600) / TILE_SIZE) + 2);
            
            // 绘制地图
            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    var tile = _world.GetTile(x, y);
                    if (tile == TileType.Air) continue;
                    
                    int sx = x * TILE_SIZE - (int)_camX;
                    int sy = y * TILE_SIZE - (int)_camY;
                    
                    DrawTile(g, tile, sx, sy);
                }
            }
            
            // 绘制下落中的沙子
            _world.DrawFallingSand(g, _camX, _camY, TILE_SIZE);
            
            // 绘制挖掘效果
            DrawDiggingEffect(g);
            
            // 绘制玩家
            DrawPlayer(g);
            
            // 绘制血量
            DrawHealth(g);
            
            // 绘制光照效果
            DrawLighting(g, startX, endX, startY, endY);
            
            // 绘制调试信息
            DrawDebugInfo(g);
            
            // 绘制背包界面
            DrawBag(g);
            
            // 绘制成就通知
            DrawAdvancementNotification(g);
            
            // 绘制成就面板
            if (_form.IsAdvancementsOpen)
            {
                DrawAdvancementsPanel(g, 800, 600);
            }
            
            // 绘制快捷栏（始终显示在右边）
            DrawHotbar(g, 800, 600);
            
            // 绘制镐子光标（直接在游戏画面上绘制，保留透明通道）
            DrawPickaxeCursor(g);
        }
        
        private void DrawPickaxeCursor(Graphics g)
        {
            if (!_gameActive || _bagOpen) return;
            
            try
            {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(currentDir, PICKAXE_IMAGES[_player.PickaxeLevel - 1]);
                if (File.Exists(path))
                {
                    using var img = new Bitmap(path);
                    // 直接绘制 PNG，保留透明通道
                    g.DrawImage(img, _mouseScreenX - 16, _mouseScreenY - 16, 32, 32);
                }
            }
            catch { }
        }
        
        private void DrawDebugInfo(Graphics g)
        {
            if (!_debugMode || !_gameActive) return;
            
            using var font = new Font("Consolas", 10);
            using var brush = new SolidBrush(Color.White);
            using var bgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
            
            // 调整起始位置，避免和统计标签叠加
            int y = 35;
            int padding = 5;
            
            // FPS
            string fpsText = $"FPS: {_currentFps:F1}";
            SizeF fpsSize = g.MeasureString(fpsText, font);
            g.FillRectangle(bgBrush, 10, y, fpsSize.Width + padding * 2, fpsSize.Height + padding * 2);
            g.DrawString(fpsText, font, brush, 10 + padding, y + padding);
            y += (int)fpsSize.Height + padding * 2 + 5;
            
            // 玩家坐标
            string posText = $"X: {_player.X:F2}, Y: {_player.Y:F2}";
            SizeF posSize = g.MeasureString(posText, font);
            g.FillRectangle(bgBrush, 10, y, posSize.Width + padding * 2, posSize.Height + padding * 2);
            g.DrawString(posText, font, brush, 10 + padding, y + padding);
            y += (int)posSize.Height + padding * 2 + 5;
            
            // 时间
            string timeText = $"Time: {_dayNightCycle:F2}";
            SizeF timeSize = g.MeasureString(timeText, font);
            g.FillRectangle(bgBrush, 10, y, timeSize.Width + padding * 2, timeSize.Height + padding * 2);
            g.DrawString(timeText, font, brush, 10 + padding, y + padding);
            y += (int)timeSize.Height + padding * 2 + 5;
            
            // 重生点
            string spawnText = $"Spawn: ({_world.SpawnX}, {_world.SpawnY})";
            SizeF spawnSize = g.MeasureString(spawnText, font);
            g.FillRectangle(bgBrush, 10, y, spawnSize.Width + padding * 2, spawnSize.Height + padding * 2);
            g.DrawString(spawnText, font, brush, 10 + padding, y + padding);
        }
        
        private void DrawTile(Graphics g, TileType tile, int x, int y)
        {
            if (_tileImages != null && _tileImages.ContainsKey(tile) && _tileImages[tile] != null)
            {
                g.DrawImage(_tileImages[tile], x, y, TILE_SIZE, TILE_SIZE);
            }
            else
            {
                // 备用：使用颜色
                using var brush = new SolidBrush(GetTileColor(tile));
                g.FillRectangle(brush, x, y, TILE_SIZE, TILE_SIZE);
            }
            
            // 添加网格线
            using var pen = new Pen(Color.FromArgb(40, 0, 0, 0));
            g.DrawRectangle(pen, x, y, TILE_SIZE, TILE_SIZE);
        }
        
        private void DrawBag(Graphics g)
        {
            if (!_bagOpen || !_gameActive) return;
            
            // 背包背景
            int bagX = 260;
            int bagY = 120;
            int slotSize = 36;
            int slotPadding = 4;
            
            using var bgBrush = new SolidBrush(Color.FromArgb(200, 30, 30, 30));
            using var slotBgBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
            using var selectedBrush = new SolidBrush(Color.FromArgb(100, 100, 255, 100));
            using var borderPen = new Pen(Color.FromArgb(100, 100, 100));
            using var font = new Font("Segoe UI", 10);
            using var countBrush = new SolidBrush(Color.White);
            
            // 绘制背包背景
            int bagWidth = Player.BAG_COLS * (slotSize + slotPadding) + slotPadding;
            int bagHeight = Player.BAG_ROWS * (slotSize + slotPadding) + slotPadding + 30;
            g.FillRectangle(bgBrush, bagX, bagY, bagWidth, bagHeight);
            g.DrawRectangle(borderPen, bagX, bagY, bagWidth, bagHeight);
            
            // 背包标题
            g.DrawString("背包", font, countBrush, bagX + 10, bagY + 8);
            
            // 绘制背包格子 (4x8)
            for (int row = 0; row < Player.BAG_ROWS; row++)
            {
                for (int col = 0; col < Player.BAG_COLS; col++)
                {
                    int slotIndex = row * Player.BAG_COLS + col;
                    int x = bagX + col * (slotSize + slotPadding) + slotPadding;
                    int y = bagY + row * (slotSize + slotPadding) + slotPadding + 30;
                    
                    // 选中状态
                    if (slotIndex == _selectedBagSlot)
                    {
                        g.FillRectangle(selectedBrush, x, y, slotSize, slotSize);
                    }
                    else
                    {
                        g.FillRectangle(slotBgBrush, x, y, slotSize, slotSize);
                    }
                    g.DrawRectangle(borderPen, x, y, slotSize, slotSize);
                    
                    // 绘制物品
                    var stack = _player.Bag[slotIndex];
                    if (stack != null)
                    {
                        DrawItem(g, stack.TileId, stack.Count, x + 2, y + 2, slotSize - 4);
                    }
                }
            }
            
            // 在背包下方绘制快捷栏
            int hotbarX = bagX;
            int hotbarY = bagY + bagHeight + 10;
            
            // 快捷栏标题
            g.DrawString("快捷栏", font, countBrush, hotbarX + 10, hotbarY + 8);
            
            // 绘制快捷栏格子 (8x1 水平)
            int hotbarSlotY = hotbarY + 30;
            int hotbarTotalWidth = Player.HOTBAR_SIZE * (slotSize + slotPadding) + slotPadding;
            
            for (int i = 0; i < Player.HOTBAR_SIZE; i++)
            {
                int x = hotbarX + i * (slotSize + slotPadding) + slotPadding;
                int y = hotbarSlotY;
                
                // 选中状态
                if (i == _player.SelectedHotbarIndex)
                {
                    g.FillRectangle(selectedBrush, x, y, slotSize, slotSize);
                }
                else
                {
                    g.FillRectangle(slotBgBrush, x, y, slotSize, slotSize);
                }
                g.DrawRectangle(borderPen, x, y, slotSize, slotSize);
                
                // 绘制数字标签
                g.DrawString((i + 1).ToString(), font, countBrush, x + 2, y + 2);
                
                // 绘制物品
                var stack = _player.Hotbar[i];
                if (stack != null)
                {
                    DrawItem(g, stack.TileId, stack.Count, x + 2, y + 2, slotSize - 4);
                }
            }
            
            // 绘制鼠标持有的物品（跟随鼠标）
            if (_mouseItem != null)
            {
                // 在鼠标位置绘制物品
                DrawItem(g, _mouseItem.TileId, _mouseItem.Count, _mouseScreenX - slotSize / 2, _mouseScreenY - slotSize / 2, slotSize - 4);
                
                // 绘制半透明边框
                using var mousePen = new Pen(Color.FromArgb(150, 255, 255, 255), 2);
                g.DrawRectangle(mousePen, _mouseScreenX - slotSize / 2 - 2, _mouseScreenY - slotSize / 2 - 2, slotSize, slotSize);
            }
        }
        
        private void DrawHotbar(Graphics g, int screenWidth, int screenHeight)
        {
            if (!_gameActive || _bagOpen) return;
            
            int slotSize = 36;
            int slotPadding = 4;
            int hotbarWidth = slotSize + slotPadding * 2;
            
            // 快捷栏垂直显示在右边
            int hotbarX = screenWidth - hotbarWidth - 20;
            int hotbarY = 120;
            
            using var bgBrush = new SolidBrush(Color.FromArgb(200, 30, 30, 30));
            using var slotBgBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
            using var selectedBrush = new SolidBrush(Color.FromArgb(100, 100, 255, 100));
            using var borderPen = new Pen(Color.FromArgb(100, 100, 100));
            using var font = new Font("Segoe UI", 10);
            using var countBrush = new SolidBrush(Color.White);
            
            int hotbarHeight = Player.HOTBAR_SIZE * (slotSize + slotPadding) + slotPadding;
            g.FillRectangle(bgBrush, hotbarX, hotbarY, hotbarWidth, hotbarHeight);
            g.DrawRectangle(borderPen, hotbarX, hotbarY, hotbarWidth, hotbarHeight);
            
            // 绘制快捷栏 (1x8 垂直)
            for (int i = 0; i < Player.HOTBAR_SIZE; i++)
            {
                int x = hotbarX + slotPadding;
                int y = hotbarY + i * (slotSize + slotPadding) + slotPadding;
                
                // 选中状态
                if (i == _player.SelectedHotbarIndex)
                {
                    g.FillRectangle(selectedBrush, x, y, slotSize, slotSize);
                }
                else
                {
                    g.FillRectangle(slotBgBrush, x, y, slotSize, slotSize);
                }
                g.DrawRectangle(borderPen, x, y, slotSize, slotSize);
                
                // 绘制数字标签
                g.DrawString((i + 1).ToString(), font, countBrush, x + 2, y + 2);
                
                // 绘制物品
                var stack = _player.Hotbar[i];
                if (stack != null)
                {
                    DrawItem(g, stack.TileId, stack.Count, x + 2, y + 2, slotSize - 4);
                }
            }
        }
        
        private void DrawItem(Graphics g, byte tileId, int count, int x, int y, int size)
        {
            TileType tile = (TileType)tileId;
            
            // 绘制物品图标
            if (_tileImages != null && _tileImages.ContainsKey(tile) && _tileImages[tile] != null)
            {
                g.DrawImage(_tileImages[tile], x, y, size, size);
            }
            else
            {
                using var brush = new SolidBrush(GetTileColor(tile));
                g.FillRectangle(brush, x, y, size, size);
            }
            
            // 绘制数量
            if (count > 1)
            {
                using var font = new Font("Segoe UI", 8, FontStyle.Bold);
                using var brush = new SolidBrush(Color.White);
                using var bgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
                
                string countStr = count.ToString();
                SizeF strSize = g.MeasureString(countStr, font);
                g.FillRectangle(bgBrush, x + size - strSize.Width - 2, y + size - strSize.Height - 2, strSize.Width + 2, strSize.Height + 2);
                g.DrawString(countStr, font, brush, x + size - strSize.Width - 2, y + size - strSize.Height - 2);
            }
        }
        
        public void HandleBagClick(int mouseX, int mouseY, bool isRightClick)
        {
            if (!_bagOpen || !_gameActive) return;
            
            int bagX = 260;
            int bagY = 120;
            int slotSize = 36;
            int slotPadding = 4;
            
            // 背包宽度
            int bagWidth = Player.BAG_COLS * (slotSize + slotPadding) + slotPadding;
            int bagHeight = Player.BAG_ROWS * (slotSize + slotPadding) + slotPadding + 30;
            
            // 检查背包格子点击
            for (int row = 0; row < Player.BAG_ROWS; row++)
            {
                for (int col = 0; col < Player.BAG_COLS; col++)
                {
                    int slotIndex = row * Player.BAG_COLS + col;
                    int x = bagX + col * (slotSize + slotPadding) + slotPadding;
                    int y = bagY + row * (slotSize + slotPadding) + slotPadding + 30;
                    
                    if (mouseX >= x && mouseX <= x + slotSize &&
                        mouseY >= y && mouseY <= y + slotSize)
                    {
                        if (isRightClick)
                        {
                            // 右键出售单个物品
                            int value = _player.SellItem(slotIndex, false);
                            if (value > 0)
                            {
                                DebugConsole.Log($"出售物品，获得 {value} 金币");
                                TriggerSell("item");
                            }
                        }
                        else
                        {
                            // 左键拖拽物品
                            HandleItemDrag(slotIndex, true); // true = 背包
                        }
                        return;
                    }
                }
            }
            
            // 检查快捷栏格子点击（在背包下方）
            int hotbarX = bagX;
            int hotbarY = bagY + bagHeight + 10;
            int hotbarSlotY = hotbarY + 30;
            
            for (int i = 0; i < Player.HOTBAR_SIZE; i++)
            {
                int x = hotbarX + i * (slotSize + slotPadding) + slotPadding;
                int y = hotbarSlotY;
                
                if (mouseX >= x && mouseX <= x + slotSize &&
                    mouseY >= y && mouseY <= y + slotSize)
                {
                    if (isRightClick)
                    {
                        // 右键出售单个物品
                        int value = _player.SellItem(i, true); // true = 快捷栏
                        if (value > 0)
                        {
                            DebugConsole.Log($"出售物品，获得 {value} 金币");
                            TriggerSell("item");
                        }
                    }
                    else
                    {
                        // 左键拖拽物品
                        HandleItemDrag(i, false); // false = 快捷栏
                    }
                    return;
                }
            }
        }
        
        private void HandleItemDrag(int slotIndex, bool isBag)
        {
            if (isBag)
            {
                // 从背包拖拽
                var bagItem = _player.Bag[slotIndex];
                
                if (_mouseItem == null)
                {
                    // 鼠标没有物品，拿起背包中的物品
                    if (bagItem != null)
                    {
                        _mouseItem = bagItem;
                        _player.Bag[slotIndex] = null;
                        DebugConsole.Log($"拿起背包物品: TileId={_mouseItem.TileId}, Count={_mouseItem.Count}");
                    }
                }
                else
                {
                    // 鼠标有物品，放下到背包
                    if (bagItem == null)
                    {
                        // 空格，放下物品
                        _player.Bag[slotIndex] = _mouseItem;
                        _mouseItem = null;
                        DebugConsole.Log($"放下物品到背包: Slot={slotIndex}");
                    }
                    else if (bagItem.IsSameType(_mouseItem.TileId))
                    {
                        // 同类物品，堆叠
                        int space = 99 - bagItem.Count;
                        if (space > 0)
                        {
                            int toAdd = Math.Min(space, _mouseItem.Count);
                            bagItem.Add(toAdd);
                            _mouseItem.Remove(toAdd);
                            if (_mouseItem.Count <= 0)
                            {
                                _mouseItem = null;
                            }
                            DebugConsole.Log($"堆叠物品，剩余: {_mouseItem?.Count ?? 0}");
                        }
                    }
                    else
                    {
                        // 不同类物品，交换
                        _player.Bag[slotIndex] = _mouseItem;
                        _mouseItem = bagItem;
                        DebugConsole.Log($"交换物品");
                    }
                }
            }
            else
            {
                // 从快捷栏拖拽
                var hotbarItem = _player.Hotbar[slotIndex];
                
                if (_mouseItem == null)
                {
                    // 鼠标没有物品，拿起快捷栏中的物品
                    if (hotbarItem != null)
                    {
                        _mouseItem = hotbarItem;
                        _player.Hotbar[slotIndex] = null;
                        DebugConsole.Log($"拿起快捷栏物品: TileId={_mouseItem.TileId}, Count={_mouseItem.Count}");
                    }
                }
                else
                {
                    // 鼠标有物品，放下到快捷栏
                    if (hotbarItem == null)
                    {
                        // 空格，放下物品
                        _player.Hotbar[slotIndex] = _mouseItem;
                        _mouseItem = null;
                        DebugConsole.Log($"放下物品到快捷栏: Slot={slotIndex}");
                    }
                    else if (hotbarItem.IsSameType(_mouseItem.TileId))
                    {
                        // 同类物品，堆叠
                        int space = 99 - hotbarItem.Count;
                        if (space > 0)
                        {
                            int toAdd = Math.Min(space, _mouseItem.Count);
                            hotbarItem.Add(toAdd);
                            _mouseItem.Remove(toAdd);
                            if (_mouseItem.Count <= 0)
                            {
                                _mouseItem = null;
                            }
                            DebugConsole.Log($"堆叠物品，剩余: {_mouseItem?.Count ?? 0}");
                        }
                    }
                    else
                    {
                        // 不同类物品，交换
                        _player.Hotbar[slotIndex] = _mouseItem;
                        _mouseItem = hotbarItem;
                        DebugConsole.Log($"交换物品");
                    }
                }
            }
        }
        
        private Color GetTileColor(TileType tile)
        {
            return tile switch
            {
                TileType.Air => Color.FromArgb(135, 206, 235),
                TileType.Grass => Color.FromArgb(124, 156, 76),
                TileType.Dirt => Color.FromArgb(158, 123, 90),
                TileType.Stone => Color.FromArgb(138, 138, 138),
                TileType.Coal => Color.FromArgb(58, 58, 58),
                TileType.Iron => Color.FromArgb(184, 155, 123),
                TileType.Gold => Color.FromArgb(224, 180, 64),
                TileType.Emerald => Color.FromArgb(78, 222, 110),
                TileType.Redstone => Color.FromArgb(208, 64, 64),
                TileType.Diamond => Color.FromArgb(91, 207, 227),
                TileType.Lava => Color.FromArgb(224, 80, 32),
                TileType.Bedrock => Color.FromArgb(26, 26, 26),
                TileType.TNT => Color.FromArgb(196, 48, 48),
                TileType.Sand => Color.FromArgb(232, 209, 116),
                TileType.Cactus => Color.FromArgb(45, 140, 45),
                TileType.Wood => Color.FromArgb(139, 90, 43),
                TileType.Leaves => Color.FromArgb(58, 140, 58),
                TileType.GrassDecor => Color.FromArgb(92, 156, 76),
                _ => Color.Gray
            };
        }
        
        private void DrawDiggingEffect(Graphics g)
        {
            if (!_player.IsDigging) return;
            
            int digIndex = (int)(_player.DigProgress / _world.GetTileHealth(_player.DigX, _player.DigY) * 10);
            digIndex = Math.Min(10, digIndex);
            
            int sx = _player.DigX * TILE_SIZE - (int)_camX;
            int sy = _player.DigY * TILE_SIZE - (int)_camY;
            
            if (_digImages != null && _digImages.ContainsKey(digIndex) && _digImages[digIndex] != null)
            {
                g.DrawImage(_digImages[digIndex], sx, sy, TILE_SIZE, TILE_SIZE);
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(128, 255, 255, 255));
                g.FillRectangle(brush, sx, sy, TILE_SIZE, TILE_SIZE);
            }
        }
        
        private void DrawPlayer(Graphics g)
		{
			float px = _player.X - _camX;
			float py = _player.Y - _camY;
			int catW = (int)_player.Width + 8;
			int catH = (int)_player.Height + 8;
			
			if (_catImage != null)
			{
				// 保存图形状态
				var state = g.Save();
				
				if (_player.Vx < -0.1f)
				{
					// 水平翻转
					g.TranslateTransform(px, py);
					g.ScaleTransform(-1, 1);
					g.DrawImage(_catImage, -catW / 2, -catH, catW, catH);
				}
				else
				{
					g.DrawImage(_catImage, px - catW / 2, py - catH, catW, catH);
				}
				
				// 恢复图形状态
				g.Restore(state);
				
				// 无敌闪烁效果
				if (_player.IsInvincible && (DateTime.Now.Millisecond / 100) % 2 == 0)
				{
					using var brush = new SolidBrush(Color.FromArgb(128, 255, 255, 255));
					g.FillRectangle(brush, px - catW / 2, py - catH, catW, catH);
				}
			}
			else
			{
				// 备用：绘制矩形猫咪
				using var brush = new SolidBrush(Color.FromArgb(244, 162, 97));
				g.FillRectangle(brush, px - _player.Width / 2, py - _player.Height, _player.Width, _player.Height);
				using var brush2 = new SolidBrush(Color.Black);
				g.FillEllipse(brush2, px - 8, py - _player.Height + 12, 4, 4);
				g.FillEllipse(brush2, px + 4, py - _player.Height + 12, 4, 4);
				// 绘制耳朵
				using var brush3 = new SolidBrush(Color.FromArgb(200, 120, 70));
				g.FillPolygon(brush3, new Point[] {
					new Point((int)(px - _player.Width / 2), (int)(py - _player.Height)),
					new Point((int)(px - _player.Width / 2 - 8), (int)(py - _player.Height - 8)),
					new Point((int)(px), (int)(py - _player.Height))
				});
				g.FillPolygon(brush3, new Point[] {
					new Point((int)(px + _player.Width / 2), (int)(py - _player.Height)),
					new Point((int)(px + _player.Width / 2 + 8), (int)(py - _player.Height - 8)),
					new Point((int)(px), (int)(py - _player.Height))
				});
			}
		}
        
        private void DrawHealth(Graphics g)
        {
            // 在左上角显示血量 (x/20 格式)
            int healthX = 10;
            int healthY = 10;
            
            using var font = new Font("Segoe UI", 12, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            using var bgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
            
            string healthStr = $"{_player.Health}/{_player.MaxHealth}";
            SizeF strSize = g.MeasureString(healthStr, font);
            
            // 绘制背景
            g.FillRectangle(bgBrush, healthX - 2, healthY - 2, strSize.Width + 4, strSize.Height + 4);
            
            // 绘制血量文字
            g.DrawString(healthStr, font, brush, healthX, healthY);
        }
        
        private void DrawLighting(Graphics g, int startX, int endX, int startY, int endY)
        {
            for (int x = startX - 3; x <= endX + 3; x++)
            {
                for (int y = startY - 3; y <= endY + 3; y++)
                {
                    if (x < 0 || x >= _world.WorldWidth || y < 0 || y >= WORLD_H) continue;
                    
                    float light = _lightEngine.GetLight(x, y);
                    if (light < 0.99f)
                    {
                        int sx = x * TILE_SIZE - (int)_camX;
                        int sy = y * TILE_SIZE - (int)_camY;
                        int alpha = (int)((1f - light) * 200);
                        if (alpha > 5)
                        {
                            using var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
                            g.FillRectangle(brush, sx, sy, TILE_SIZE, TILE_SIZE);
                        }
                    }
                }
            }
        }
        
        private Color GetSkyColor()
        {
            float t = _dayNightCycle;
            
            // 定义颜色
            Color dayColor = Color.FromArgb(135, 206, 235);      // 白天：浅蓝色
            Color nightColor = Color.FromArgb(10, 10, 40);       // 夜晚：深蓝色
            Color sunriseColor = Color.FromArgb(255, 140, 80);    // 日出：橙红色
            Color sunsetColor = Color.FromArgb(180, 80, 60);      // 日落：红橙色
            
            if (t < 0.2f)
            {
                // 日出过程 0.0 - 0.2 (夜晚到日出)
                float blend = t / 0.2f;
                return LerpColor(nightColor, sunriseColor, blend);
            }
            else if (t < 0.45f)
            {
                // 上午 0.2 - 0.45 (日出到正常白天)
                float blend = (t - 0.2f) / 0.25f;
                return LerpColor(sunriseColor, dayColor, blend);
            }
            else if (t < 0.7f)
            {
                // 下午 0.45 - 0.7 (白天)
                return dayColor;
            }
            else if (t < 0.8f)
            {
                // 日落 0.7 - 0.8 (白天到夜晚)
                float blend = (t - 0.7f) / 0.1f;
                return LerpColor(dayColor, sunsetColor, blend);
            }
            else
            {
                // 夜晚 0.8 - 1.0
                float blend = (t - 0.8f) / 0.2f;
                return LerpColor(sunsetColor, nightColor, blend);
            }
        }
        
        private Color LerpColor(Color c1, Color c2, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            int r = (int)(c1.R + (c2.R - c1.R) * t);
            int g = (int)(c1.G + (c2.G - c1.G) * t);
            int b = (int)(c1.B + (c2.B - c1.B) * t);
            return Color.FromArgb(r, g, b);
        }
        
        public string GetTimeDisplay()
        {
            float t = _dayNightCycle;
            if (t < 0.2f) return "日出";
            if (t < 0.45f) return "上午";
            if (t < 0.55f) return "正午";
            if (t < 0.7f) return "下午";
            if (t < 0.8f) return "日落";
            return "夜晚";
        }
        
        public void StartDigging()
        {
            if (!_gameActive || _paused) return;
            
            int worldX = (int)(_mouseWorldX / TILE_SIZE);
            int worldY = (int)(_mouseWorldY / TILE_SIZE);
            var tile = _world.GetTile(worldX, worldY);
            
            if (tile == TileType.Air || tile == TileType.Bedrock) return;
            
            float maxDistance = 5 * TILE_SIZE;
            float dx = (worldX * TILE_SIZE + TILE_SIZE / 2) - _player.X;
            float dy = (worldY * TILE_SIZE + TILE_SIZE / 2) - _player.Y;
            
            if (MathF.Sqrt(dx * dx + dy * dy) > maxDistance) return;
            
            _player.StartDigging(worldX, worldY);
        }
        
        public void StopDigging() => _player.StopDigging();
        
        public void SellBag()
        {
            if (!_gameActive || _paused) return;
            _player.SellBag(0.5f); // 原地出售扣损50%
        }
        
        public void SellBagAtSpawn()
        {
            if (!_gameActive || _paused) return;
            _player.SellBag(1.0f); // 出生点出售100%价值
            // 传送到出生点
            _player.X = _world.SpawnX * Config.TILE_SIZE + Config.TILE_SIZE / 2;
            _player.Y = _world.SpawnY * Config.TILE_SIZE - _player.Height - 2;
            _player.Vx = 0;
            _player.Vy = 0;
        }
        
        public void UpgradePickaxe()
        {
            if (!_gameActive || _paused) return;
            if (_player.PickaxeLevel >= 6) return;
            
            int cost = PICKAXE_COSTS[_player.PickaxeLevel - 1];
            if (_player.Money >= cost)
            {
                _player.Money -= cost;
                _player.PickaxeLevel++;
                _form.UpdatePickaxeCursor();
                _form.UpdateShopUI();
                TriggerBuy("pickaxe");
            }
        }
        
        public void UpgradeBag()
        {
            // 背包容量不再可升级（固定32格）
        }
        
        public (Image Image, string Name, int NextLevel, int Price, bool IsMaxed) GetPickaxeInfo()
        {
            bool isMaxed = _player.PickaxeLevel >= 6;
            Image img = null;
            try
            {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(currentDir, PICKAXE_IMAGES[Math.Min(_player.PickaxeLevel - 1, 5)]);
                if (File.Exists(path))
                {
                    // 直接加载 PNG，保留透明通道
                    using var originalImg = new Bitmap(path);
                    // 创建副本以避免文件锁定
                    img = new Bitmap(originalImg);
                }
            }
            catch { }
            
            return (img, 
                    isMaxed ? PICKAXE_NAMES[5] : PICKAXE_NAMES[_player.PickaxeLevel - 1],
                    _player.PickaxeLevel + 1,
                    isMaxed ? 0 : PICKAXE_COSTS[_player.PickaxeLevel - 1],
                    isMaxed);
        }
        
        public (int CurrentSize, int Price, bool IsMaxed) GetBagInfo()
        {
            // 背包容量固定32格，不再可升级
            return (Player.BAG_ROWS * Player.BAG_COLS, 0, true);
        }
        
        public Image GetPickaxeCursorImage()
        {
            try
            {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(currentDir, PICKAXE_IMAGES[_player.PickaxeLevel - 1]);
                DebugConsole.Log($"[镐子光标] 尝试加载: {path}");
                DebugConsole.Log($"[镐子光标] 文件存在: {File.Exists(path)}");
                if (File.Exists(path))
                {
                    // 直接加载 PNG，保留透明通道
                    using var originalImg = new Bitmap(path);
                    var img = new Bitmap(originalImg);
                    DebugConsole.Log($"[镐子光标] 加载成功: {img.Width}x{img.Height}");
                    return img;
                }
            }
            catch (Exception ex)
            {
                DebugConsole.Log($"[镐子光标] 加载失败: {ex.Message}");
            }
            return null;
        }
        
        public void UpdateMousePosition(float worldX, float worldY, int screenX = 0, int screenY = 0)
        {
            _mouseWorldX = worldX;
            _mouseWorldY = worldY;
            _mouseScreenX = screenX;
            _mouseScreenY = screenY;
        }
        
        public void KeyDown(Keys key) 
        { 
            _keys[key] = true;
            
            // F3 切换调试模式
            if (key == Keys.F3)
            {
                _debugMode = !_debugMode;
                DebugConsole.Log(_debugMode ? "调试模式已开启" : "调试模式已关闭");
            }
            
            // B键打开/关闭背包
            if (key == Keys.B && _gameActive)
            {
                _bagOpen = !_bagOpen;
                _selectedBagSlot = -1;
                DebugConsole.Log(_bagOpen ? "背包已打开" : "背包已关闭");
            }
            
            if (key == Keys.V && _gameActive)
            {
                if (_form.IsAdvancementsOpen)
                {
                    _form.HideAdvancements();
                }
                else
                {
                    _form.ShowAdvancements();
                }
            }
            
            // 数字键1-8选择快捷栏
            if (_gameActive && !_bagOpen)
            {
                if (key >= Keys.D1 && key <= Keys.D8)
                {
                    int index = key - Keys.D1;
                    _player.SelectHotbar(index);
                    DebugConsole.Log($"选择快捷栏: {index + 1}");
                }
            }
        }
        public void KeyUp(Keys key) => _keys[key] = false;
        public void SetPaused(bool paused) => _paused = paused;
        public void UpdateShopDisplay() => _form.UpdateShopUI();
        
        #region 存档系统
        
        private void LoadWorldsIndex()
        {
            string path = Path.Combine(SAVE_DIR, WORLDS_INDEX_FILE);
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    _worlds = JsonSerializer.Deserialize<List<WorldSaveData>>(json) ?? new List<WorldSaveData>();
                }
                catch { _worlds = new List<WorldSaveData>(); }
            }
            else
            {
                _worlds = new List<WorldSaveData>();
            }
        }
        
        private void SaveWorldsIndex()
        {
            string path = Path.Combine(SAVE_DIR, WORLDS_INDEX_FILE);
            try
            {
                string json = JsonSerializer.Serialize(_worlds);
                File.WriteAllText(path, json);
            }
            catch { }
        }
        
        public void RefreshWorldList()
        {
            _form.ClearWorldList();
            
            if (_worlds.Count == 0)
            {
                _form.SetNoWorldsMessageVisible(true);
                return;
            }
            
            _form.SetNoWorldsMessageVisible(false);
            
            foreach (var world in _worlds.OrderByDescending(w => w.LastPlayed))
            {
                _form.AddWorldToList(world.Id, world.Name, world.GetFormattedLastPlayed(), world.Id == _selectedWorldId);
            }
        }
        
        public void SelectWorld(string id)
        {
            DebugConsole.Log($"SelectWorld 被调用: id={id}");
            _selectedWorldId = id;
        }
        
        public void CreateNewWorld(string name = null)
        {
            // 如果没有提供名称，使用默认名称
            if (string.IsNullOrEmpty(name))
            {
                name = $"世界 {_worlds.Count + 1}";
            }
            
            var newWorld = new WorldSaveData
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                CreatedAt = DateTime.Now.Ticks,
                LastPlayed = DateTime.Now.Ticks
            };
            _worlds.Add(newWorld);
            SaveWorldsIndex();
            
            SaveWorldData(newWorld.Id, new GameSaveData());
            
            RefreshWorldList();
            StartGameWithWorld(newWorld.Id);
        }
        
        public void DeleteSelectedWorld()
        {
            DebugConsole.Log($"删除世界: _selectedWorldId={_selectedWorldId}");
            
            if (string.IsNullOrEmpty(_selectedWorldId))
            {
                DebugConsole.Log("没有选中的世界");
                MessageBox.Show("请先选择一个世界", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var world = _worlds.FirstOrDefault(w => w.Id == _selectedWorldId);
            if (world != null)
            {
                var result = MessageBox.Show($"确定要删除世界 \"{world.Name}\" 吗？\n此操作不可恢复！", 
                    "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                
                if (result == DialogResult.Yes)
                {
                    // 删除新格式的文件夹
                    string worldDir = Path.Combine(SAVE_DIR, world.Id);
                    if (Directory.Exists(worldDir))
                    {
                        Directory.Delete(worldDir, true);
                        DebugConsole.Log($"删除文件夹: {worldDir}");
                    }
                    
                    // 删除旧格式的单文件（兼容旧版本）
                    string oldSavePath = Path.Combine(SAVE_DIR, $"{world.Id}.json");
                    if (File.Exists(oldSavePath))
                    {
                        File.Delete(oldSavePath);
                        DebugConsole.Log($"删除旧格式文件: {oldSavePath}");
                    }
                    
                    _worlds.Remove(world);
                    SaveWorldsIndex();
                    
                    if (_currentWorldId == _selectedWorldId)
                        _currentWorldId = null;
                    
                    _selectedWorldId = null;
                    RefreshWorldList();
                    
                    DebugConsole.Log($"世界 {world.Name} 已删除");
                }
            }
        }
        
        public void StartGameWithWorld(string id)
        {
            DebugConsole.Log($"开始游戏: id={id}");
            
            var worldData = _worlds.FirstOrDefault(w => w.Id == id);
            if (worldData == null)
            {
                DebugConsole.Log($"找不到世界: {id}");
                return;
            }
            
            _currentWorldId = id;
            worldData.LastPlayed = DateTime.Now.Ticks;
            SaveWorldsIndex();
            
            // 重置成就和触发器状态（每个存档独立）
            _completedAdvancements.Clear();
            foreach (var adv in _advancements)
            {
                adv.Completed = false;
            }
            Triggers.Reset(); // 重置触发器
            
            var saveData = LoadWorldData(id);
            if (saveData != null)
            {
                DebugConsole.Log("加载存档");
                _player = new Player();
                LoadGameState(saveData);
            }
            else
            {
                DebugConsole.Log("创建新游戏");
                NewGame();
            }
            
            _gameActive = true;
            _paused = false;
            _justActivated = true;
            
            // 重新加载成就（确保调试控制台已打开，可以看到日志）
            LoadAdvancements();
            
            // 通知 MainForm 开始游戏
            _form.StartGame();
            
            DebugConsole.Log("游戏已启动");
        }
        
        private GameSaveData LoadWorldData(string id)
        {
            // 尝试从新的文件夹结构加载
            string worldDir = Path.Combine(SAVE_DIR, id);
            string blocksPath = Path.Combine(worldDir, "blocks.json");
            string playerPath = Path.Combine(worldDir, "player.json");
            string statePath = Path.Combine(worldDir, "state.json");
            
            if (Directory.Exists(worldDir) && File.Exists(blocksPath) && File.Exists(playerPath))
            {
                try
                {
                    var data = new GameSaveData();
                    
                    // 加载方块数据
                    string blocksJson = File.ReadAllText(blocksPath);
                    data.WorldData = JsonSerializer.Deserialize<object>(blocksJson);
                    
                    // 加载玩家数据
                    string playerJson = File.ReadAllText(playerPath);
                    var playerDoc = System.Text.Json.JsonDocument.Parse(playerJson);
                    var playerRoot = playerDoc.RootElement;
                    
                    data.PlayerX = playerRoot.GetProperty("X").GetSingle();
                    data.PlayerY = playerRoot.GetProperty("Y").GetSingle();
                    data.PlayerMoney = playerRoot.GetProperty("Money").GetInt32();
                    data.PlayerHealth = playerRoot.GetProperty("Health").GetInt32();
                    // BagMax 已移除，背包容量现在固定
                    data.PlayerPickaxeLevel = playerRoot.GetProperty("PickaxeLevel").GetInt32();
                    
                    // 加载背包（支持旧格式）
                    if (playerRoot.TryGetProperty("Bag", out var bagProp))
                    {
                        // 新格式：ItemStack 数组
                        try
                        {
                            data.PlayerBag = bagProp.Deserialize<object[]>();
                        }
                        catch
                        {
                            // 旧格式：base64 字节数组
                            var bagStr = bagProp.GetString();
                            if (!string.IsNullOrEmpty(bagStr))
                            {
                                try
                                {
                                    byte[] oldBag = Encoding.UTF8.GetBytes(bagStr);
                                    data.PlayerBag = oldBag.Cast<object>().ToArray();
                                }
                                catch { }
                            }
                        }
                    }
                    
                    // 加载快捷栏（新格式）
                    if (playerRoot.TryGetProperty("Hotbar", out var hotbarProp))
                    {
                        try
                        {
                            data.PlayerHotbar = hotbarProp.Deserialize<object[]>();
                        }
                        catch { }
                    }
                    
                    // 加载选中的快捷栏索引（新格式）
                    if (playerRoot.TryGetProperty("SelectedHotbarIndex", out var indexProp))
                    {
                        try
                        {
                            data.PlayerSelectedHotbarIndex = indexProp.GetInt32();
                        }
                        catch { }
                    }
                    
                    // 加载状态数据
                    if (File.Exists(statePath))
                    {
                        string stateJson = File.ReadAllText(statePath);
                        var stateDoc = System.Text.Json.JsonDocument.Parse(stateJson);
                        var stateRoot = stateDoc.RootElement;
                        
                        data.DayNightCycle = stateRoot.GetProperty("DayNightCycle").GetSingle();
                        
                        if (stateRoot.TryGetProperty("SpawnX", out var spawnX))
                            data.SpawnX = spawnX.GetInt32();
                        if (stateRoot.TryGetProperty("SpawnY", out var spawnY))
                            data.SpawnY = spawnY.GetInt32();
                        
                        DebugConsole.Log($"加载状态: DayNightCycle={data.DayNightCycle:F2}, Spawn=({data.SpawnX},{data.SpawnY})");
                    }
                    
                    DebugConsole.Log($"从 {worldDir} 加载游戏数据成功");
                    DebugConsole.Log($"玩家位置: ({data.PlayerX}, {data.PlayerY})");
                    return data;
                }
                catch (Exception ex) 
                { 
                    DebugConsole.Log($"加载失败: {ex.Message}");
                }
            }
            
            // 兼容旧的单文件格式
            string oldPath = Path.Combine(SAVE_DIR, $"{id}.json");
            if (File.Exists(oldPath))
            {
                try
                {
                    string json = File.ReadAllText(oldPath);
                    var oldData = JsonSerializer.Deserialize<GameSaveData>(json);
                    DebugConsole.Log($"使用旧格式加载游戏数据");
                    return oldData;
                }
                catch { }
            }
            
            return null;
        }
        
        private void SaveWorldData(string id, GameSaveData data)
        {
            // 创建 world_name 文件夹
            string worldDir = Path.Combine(SAVE_DIR, id);
            try
            {
                if (!Directory.Exists(worldDir))
                {
                    Directory.CreateDirectory(worldDir);
                }
                
                // 保存方块数据到 blocks.json
                string blocksPath = Path.Combine(worldDir, "blocks.json");
                string blocksJson = JsonSerializer.Serialize(data.WorldData);
                File.WriteAllText(blocksPath, blocksJson);
                
                // 创建玩家数据对象
                var playerData = new
                {
                    X = data.PlayerX,
                    Y = data.PlayerY,
                    Money = data.PlayerMoney,
                    Health = data.PlayerHealth,
                    Bag = data.PlayerBag,
                    Hotbar = data.PlayerHotbar,
                    SelectedHotbarIndex = data.PlayerSelectedHotbarIndex,
                    PickaxeLevel = data.PlayerPickaxeLevel
                };
                
                // 保存玩家数据到 player.json
                string playerPath = Path.Combine(worldDir, "player.json");
                string playerJson = JsonSerializer.Serialize(playerData);
                File.WriteAllText(playerPath, playerJson);
                
                // 保存状态数据（包含昼夜循环和重生点）
                string statePath = Path.Combine(worldDir, "state.json");
                var stateData = new 
                { 
                    DayNightCycle = data.DayNightCycle,
                    SpawnX = data.SpawnX,
                    SpawnY = data.SpawnY
                };
                string stateJson = JsonSerializer.Serialize(stateData);
                File.WriteAllText(statePath, stateJson);
                
                DebugConsole.Log($"游戏数据已保存到 {worldDir}");
                DebugConsole.Log($"保存重生点: SpawnX={data.SpawnX}, SpawnY={data.SpawnY}");
            }
            catch (Exception ex) 
            { 
                DebugConsole.Log($"保存失败: {ex.Message}");
            }
        }
        
        private void NewGame()
		{
			DebugConsole.Log("创建新游戏世界");
			_world = new World();
			_world.GenerateNewWorld();
			_player = new Player();
			_player.X = 3000;  // 设置初始X
			int tileX = (int)(_player.X / TILE_SIZE);
			int surfaceY = _world.FindSurfaceY(tileX);
			if (surfaceY < 0) surfaceY = 20;
			_player.Y = surfaceY * TILE_SIZE - _player.Height - 2;
			_player.Vx = _player.Vy = 0;
			_player.Health = _player.MaxHealth;
			_player.Money = 0;
			_player.Bag.Clear();
			for (int i = 0; i < Player.BAG_ROWS * Player.BAG_COLS; i++)
				_player.Bag.Add(null);
			for (int i = 0; i < Player.HOTBAR_SIZE; i++)
				_player.Hotbar.Add(null);
			_player.PickaxeLevel = 1;
			_dayNightCycle = 0;
			
			// 设置玩家重生点
			_player.SpawnX = _player.X;
			_player.SpawnY = _player.Y;
			
			DebugConsole.Log($"新世界创建 - 初始位置: ({_player.X}, {_player.Y}), 速度: ({_player.Vx}, {_player.Vy})");
			DebugConsole.Log($"地表Y坐标: {surfaceY}, 格子大小: {TILE_SIZE}");
			
			// 确保玩家不会卡在地下
			FixPlayerStuckInBlocks(_world, _player);
			
			DebugConsole.Log($"新世界创建完成，玩家位置: ({_player.X}, {_player.Y}), 速度: ({_player.Vx}, {_player.Vy})");
		}
		
		private void LoadGameState(GameSaveData data)
		{
			DebugConsole.Log("========== 开始加载游戏状态 ==========");
			DebugConsole.Log($"当前世界ID: {_currentWorldId}");
			
			if (data.WorldData != null)
			{
				_world.LoadFromData(data.WorldData);
				DebugConsole.Log($"世界已加载，宽度={_world.WorldWidth} 格");
			}
			else
			{
				_world.GenerateNewWorld();
			}
			
			// 恢复重生点
			DebugConsole.Log($"加载重生点检查: data.SpawnX={data.SpawnX}, data.SpawnY={data.SpawnY}");
			// 只有当重生点大于 0 时才使用，否则使用默认重生点
			if (data.SpawnX > 0 && data.SpawnY > 0 && data.SpawnX < _world.WorldWidth)
			{
				_world.SpawnX = data.SpawnX;
				_world.SpawnY = data.SpawnY;
				DebugConsole.Log($"从存档加载重生点: SpawnX={_world.SpawnX}, SpawnY={_world.SpawnY}");
			}
			else
			{
				// 如果没有保存的重生点，使用世界中心
				_world.SpawnX = _world.WorldWidth / 2;
				_world.SpawnY = _world.FindSurfaceY(_world.SpawnX);
				DebugConsole.Log($"使用默认重生点: SpawnX={_world.SpawnX}, SpawnY={_world.SpawnY}");
			}
			
			// 从保存数据恢复玩家
			DebugConsole.Log($"存档中的玩家数据: PlayerX={data.PlayerX:F2}, PlayerY={data.PlayerY:F2}");
			DebugConsole.Log($"世界范围: 0 到 {_world.WorldWidth * TILE_SIZE}");
			
			// 清空背包和快捷栏，防止旧存档数据残留
			for (int i = 0; i < _player.Bag.Count; i++)
			{
				_player.Bag[i] = null;
			}
			for (int i = 0; i < _player.Hotbar.Count; i++)
			{
				_player.Hotbar[i] = null;
			}
			
			// 确保玩家位置在有效范围内
			bool positionValid = data.PlayerX >= 0 && data.PlayerX <= _world.WorldWidth * TILE_SIZE &&
			                     data.PlayerY >= 0 && data.PlayerY <= 120 * TILE_SIZE;
			
			if (positionValid)
			{
				_player.X = data.PlayerX;
				_player.Y = data.PlayerY;
				_player.Money = data.PlayerMoney;
				_player.Health = data.PlayerHealth > 0 ? data.PlayerHealth : _player.MaxHealth;
				// 背包现在是 ItemStack 类型，由 Player.LoadFromData 处理
				_player.PickaxeLevel = data.PlayerPickaxeLevel > 0 ? data.PlayerPickaxeLevel : 1;
				
				// 加载背包数据
				if (data.PlayerBag != null)
				{
					for (int i = 0; i < _player.Bag.Count && i < data.PlayerBag.Length; i++)
					{
						var item = data.PlayerBag[i];
						if (item != null)
						{
							// 处理 JsonElement 类型
							if (item is System.Text.Json.JsonElement jsonElement)
							{
								if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
								{
									byte tileId = 0;
									int count = 0;
									
									if (jsonElement.TryGetProperty("TileId", out var tileIdProp))
										tileId = tileIdProp.GetByte();
									if (jsonElement.TryGetProperty("Count", out var countProp))
										count = countProp.GetInt32();
									
									if (tileId > 0 || count > 0)
									{
										_player.Bag[i] = new ItemStack(tileId, count);
									}
								}
							}
							// 处理 Dictionary 类型
							else if (item is System.Collections.Generic.Dictionary<string, object> dict)
							{
								if (dict.ContainsKey("TileId") && dict.ContainsKey("Count"))
								{
									_player.Bag[i] = new ItemStack(Convert.ToByte(dict["TileId"]), Convert.ToInt32(dict["Count"]));
								}
							}
						}
						else
						{
							_player.Bag[i] = null;
						}
					}
					DebugConsole.Log($"背包数据已加载，共 {data.PlayerBag.Length} 个格子");
				}
				
				// 加载快捷栏数据
				if (data.PlayerHotbar != null)
				{
					for (int i = 0; i < _player.Hotbar.Count && i < data.PlayerHotbar.Length; i++)
					{
						var item = data.PlayerHotbar[i];
						if (item != null)
						{
							// 处理 JsonElement 类型
							if (item is System.Text.Json.JsonElement jsonElement)
							{
								if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
								{
									byte tileId = 0;
									int count = 0;
									
									if (jsonElement.TryGetProperty("TileId", out var tileIdProp))
										tileId = tileIdProp.GetByte();
									if (jsonElement.TryGetProperty("Count", out var countProp))
										count = countProp.GetInt32();
									
									if (tileId > 0 || count > 0)
									{
										_player.Hotbar[i] = new ItemStack(tileId, count);
									}
								}
							}
							// 处理 Dictionary 类型
							else if (item is System.Collections.Generic.Dictionary<string, object> dict)
							{
								if (dict.ContainsKey("TileId") && dict.ContainsKey("Count"))
								{
									_player.Hotbar[i] = new ItemStack(Convert.ToByte(dict["TileId"]), Convert.ToInt32(dict["Count"]));
								}
							}
						}
						else
						{
							_player.Hotbar[i] = null;
						}
					}
					DebugConsole.Log($"快捷栏数据已加载，共 {data.PlayerHotbar.Length} 个格子");
				}
				
				// 加载选中的快捷栏索引
				_player.SelectedHotbarIndex = data.PlayerSelectedHotbarIndex;
				DebugConsole.Log($"选中的快捷栏索引: {_player.SelectedHotbarIndex}");
				
				// 加载成就数据
				LoadAdvancementsFromSave();
				
				DebugConsole.Log($"成功加载玩家位置: ({_player.X:F2}, {_player.Y:F2})");
				
				// 检查玩家是否在地表上方，如果在地表下方，使用重生点
				int playerTileX = (int)(_player.X / TILE_SIZE);
				int surfaceY = _world.FindSurfaceY(playerTileX);
				int playerTileY = (int)(_player.Y / TILE_SIZE);
				
				DebugConsole.Log($"玩家格子位置: ({playerTileX}, {playerTileY}), 地表Y: {surfaceY}");
				
				// 如果玩家在地表下方，使用重生点位置
				if (playerTileY > surfaceY + 2)
				{
					_player.X = _world.SpawnX * TILE_SIZE + TILE_SIZE / 2;
					_player.Y = _world.SpawnY * TILE_SIZE - _player.Height - 2;
					DebugConsole.Log($"玩家在地表下方，重新定位到重生点: ({_player.X:F2}, {_player.Y:F2})");
				}
			}
			else
			{
				// 如果保存的位置无效，使用重生点
				_player.X = _world.SpawnX * TILE_SIZE + TILE_SIZE / 2;
				_player.Y = _world.SpawnY * TILE_SIZE - _player.Height - 2;
				DebugConsole.Log($"位置无效，使用重生点位置: ({_player.X:F2}, {_player.Y:F2})");
			}
			
			// 检查并修复玩家位置（防止卡进地里）
			FixPlayerStuckInBlocks(_world, _player);
			
			_player.Vx = _player.Vy = 0;
			_player.ResetInvincible();
			_dayNightCycle = data.DayNightCycle;
			
			// 设置玩家重生点
			_player.SpawnX = _world.SpawnX * TILE_SIZE + TILE_SIZE / 2;
			_player.SpawnY = _world.SpawnY * TILE_SIZE - _player.Height - 2;
			
			DebugConsole.Log($"========== 游戏状态加载完成 ==========");
			DebugConsole.Log($"最终位置: ({_player.X:F2}, {_player.Y:F2})");
			DebugConsole.Log($"速度: Vx={_player.Vx}, Vy={_player.Vy}");
			DebugConsole.Log($"重生点: ({_player.SpawnX:F2}, {_player.SpawnY:F2})");
			DebugConsole.Log($"=========================================");
		}
		
		private void FixPlayerStuckInBlocks(World world, Player player)
		{
			float halfW = player.Width / 2;
			float playerBottom = player.Y;
			float playerTop = player.Y - player.Height;
			float playerLeft = player.X - halfW;
			float playerRight = player.X + halfW;
			
			// 检查玩家底部是否在方块内部（穿过地面）
			int bottomTileY = (int)(playerBottom / TILE_SIZE);
			for (int x = (int)(playerLeft / TILE_SIZE); x <= (int)(playerRight / TILE_SIZE); x++)
			{
				if (x < 0 || x >= world.WorldWidth) continue;
				if (bottomTileY < 0 || bottomTileY >= WORLD_H) continue;
				
				var tileType = world.GetTile(x, bottomTileY);
				if (tileType != TileType.Air && tileType != TileType.Lava && tileType != TileType.GrassDecor)
				{
					float tileTop = bottomTileY * TILE_SIZE;
					// 如果玩家底部低于方块顶部，说明卡在地下了
					if (playerBottom > tileTop)
					{
						int surfaceTileY = world.FindSurfaceY(x);
						player.Y = surfaceTileY * TILE_SIZE - player.Height - 2;
						player.Vx = 0;
						player.Vy = 0;
						DebugConsole.Log($"玩家位置修正 - 底部卡在方块内，移到地表 Y={player.Y}");
						return;
					}
				}
			}
			
			// 检查玩家是否完全在方块内部（身体被嵌入）
			for (int y = (int)(playerTop / TILE_SIZE); y <= (int)(playerBottom / TILE_SIZE); y++)
			{
				if (y < 0 || y >= WORLD_H) continue;
				
				for (int x = (int)(playerLeft / TILE_SIZE); x <= (int)(playerRight / TILE_SIZE); x++)
				{
					if (x < 0 || x >= world.WorldWidth) continue;
					
					var tileType = world.GetTile(x, y);
					if (tileType != TileType.Air && tileType != TileType.Lava && tileType != TileType.GrassDecor)
					{
						// 玩家被嵌入到方块中了，需要移动到地表
						int tileX = (int)(player.X / TILE_SIZE);
						int surfaceY = world.FindSurfaceY(tileX);
						player.Y = surfaceY * TILE_SIZE - player.Height - 2;
						player.Vx = 0;
						player.Vy = 0;
						DebugConsole.Log($"玩家位置修正 - 嵌入方块中，移到地表 Y={player.Y}");
						return;
					}
				}
			}
		}
		
        public void SaveCurrentWorld()
		{
			if (!_gameActive || string.IsNullOrEmpty(_currentWorldId)) return;
			
			DebugConsole.Log($"保存世界: {_currentWorldId}");
			
			var saveData = new GameSaveData
			{
				WorldData = _world.Serialize(),
				DayNightCycle = _dayNightCycle,
				PlayerX = _player.X,        // 保存玩家X坐标
				PlayerY = _player.Y,        // 保存玩家Y坐标
				PlayerMoney = _player.Money, // 保存金钱
				PlayerHealth = _player.Health, // 保存血量
				PlayerBag = _player.Bag.Select(s => s != null ? new { TileId = s.TileId, Count = s.Count } : null).ToArray(), // 保存背包
				PlayerHotbar = _player.Hotbar.Select(s => s != null ? new { TileId = s.TileId, Count = s.Count } : null).ToArray(), // 保存快捷栏
				PlayerSelectedHotbarIndex = _player.SelectedHotbarIndex, // 保存选中的快捷栏索引
				PlayerPickaxeLevel = _player.PickaxeLevel, // 保存镐子等级
				SpawnX = _world.SpawnX, // 保存重生点X
				SpawnY = _world.SpawnY  // 保存重生点Y
			};
			
			SaveWorldData(_currentWorldId, saveData);
			
			var world = _worlds.FirstOrDefault(w => w.Id == _currentWorldId);
			if (world != null)
			{
				world.LastPlayed = DateTime.Now.Ticks;
				SaveWorldsIndex();
			}
			
			SaveAdvancements();
		}
        
        public void SaveAndQuitToTitle()
        {
            SaveCurrentWorld();
            _gameActive = false;
            _paused = false;
            _form.ShowMainMenu();
        }
        
        #endregion
    }
    
    public class WorldSaveData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public long CreatedAt { get; set; }
        public long LastPlayed { get; set; }
        
        public string GetFormattedLastPlayed()
        {
            var date = new DateTime(LastPlayed);
            return $"{date.Year}/{date.Month}/{date.Day} {date.Hour:D2}:{date.Minute:D2}";
        }
    }
    
    public class GameSaveData
	{
		public object WorldData { get; set; }
		public object PlayerData { get; set; }
		public float DayNightCycle { get; set; }
		
		// 玩家数据
		public float PlayerX { get; set; }
		public float PlayerY { get; set; }
		public int PlayerMoney { get; set; }
		public int PlayerHealth { get; set; }
		public object[] PlayerBag { get; set; }  // 改为 object[] 支持 ItemStack 格式
		public object[] PlayerHotbar { get; set; }  // 新增：快捷栏
		public int PlayerSelectedHotbarIndex { get; set; }  // 新增：选中的快捷栏索引
		public int PlayerPickaxeLevel { get; set; }
		
		// 重生点
		public int SpawnX { get; set; }
		public int SpawnY { get; set; }
		
		// 成就数据
		public object[] CompletedAdvancements { get; set; }
	}
	
	public class Advancement
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public string Id { get; set; }
		public List<string> Trigger { get; set; }
		public string Img { get; set; }
		public string Prev { get; set; }
		public bool Completed { get; set; }
		public Image Icon { get; set; }
	}
	
	public partial class Game
	{
		private void LoadAdvancements()
		{
			_advancements.Clear();
			string currentDir = AppDomain.CurrentDomain.BaseDirectory;
			string advDir = Path.Combine(currentDir, ADVANCEMENTS_DIR);
			
			DebugConsole.Log($"成就目录: {advDir}");
			DebugConsole.Log($"目录存在: {Directory.Exists(advDir)}");
			
			if (!Directory.Exists(advDir))
			{
				DebugConsole.Log("成就目录不存在");
				return;
			}
			
			string[] files = Directory.GetFiles(advDir, "*.json");
			DebugConsole.Log($"找到 {files.Length} 个成就文件");
			
			foreach (string file in files)
			{
				DebugConsole.Log($"加载成就文件: {file}");
				try
				{
					string json = File.ReadAllText(file);
					var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
					var adv = JsonSerializer.Deserialize<Advancement>(json, options);
					if (adv != null && !string.IsNullOrEmpty(adv.Id))
					{
						string imgPath = Path.Combine(currentDir, "src/img", adv.Img);
						DebugConsole.Log($"图标路径: {imgPath}, 存在: {File.Exists(imgPath)}");
						if (File.Exists(imgPath))
						{
							try
							{
								adv.Icon = Image.FromFile(imgPath);
								DebugConsole.Log($"图标加载成功: {adv.Img}");
							}
							catch (Exception ex)
							{
								DebugConsole.Log($"图标加载失败: {ex.Message}");
							}
						}
						_advancements.Add(adv);
						DebugConsole.Log($"成就加载成功: {adv.Id} - {adv.Name}");
					}
				}
				catch (Exception ex)
				{
					DebugConsole.Log($"成就加载失败: {ex.Message}");
				}
			}
			
			Triggers.RegisterCallback("mine", OnMineTrigger);
			Triggers.RegisterCallback("buy", OnBuyTrigger);
			Triggers.RegisterCallback("sell", OnSellTrigger);
			DebugConsole.Log($"成就系统初始化完成，共 {_advancements.Count} 个成就");
		}
		
		private void OnMineTrigger(string triggerId)
		{
			DebugConsole.Log($"触发器触发: {triggerId}");
			CheckAdvancements();
		}
		
		private void OnBuyTrigger(string triggerId)
		{
			DebugConsole.Log($"触发器触发: {triggerId}");
			CheckAdvancements();
		}
		
		private void OnSellTrigger(string triggerId)
		{
			DebugConsole.Log($"触发器触发: {triggerId}");
			CheckAdvancements();
		}
		
		private void CheckAdvancements()
		{
			bool hasFirstDirt = _completedAdvancements.Contains("mine_dirt");
			DebugConsole.Log($"检查成就: hasFirstDirt={hasFirstDirt}, 成就数量={_advancements.Count}");
			
			foreach (var adv in _advancements)
			{
				if (_completedAdvancements.Contains(adv.Id)) continue;
				
				if (!hasFirstDirt && adv.Id != "mine_dirt") continue;
				
				if (!string.IsNullOrEmpty(adv.Prev) && !_completedAdvancements.Contains(adv.Prev)) continue;
				
				DebugConsole.Log($"检查成就: {adv.Id}, 触发器数量={adv.Trigger?.Count ?? 0}");
				
				bool allTriggersActive = true;
				foreach (var trigger in adv.Trigger)
				{
					bool isActive = Triggers.IsActive(trigger);
					DebugConsole.Log($"  触发器 {trigger}: active={isActive}");
					if (!isActive)
					{
						allTriggersActive = false;
						break;
					}
				}
				
				if (allTriggersActive)
				{
					_completedAdvancements.Add(adv.Id);
					adv.Completed = true;
					_pendingNotification = adv.Id;
					_notificationTimer = 3f;
					DebugConsole.Log($"成就解锁: {adv.Name}");
				}
			}
		}
		
		private void TriggerMine(TileType tileType)
		{
			string triggerId = "";
			switch (tileType)
			{
				case TileType.Dirt:
					triggerId = "mine.dirt";
					break;
				case TileType.Stone:
					triggerId = "mine.stone";
					break;
				case TileType.Coal:
					triggerId = "mine.coal";
					break;
				case TileType.Iron:
					triggerId = "mine.iron";
					break;
				case TileType.Gold:
					triggerId = "mine.gold";
					break;
				case TileType.Diamond:
					triggerId = "mine.diamond";
					break;
				case TileType.Emerald:
					triggerId = "mine.emerald";
					break;
				case TileType.Redstone:
					triggerId = "mine.redstone";
					break;
			}
			
			if (!string.IsNullOrEmpty(triggerId))
			{
				Triggers.Trigger(triggerId);
			}
		}
		
		private void TriggerBuy(string item)
		{
			Triggers.Trigger($"buy.{item}");
		}
		
		private void TriggerSell(string item)
		{
			Triggers.Trigger($"sell.{item}");
		}
		
		public void DrawAdvancementNotification(Graphics g)
		{
			if (_pendingNotification == null || _notificationTimer <= 0) return;
			
			var adv = _advancements.FirstOrDefault(a => a.Id == _pendingNotification);
			if (adv == null) return;
			
			// 右上角显示
			int width = 280;
			int height = 60;
			int x = 800 - width - 20; // 右边缘 - 宽度 - 边距
			int y = 20; // 顶部边距
			
			using var bgBrush = new SolidBrush(Color.FromArgb(220, 40, 40, 40));
			using var borderPen = new Pen(Color.FromArgb(255, 215, 0), 2);
			using var font = new Font("Segoe UI", 12, FontStyle.Bold);
			using var nameBrush = new SolidBrush(Color.FromArgb(255, 215, 0));
			using var descFont = new Font("Segoe UI", 9);
			using var descBrush = new SolidBrush(Color.White);
			
			g.FillRectangle(bgBrush, x, y, width, height);
			g.DrawRectangle(borderPen, x, y, width, height);
			
			if (adv.Icon != null)
			{
				g.DrawImage(adv.Icon, x + 8, y + 8, 44, 44);
			}
			
			g.DrawString("成就解锁！", font, nameBrush, x + 60, y + 5);
			g.DrawString(adv.Name, descFont, descBrush, x + 60, y + 30);
			
			_notificationTimer -= 0.016f;
			if (_notificationTimer <= 0)
			{
				_pendingNotification = null;
			}
		}
		
		public void DrawAdvancementsPanel(Graphics g, int screenWidth, int screenHeight)
		{
			int panelX = 50;
			int panelY = 50;
			int panelWidth = screenWidth - 100;
			int panelHeight = screenHeight - 100;
			
			using var bgBrush = new SolidBrush(Color.FromArgb(220, 30, 30, 30));
			using var borderPen = new Pen(Color.FromArgb(100, 100, 100));
			using var titleFont = new Font("Segoe UI", 20, FontStyle.Bold);
			using var titleBrush = new SolidBrush(Color.White);
			
			g.FillRectangle(bgBrush, panelX, panelY, panelWidth, panelHeight);
			g.DrawRectangle(borderPen, panelX, panelY, panelWidth, panelHeight);
			
			g.DrawString("成就", titleFont, titleBrush, panelX + 20, panelY + 15);
			
			bool hasFirstDirt = _completedAdvancements.Contains("mine_dirt");
			
			int iconSize = 50;
			int horizontalSpacing = 80;
			int verticalSpacing = 70;
			
			var positionMap = new Dictionary<string, Point>();
			
			int rootX = panelX + 60;
			int rootY = panelY + panelHeight / 2;
			
			var rootAdv = _advancements.FirstOrDefault(a => string.IsNullOrEmpty(a.Prev));
			if (rootAdv != null)
			{
				positionMap[rootAdv.Id] = new Point(rootX, rootY);
			}
			
			var depthMap = new Dictionary<string, int>();
			BuildAdvancementTree(positionMap, depthMap, rootAdv, rootX, rootY, horizontalSpacing, verticalSpacing);
			
			DrawAdvancementConnections(g, positionMap, iconSize);
			
			// 检测悬浮的成就
			_hoveredAdvancementId = null;
			foreach (var kvp in positionMap)
			{
				var adv = _advancements.FirstOrDefault(a => a.Id == kvp.Key);
				if (adv == null) continue;
				
				if (!hasFirstDirt && adv.Id != "mine_dirt") continue;
				
				if (!string.IsNullOrEmpty(adv.Prev) && !_completedAdvancements.Contains(adv.Prev)) continue;
				
				DrawAdvancementIcon(g, adv, kvp.Value.X, kvp.Value.Y, iconSize);
				
				// 检测鼠标是否在此图标上
				if (_mouseScreenX >= kvp.Value.X && _mouseScreenX <= kvp.Value.X + iconSize &&
					_mouseScreenY >= kvp.Value.Y && _mouseScreenY <= kvp.Value.Y + iconSize)
				{
					_hoveredAdvancementId = adv.Id;
				}
			}
			
			// 绘制悬浮提示
			if (_hoveredAdvancementId != null)
			{
				var hoveredAdv = _advancements.FirstOrDefault(a => a.Id == _hoveredAdvancementId);
				if (hoveredAdv != null)
				{
					DrawAdvancementTooltip(g, hoveredAdv);
				}
			}
		}
		
		private void BuildAdvancementTree(Dictionary<string, Point> positions, Dictionary<string, int> depths, Advancement root, int startX, int startY, int hSpacing, int vSpacing)
		{
			if (root == null) return;
			
			depths[root.Id] = 0;
			
			var children = _advancements.Where(a => a.Prev == root.Id).ToList();
			
			if (children.Count == 0) return;
			
			int currentX = startX + hSpacing;
			
			for (int i = 0; i < children.Count; i++)
			{
				var child = children[i];
				
				if (children.Count == 1)
				{
					positions[child.Id] = new Point(currentX, startY);
				}
				else
				{
					int totalHeight = (children.Count - 1) * vSpacing;
					int offsetY = startY - totalHeight / 2 + i * vSpacing;
					positions[child.Id] = new Point(currentX, offsetY);
				}
				
				BuildAdvancementTree(positions, depths, child, currentX, positions[child.Id].Y, hSpacing, vSpacing);
			}
		}
		
		private void DrawAdvancementConnections(Graphics g, Dictionary<string, Point> positions, int iconSize)
		{
			bool hasFirstDirt = _completedAdvancements.Contains("mine_dirt");
			
			using var linePen = new Pen(Color.FromArgb(150, 150, 150), 2);
			using var completedLinePen = new Pen(Color.FromArgb(255, 215, 0), 2);
			
			foreach (var adv in _advancements)
			{
				if (!hasFirstDirt && adv.Id != "mine_dirt") continue;
				
				if (string.IsNullOrEmpty(adv.Prev)) continue;
				
				if (!_completedAdvancements.Contains(adv.Prev)) continue;
				
				if (positions.TryGetValue(adv.Id, out var childPos) && positions.TryGetValue(adv.Prev, out var parentPos))
				{
					int parentCenterX = parentPos.X + iconSize / 2;
					int parentCenterY = parentPos.Y + iconSize / 2;
					int childCenterX = childPos.X + iconSize / 2;
					int childCenterY = childPos.Y + iconSize / 2;
					
					bool isCompleted = _completedAdvancements.Contains(adv.Id) && _completedAdvancements.Contains(adv.Prev);
					g.DrawLine(isCompleted ? completedLinePen : linePen, parentCenterX, parentCenterY, childCenterX, childCenterY);
				}
			}
		}
		
		private void DrawAdvancementIcon(Graphics g, Advancement adv, int x, int y, int size)
		{
			using var slotBrush = adv.Completed 
				? new SolidBrush(Color.FromArgb(80, 215, 215, 0)) 
				: new SolidBrush(Color.FromArgb(60, 60, 60));
			using var slotPen = adv.Completed 
				? new Pen(Color.FromArgb(255, 215, 0)) 
				: new Pen(Color.FromArgb(100, 100, 100));
			
			g.FillRectangle(slotBrush, x, y, size, size);
			g.DrawRectangle(slotPen, x, y, size, size);
			
			if (adv.Icon != null)
			{
				g.DrawImage(adv.Icon, x + 3, y + 3, size - 6, size - 6);
			}
			
			if (adv.Completed)
			{
				using var glowBrush = new SolidBrush(Color.FromArgb(50, 255, 215, 0));
				g.FillRectangle(glowBrush, x - 2, y - 2, size + 4, size + 4);
			}
		}
		
		public void DrawAdvancementTooltip(Graphics g, string advId, int mouseX, int mouseY)
		{
			var adv = _advancements.FirstOrDefault(a => a.Id == advId);
			if (adv == null) return;
			
			DrawAdvancementTooltipBox(g, adv, mouseX + 15, mouseY + 15);
		}
		
		public void DrawAdvancementTooltip(Graphics g, Advancement adv)
		{
			DrawAdvancementTooltipBox(g, adv, _mouseScreenX + 15, _mouseScreenY + 15);
		}
		
		private void DrawAdvancementTooltipBox(Graphics g, Advancement adv, int baseX, int baseY)
		{
			int width = 220;
			int height = 70;
			
			int x = baseX;
			int y = baseY;
			
			// 确保提示框不超出屏幕
			if (x + width > 800) x = baseX - width - 15;
			if (y + height > 600) y = baseY - height - 15;
			
			using var bgBrush = new SolidBrush(Color.FromArgb(240, 50, 50, 50));
			using var borderPen = new Pen(Color.FromArgb(150, 150, 150));
			using var nameFont = new Font("Segoe UI", 11, FontStyle.Bold);
			using var descFont = new Font("Segoe UI", 9);
			using var nameBrush = adv.Completed 
				? new SolidBrush(Color.FromArgb(255, 215, 0)) 
				: new SolidBrush(Color.White);
			using var descBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
			
			g.FillRectangle(bgBrush, x, y, width, height);
			g.DrawRectangle(borderPen, x, y, width, height);
			
			g.DrawString(adv.Name, nameFont, nameBrush, x + 10, y + 8);
			g.DrawString(adv.Description, descFont, descBrush, x + 10, y + 35);
			
			string status = adv.Completed ? "已完成" : "未完成";
			using var statusBrush = adv.Completed 
				? new SolidBrush(Color.FromArgb(255, 215, 0)) 
				: new SolidBrush(Color.FromArgb(150, 150, 150));
			g.DrawString(status, descFont, statusBrush, x + 10, y + 50);
		}
		
		public string GetAdvancementAt(int x, int y, int screenWidth, int screenHeight)
		{
			int panelX = 50;
			int panelY = 50;
			int panelWidth = screenWidth - 100;
			int panelHeight = screenHeight - 100;
			
			if (x < panelX || x > panelX + panelWidth || y < panelY || y > panelY + panelHeight)
			{
				return null;
			}
			
			bool hasFirstDirt = _completedAdvancements.Contains("mine_dirt");
			
			int iconSize = 50;
			int horizontalSpacing = 80;
			int verticalSpacing = 70;
			
			var positionMap = new Dictionary<string, Point>();
			
			int rootX = panelX + 60;
			int rootY = panelY + panelHeight / 2;
			
			var rootAdv = _advancements.FirstOrDefault(a => string.IsNullOrEmpty(a.Prev));
			if (rootAdv != null)
			{
				positionMap[rootAdv.Id] = new Point(rootX, rootY);
			}
			
			var depthMap = new Dictionary<string, int>();
			BuildAdvancementTree(positionMap, depthMap, rootAdv, rootX, rootY, horizontalSpacing, verticalSpacing);
			
			foreach (var kvp in positionMap)
			{
				var adv = _advancements.FirstOrDefault(a => a.Id == kvp.Key);
				if (adv == null) continue;
				
				if (!hasFirstDirt && adv.Id != "mine_dirt") continue;
				
				if (!string.IsNullOrEmpty(adv.Prev) && !_completedAdvancements.Contains(adv.Prev)) continue;
				
				int iconX = kvp.Value.X;
				int iconY = kvp.Value.Y;
				
				if (x >= iconX && x <= iconX + iconSize && y >= iconY && y <= iconY + iconSize)
				{
					return adv.Id;
				}
			}
			
			return null;
		}
		
		public void SaveAdvancements()
		{
			if (string.IsNullOrEmpty(_currentWorldId)) return;
			
			string savePath = Path.Combine(SAVE_DIR, _currentWorldId, "advancements.json");
			var data = new { Completed = _completedAdvancements.ToArray() };
			string json = JsonSerializer.Serialize(data);
			File.WriteAllText(savePath, json);
		}
		
		public void LoadAdvancementsFromSave()
		{
			if (string.IsNullOrEmpty(_currentWorldId)) return;
			
			string savePath = Path.Combine(SAVE_DIR, _currentWorldId, "advancements.json");
			if (File.Exists(savePath))
			{
				try
				{
					string json = File.ReadAllText(savePath);
					var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
					if (data != null && data.ContainsKey("Completed"))
					{
						var completed = (JsonElement)data["Completed"];
						if (completed.ValueKind == JsonValueKind.Array)
						{
							foreach (var item in completed.EnumerateArray())
							{
								_completedAdvancements.Add(item.GetString());
							}
						}
					}
				}
				catch { }
			}
			
			foreach (var adv in _advancements)
			{
				adv.Completed = _completedAdvancements.Contains(adv.Id);
			}
		}
	}
}