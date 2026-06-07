using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CatCraft
{
    public class ItemStack
    {
        public byte TileId { get; set; }
        public int Count { get; set; }
        
        public ItemStack(byte tileId, int count = 1)
        {
            TileId = tileId;
            Count = count;
        }
        
        public bool IsSameType(byte otherTileId)
        {
            return TileId == otherTileId;
        }
        
        public bool CanAdd(int amount = 1)
        {
            return Count + amount <= 99;
        }
        
        public int Add(int amount = 1)
        {
            int added = Math.Min(amount, 99 - Count);
            Count += added;
            return added;
        }
        
        public int Remove(int amount = 1)
        {
            int removed = Math.Min(amount, Count);
            Count -= removed;
            return removed;
        }
    }
    
    public class Player
    {
        private const float GRAVITY = 800f;
        private const float MOVE_SPEED = 250f;
        private const float JUMP_SPEED = -420f;
        private const int TILE_SIZE = 40;
        
        // 背包常量
        public const int BAG_ROWS = 4;
        public const int BAG_COLS = 8;
        public const int HOTBAR_SIZE = 8;
        public const int MAX_STACK = 99;
        
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
        
        // 背包和快捷栏
        public List<ItemStack> Bag { get; set; }
        public List<ItemStack> Hotbar { get; set; }
        public int SelectedHotbarIndex { get; set; } = 0;
        
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
            Bag = new List<ItemStack>();
            Hotbar = new List<ItemStack>();
            // 初始化背包空格子
            for (int i = 0; i < BAG_ROWS * BAG_COLS; i++)
                Bag.Add(null);
            // 初始化快捷栏空格子
            for (int i = 0; i < HOTBAR_SIZE; i++)
                Hotbar.Add(null);
            
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
            // 出售背包中的物品
            foreach (var stack in Bag)
            {
                if (stack != null && TileValues.ContainsKey(stack.TileId))
                    Money += (int)(TileValues[stack.TileId] * stack.Count * multiplier);
            }
            // 出售快捷栏中的物品
            foreach (var stack in Hotbar)
            {
                if (stack != null && TileValues.ContainsKey(stack.TileId))
                    Money += (int)(TileValues[stack.TileId] * stack.Count * multiplier);
            }
            ClearBag();
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
				Bag = Bag.Select(s => s != null ? new { TileId = s.TileId, Count = s.Count } : null).ToArray(),
				Hotbar = Hotbar.Select(s => s != null ? new { TileId = s.TileId, Count = s.Count } : null).ToArray(),
				SelectedHotbarIndex,
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
				Vx = 0;
				Vy = 0;
				Health = obj.Health;
				Money = obj.Money;
				PickaxeLevel = obj.PickaxeLevel;
				
				// 加载背包
				try
				{
					var bagData = obj.Bag;
					if (bagData != null)
					{
						for (int i = 0; i < Bag.Count && i < bagData.Length; i++)
						{
							var item = bagData[i];
							if (item != null)
							{
								Bag[i] = new ItemStack((byte)item.TileId, (int)item.Count);
							}
							else
							{
								Bag[i] = null;
							}
						}
					}
				}
				catch
				{
					// 旧格式兼容
				}
				
				// 加载快捷栏
				try
				{
					var hotbarData = obj.Hotbar;
					if (hotbarData != null)
					{
						for (int i = 0; i < Hotbar.Count && i < hotbarData.Length; i++)
						{
							var item = hotbarData[i];
							if (item != null)
							{
								Hotbar[i] = new ItemStack((byte)item.TileId, (int)item.Count);
							}
							else
							{
								Hotbar[i] = null;
							}
						}
					}
				}
				catch
				{
					// 旧格式兼容
				}
				
				// 加载选中的快捷栏索引
				try
				{
					SelectedHotbarIndex = obj.SelectedHotbarIndex;
				}
				catch
				{
					SelectedHotbarIndex = 0;
				}
				
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
		
		// 背包操作方法
		public bool AddToBag(byte tileId)
		{
			// 首先尝试在背包中堆叠
			foreach (var stack in Bag)
			{
				if (stack != null && stack.IsSameType(tileId) && stack.CanAdd())
				{
					stack.Add();
					return true;
				}
			}
			
			// 然后尝试在快捷栏中堆叠
			foreach (var stack in Hotbar)
			{
				if (stack != null && stack.IsSameType(tileId) && stack.CanAdd())
				{
					stack.Add();
					return true;
				}
			}
			
			// 找背包中的空格子
			for (int i = 0; i < Bag.Count; i++)
			{
				if (Bag[i] == null)
				{
					Bag[i] = new ItemStack(tileId, 1);
					return true;
				}
			}
			
			return false; // 背包满了
		}
		
		public bool RemoveFromBag(int slotIndex)
		{
			if (slotIndex >= 0 && slotIndex < Bag.Count && Bag[slotIndex] != null)
			{
				if (Bag[slotIndex].Count <= 1)
					Bag[slotIndex] = null;
				else
					Bag[slotIndex].Remove();
				return true;
			}
			return false;
		}
		
		public bool RemoveFromHotbar(int slotIndex)
		{
			if (slotIndex >= 0 && slotIndex < Hotbar.Count && Hotbar[slotIndex] != null)
			{
				if (Hotbar[slotIndex].Count <= 1)
					Hotbar[slotIndex] = null;
				else
					Hotbar[slotIndex].Remove();
				return true;
			}
			return false;
		}
		
		public bool MoveToHotbar(int bagSlot, int hotbarSlot)
		{
			if (bagSlot < 0 || bagSlot >= Bag.Count || Bag[bagSlot] == null)
				return false;
			if (hotbarSlot < 0 || hotbarSlot >= Hotbar.Count)
				return false;
			
			var item = Bag[bagSlot];
			
			// 如果目标快捷栏格子有相同类型的物品，尝试堆叠
			if (Hotbar[hotbarSlot] != null && Hotbar[hotbarSlot].IsSameType(item.TileId))
			{
				int added = Hotbar[hotbarSlot].Add(item.Count);
				item.Remove(added);
				if (item.Count <= 0)
					Bag[bagSlot] = null;
				return true;
			}
			
			// 否则交换
			Bag[bagSlot] = Hotbar[hotbarSlot];
			Hotbar[hotbarSlot] = item;
			return true;
		}
		
		public int SellItem(int slotIndex, bool isHotbar = false)
		{
			var list = isHotbar ? Hotbar : Bag;
			if (slotIndex < 0 || slotIndex >= list.Count || list[slotIndex] == null)
				return 0;
			
			var stack = list[slotIndex];
			int value = TileValues.ContainsKey(stack.TileId) ? TileValues[stack.TileId] : 0;
			int totalValue = value * stack.Count;
			Money += totalValue;
			
			list[slotIndex] = null;
			return totalValue;
		}
		
		public void SelectHotbar(int index)
		{
			if (index >= 0 && index < HOTBAR_SIZE)
				SelectedHotbarIndex = index;
		}
		
		public ItemStack GetSelectedItem()
		{
			if (SelectedHotbarIndex >= 0 && SelectedHotbarIndex < Hotbar.Count)
				return Hotbar[SelectedHotbarIndex];
			return null;
		}
		
		public bool PlaceBlock(World world, int tileX, int tileY)
		{
			var selectedItem = GetSelectedItem();
			if (selectedItem == null)
				return false;
			
			// 检查目标位置是否可以放置
			var existingTile = world.GetTile(tileX, tileY);
			if (existingTile != TileType.Air)
				return false;
			
			// 放置方块
			world.SetTile(tileX, tileY, (TileType)selectedItem.TileId);
			
			// 消耗物品
			RemoveFromHotbar(SelectedHotbarIndex);
			return true;
		}
		
		public void ClearBag()
		{
			for (int i = 0; i < Bag.Count; i++)
				Bag[i] = null;
			for (int i = 0; i < Hotbar.Count; i++)
				Hotbar[i] = null;
		}
	}
}