using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;  // 添加这一行

namespace CatCraft
{
    public class Player
    {
        private const float GRAVITY = 800f;
        private const float MOVE_SPEED = 250f;
        private const float JUMP_SPEED = -420f;
        private const int TILE_SIZE = 40;
        
        public float X { get; set; }
        public float Y { get; set; }
        public float Vx { get; set; }
        public float Vy { get; set; }
        public float Width { get; set; } = 32;
        public float Height { get; set; } = 40;
        public bool OnGround { get; set; }
        
        public int Health { get; set; }
        public int MaxHealth { get; set; } = 20;
        public int Money { get; set; }
        public List<byte> Bag { get; set; }
        public int BagMax { get; set; }
        public int PickaxeLevel { get; set; }
        
        // 重生点
        public float SpawnX { get; set; }
        public float SpawnY { get; set; }
        
        public bool IsDigging { get; private set; }
        public int DigX { get; private set; }
        public int DigY { get; private set; }
        public float DigProgress { get; set; }
        
        public bool IsInvincible { get; set; }  // 改成 public
        
        private float _invincibleTimer;
        private float _healTimer;
        private float _fallStartY;
        
        private static readonly Dictionary<byte, int> TileValues = new()
        {
            { 2, 20 }, { 3, 20 }, { 4, 50 }, { 14, 15 },
            { 5, 50 }, { 6, 75 }, { 7, 150 }, { 8, 175 },
            { 9, 250 }, { 10, 750 }, { 15, 10 }, { 16, 30 }, { 17, 5 }
        };
        
		
		
        public Player()
        {
            Bag = new List<byte>();
            BagMax = 10;
            PickaxeLevel = 1;
            Health = MaxHealth;
            Vx = 0;
            Vy = 0;
        }
        
		public void ResetInvincible()
		{
			IsInvincible = false;
			_invincibleTimer = 0;
		}
		
        public void Reset(World world)
		{
			// 不要重置到固定位置，保持当前坐标
			// 只在需要时设置初始位置
			if (X == 0 && Y == 0)
			{
				X = 3000;
				int tileX = (int)(X / TILE_SIZE);
				int surfaceY = world.FindSurfaceY(tileX);
				if (surfaceY < 0) surfaceY = 20;
				Y = surfaceY * TILE_SIZE - Height - 2;
			}
			
			Vx = Vy = 0;
			
			// 只有血量过低时才重置血量
			if (Health <= 0)
			{
				Health = MaxHealth;
				// 移动到重生点
				X = SpawnX;
				Y = SpawnY;
				Vx = 0;
				Vy = 0;
			}
			
			IsDigging = false;
			IsInvincible = false;
			_invincibleTimer = 0;
			_healTimer = 0;
			_fallStartY = Y;
		}
        
        public void Update(float dt, World world, Dictionary<Keys, bool> keys)
        {
            // 移动输入
            float inputX = 0;
            if (keys.GetValueOrDefault(Keys.A) || keys.GetValueOrDefault(Keys.Left)) inputX--;
            if (keys.GetValueOrDefault(Keys.D) || keys.GetValueOrDefault(Keys.Right)) inputX++;
            
            // 更新速度
            Vx = inputX * MOVE_SPEED;
            
            // 重力
            Vy += GRAVITY * dt;
            
            // 跳跃
            if ((keys.GetValueOrDefault(Keys.W) || keys.GetValueOrDefault(Keys.Space) || keys.GetValueOrDefault(Keys.Up)) && OnGround)
            {
                Vy = JUMP_SPEED;
                OnGround = false;
            }
            
            // 计算新位置
            float newX = X + Vx * dt;
            float newY = Y + Vy * dt;
            
            // 使用格子级别的碰撞检测
            CheckTileCollision(world, ref newX, ref newY);
            
            // 更新位置
            X = newX;
            Y = newY;
            
            // 伤害检测
            CheckDamage(world);
            
            // 边界限制
            if (Y > (120 + 5) * TILE_SIZE)
            {
                Health = MaxHealth / 2;
                // 使用世界的固定重生点
                X = world.SpawnX * TILE_SIZE + TILE_SIZE / 2;
                Y = world.SpawnY * TILE_SIZE - Height - 2;
                Vy = 0;
            }
            
            // 无敌帧更新
            if (IsInvincible)
            {
                _invincibleTimer -= dt;
                if (_invincibleTimer <= 0)
                    IsInvincible = false;
            }
            
            // 回血
            if (Health < MaxHealth && !IsInvincible)
            {
                _healTimer += dt;
                if (_healTimer >= 2.5f)
                {
                    _healTimer -= 2.5f;
                    Health = Math.Min(MaxHealth, Health + 1);
                }
            }
        }
        
        private void CheckTileCollision(World world, ref float newX, ref float newY)
        {
            float halfW = Width / 2;
            
            // 先检查水平移动
            CheckHorizontalCollision(world, ref newX);
            
            // 再检查垂直移动
            CheckVerticalCollision(world, ref newY);
            
            // 最后检查是否卡在方块里，如果是则推出
            PushOutOfBlocks(world, ref newX, ref newY);
        }
        
        private void CheckHorizontalCollision(World world, ref float newX)
        {
            float halfW = Width / 2;
            float halfH = Height / 2;
            
            // 计算玩家会占据的格子范围
            int leftTile = (int)Math.Floor((newX - halfW) / TILE_SIZE);
            int rightTile = (int)Math.Floor((newX + halfW - 1) / TILE_SIZE);
            int topTile = (int)Math.Floor((Y - Height) / TILE_SIZE);
            int bottomTile = (int)Math.Floor((Y - 1) / TILE_SIZE);
            
            // 检查是否会碰到左边的方块
            for (int y = topTile; y <= bottomTile; y++)
            {
                if (y < 0 || y >= 120) continue;
                if (leftTile >= 0 && leftTile < world.WorldWidth)
                {
                    var tile = world.GetTile(leftTile, y);
                    if (IsSolidTile(tile))
                    {
                        // 碰到左边，停止水平移动
                        newX = leftTile * TILE_SIZE + TILE_SIZE + halfW;
                        Vx = 0;
                        return;
                    }
                }
            }
            
            // 检查是否会碰到右边的方块
            for (int y = topTile; y <= bottomTile; y++)
            {
                if (y < 0 || y >= 120) continue;
                if (rightTile >= 0 && rightTile < world.WorldWidth)
                {
                    var tile = world.GetTile(rightTile, y);
                    if (IsSolidTile(tile))
                    {
                        // 碰到右边，停止水平移动
                        newX = rightTile * TILE_SIZE - halfW;
                        Vx = 0;
                        return;
                    }
                }
            }
        }
        
        private void CheckVerticalCollision(World world, ref float newY)
        {
            float halfW = Width / 2;
            
            // 计算玩家会占据的格子范围
            int leftTile = (int)Math.Floor((X - halfW) / TILE_SIZE);
            int rightTile = (int)Math.Floor((X + halfW - 1) / TILE_SIZE);
            
            // 检查是否会碰到下面的方块（落地）
            int bottomTile = (int)Math.Floor(newY / TILE_SIZE);
            for (int x = leftTile; x <= rightTile; x++)
            {
                if (x < 0 || x >= world.WorldWidth) continue;
                if (bottomTile >= 0 && bottomTile < 120)
                {
                    var tile = world.GetTile(x, bottomTile);
                    if (IsSolidTile(tile))
                    {
                        // 碰到地面
                        newY = bottomTile * TILE_SIZE;
                        Vy = 0;
                        OnGround = true;
                        
                        float fallDistance = (_fallStartY - newY) / TILE_SIZE;
                        if (fallDistance > 3)
                        {
                            Damage((int)(fallDistance - 3));
                        }
                        _fallStartY = newY;
                        return;
                    }
                }
            }
            
            // 检查是否会碰到上面的方块（头顶）
            int topTile = (int)Math.Floor((newY - Height) / TILE_SIZE);
            for (int x = leftTile; x <= rightTile; x++)
            {
                if (x < 0 || x >= world.WorldWidth) continue;
                if (topTile >= 0 && topTile < 120)
                {
                    var tile = world.GetTile(x, topTile);
                    if (IsSolidTile(tile))
                    {
                        // 碰到头顶
                        newY = (topTile + 1) * TILE_SIZE + Height;
                        Vy = 0;
                        return;
                    }
                }
            }
            
            // 如果在空中，设置OnGround为false
            OnGround = false;
        }
        
        private void PushOutOfBlocks(World world, ref float newX, ref float newY)
        {
            float halfW = Width / 2;
            float halfH = Height / 2;
            
            // 计算玩家当前位置占据的格子
            int leftTile = (int)Math.Floor((newX - halfW) / TILE_SIZE);
            int rightTile = (int)Math.Floor((newX + halfW - 1) / TILE_SIZE);
            int topTile = (int)Math.Floor((newY - Height) / TILE_SIZE);
            int bottomTile = (int)Math.Floor((newY - 1) / TILE_SIZE);
            
            // 检查玩家是否在任何方块内部
            for (int ty = topTile; ty <= bottomTile; ty++)
            {
                for (int tx = leftTile; tx <= rightTile; tx++)
                {
                    if (tx < 0 || tx >= world.WorldWidth || ty < 0 || ty >= 120) continue;
                    
                    var tile = world.GetTile(tx, ty);
                    if (!IsSolidTile(tile)) continue;
                    
                    // 玩家在方块内部，需要推出
                    float tileCenterX = tx * TILE_SIZE + TILE_SIZE / 2;
                    float tileCenterY = ty * TILE_SIZE + TILE_SIZE / 2;
                    
                    float dx = newX - tileCenterX;
                    float dy = (newY - halfH) - tileCenterY;
                    
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        // 水平推出
                        if (dx > 0)
                            newX = (tx + 1) * TILE_SIZE + halfW;
                        else
                            newX = tx * TILE_SIZE - halfW;
                    }
                    else
                    {
                        // 垂直推出（优先向上推）
                        newY = ty * TILE_SIZE - Height;
                        Vy = 0;
                        OnGround = false;
                    }
                }
            }
        }
        
        private bool IsSolidTile(TileType tile)
        {
            return tile != TileType.Air && tile != TileType.Lava && tile != TileType.GrassDecor;
        }
        
        private void CheckDamage(World world)
        {
            float halfW = Width / 2;
            int headY = (int)((Y - Height) / TILE_SIZE);
            int footY = (int)(Y / TILE_SIZE);
            
            for (int y = headY; y <= footY; y++)
            {
                for (float x = X - halfW; x < X + halfW; x += 5)
                {
                    int tx = (int)(x / TILE_SIZE);
                    var tile = world.GetTile(tx, y);
                    
                    if (tile == TileType.Lava)
                    {
                        Damage(4);
                        return;
                    }
                    if (tile == TileType.Cactus)
                    {
                        Damage(2);
                        return;
                    }
                }
            }
        }
        
        public void Damage(int amount)
        {
            if (IsInvincible) return;
            Health -= amount;
            IsInvincible = true;
            _invincibleTimer = 0.5f;
            
            if (Health <= 0)
            {
                Health = MaxHealth / 2;
                X = 3000;
            }
        }
        
        public void SellBag(float multiplier = 1.0f)
        {
            foreach (var tile in Bag)
            {
                if (TileValues.ContainsKey(tile))
                    Money += (int)(TileValues[tile] * multiplier);
            }
            Bag.Clear();
        }
        
        public void StartDigging(int x, int y)
        {
            IsDigging = true;
            DigX = x;
            DigY = y;
            DigProgress = 0;
        }
        
        public void StopDigging()
        {
            IsDigging = false;
        }
        
        public object Serialize()
		{
			return new
			{
				X, Y, Vx, Vy,
				Health, Money,
				Bag = Bag.ToArray(),
				BagMax, 
				PickaxeLevel
			};
		}
		
		public void LoadFromData(object data, World world)
		{
			try
			{
				var obj = (dynamic)data;
				X = obj.X;
				Y = obj.Y;
				// 不加载旧的速度，重置为0
				Vx = 0;
				Vy = 0;
				Health = obj.Health;
				Money = obj.Money;
				
				byte[] bagArray = obj.Bag;
				Bag = bagArray != null ? bagArray.ToList() : new List<byte>();
				BagMax = obj.BagMax;
				PickaxeLevel = obj.PickaxeLevel;
				
				DebugConsole.Log($"玩家数据加载 - 位置: ({X}, {Y}), 金钱: {Money}, 血量: {Health}");
				DebugConsole.Log($"速度已重置为 0");
				
				FixPlayerPosition(world);
			}
			catch (Exception ex)
			{
				DebugConsole.Log($"加载玩家数据失败: {ex.Message}");
			}
		}
		
		private void FixPlayerPosition(World world)
		{
			float halfW = Width / 2;
			int leftTile = (int)((X - halfW) / TILE_SIZE);
			int rightTile = (int)((X + halfW) / TILE_SIZE);
			int topTile = (int)((Y - Height) / TILE_SIZE);
			int bottomTile = (int)(Y / TILE_SIZE);
			
			bool stuck = false;
			
			for (int y = topTile; y <= bottomTile; y++)
			{
				if (y < 0 || y >= 120) continue;
				
				for (int x = leftTile; x <= rightTile; x++)
				{
					var tileType = world.GetTile(x, y);
					if (tileType != TileType.Air && tileType != TileType.Lava && tileType != TileType.GrassDecor)
					{
						stuck = true;
						break;
					}
				}
				if (stuck) break;
			}
			
			if (stuck)
			{
				int tileX = (int)(X / TILE_SIZE);
				int surfaceY = world.FindSurfaceY(tileX);
				Y = surfaceY * TILE_SIZE - Height - 2;
				Vy = 0;
				DebugConsole.Log($"玩家位置修正 - 原位置: ({X}, {Y}), 修正后地表Y: {surfaceY}");
			}
		}
    }
}