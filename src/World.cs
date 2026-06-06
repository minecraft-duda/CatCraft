using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace CatCraft
{
    public class World
    {
        private const int WORLD_H = 120;
        private const int WORLD_EXPAND_CHUNK = 200;
        private const int TILE_SIZE = 40;
        
        private TileType[][] _tiles;
        private int[][] _health;
        private BiomeType[] _biomes;
        private int _worldWidth;
        private int _worldOffset;
        
        private List<FallingSand> _fallingSand;
        private Random _random;
        
        public int WorldWidth => _worldWidth;
        public int WorldOffset => _worldOffset;
        
        // 固定重生点
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        
        public World()
        {
            _random = new Random();
            _fallingSand = new List<FallingSand>();
        }
        
        public void GenerateNewWorld()
        {
            _worldWidth = 150;
            _worldOffset = 0;
            _tiles = new TileType[_worldWidth][];
            _health = new int[_worldWidth][];
            _biomes = new BiomeType[_worldWidth];
            
            for (int i = 0; i < _worldWidth; i++)
            {
                _tiles[i] = new TileType[WORLD_H];
                _health[i] = new int[WORLD_H];
            }
            
            GenerateTerrain();
            
            // 设置固定重生点（世界中心）
            SpawnX = _worldWidth / 2;
            SpawnY = FindSurfaceY(SpawnX);
        }
        
        private void GenerateTerrain()
        {
            int[] heightMap = GenerateHeightMap();
            GenerateBiomes();
            ApplyMountains(heightMap);
            ApplyRifts(heightMap);
            FillWorld(heightMap);
            GenerateCaves(heightMap);
            PlantGrass(heightMap);
            GenerateOres();
            GenerateVegetation();
            ApplySandGravity();
            CalculateHealth(heightMap);
        }
        
        private int[] GenerateHeightMap()
        {
            int[] heightMap = new int[_worldWidth];
            for (int idx = 0; idx < _worldWidth; idx++)
            {
                int worldX = idx + _worldOffset;
                float h = 20;
                h += (float)(Math.Sin(worldX * 0.005) * 12);
                h += (float)(Math.Sin(worldX * 0.012 + 2) * 6);
                h += (float)(Math.Sin(worldX * 0.035 + 4) * 3);
                h += (float)(Math.Sin(worldX * 0.08 + 1) * 1.5);
                heightMap[idx] = Math.Max(0, (int)h);
            }
            return heightMap;
        }
        
        private void GenerateBiomes()
        {
            for (int idx = 0; idx < _worldWidth; idx++)
            {
                int worldX = idx + _worldOffset;
                float desert = (float)(Math.Sin(worldX * 0.008 + 1) * 0.5);
                desert += (float)(Math.Sin(worldX * 0.015 + 3) * 0.3);
                desert += (float)(Math.Sin(worldX * 0.003) * 0.2);
                _biomes[idx] = desert > 0.3f ? BiomeType.Desert : BiomeType.Grassland;
            }
        }
        
        private void ApplyMountains(int[] heightMap)
        {
            for (int i = 0; i < 8; i++)
            {
                int center = 10 + _random.Next(_worldWidth - 20);
                int height = 8 + _random.Next(14);
                int radius = 12 + _random.Next(25);
                
                for (int idx = 0; idx < _worldWidth; idx++)
                {
                    float dist = Math.Abs(idx - center) / (float)radius;
                    if (dist < 1)
                    {
                        heightMap[idx] = Math.Max(0, heightMap[idx] - (int)(height * (1 - dist * dist)));
                    }
                }
            }
        }
        
        private void ApplyRifts(int[] heightMap)
        {
            for (int i = 0; i < 5; i++)
            {
                int center = 10 + _random.Next(_worldWidth - 20);
                int depth = 4 + _random.Next(8);
                int radius = 8 + _random.Next(15);
                
                for (int idx = 0; idx < _worldWidth; idx++)
                {
                    float dist = Math.Abs(idx - center) / (float)radius;
                    if (dist < 1)
                    {
                        heightMap[idx] += (int)(depth * (1 - dist * dist));
                    }
                }
            }
        }
        
        private void FillWorld(int[] heightMap)
		{
			for (int idx = 0; idx < _worldWidth; idx++)
			{
				int sy = heightMap[idx];
				// 确保 sy 在有效范围内
				sy = Math.Max(0, Math.Min(sy, WORLD_H - 1));
				
				bool isDesert = _biomes[idx] == BiomeType.Desert;
				
				for (int y = 0; y < WORLD_H; y++)
				{
					if (y >= WORLD_H - 3)
						_tiles[idx][y] = TileType.Bedrock;
					else if (y == sy)
						_tiles[idx][y] = isDesert ? TileType.Sand : TileType.Grass;
					else if (y > sy && y <= sy + 4 && y < WORLD_H)
						_tiles[idx][y] = isDesert ? TileType.Sand : TileType.Dirt;
					else if (y > sy && y < WORLD_H)
						_tiles[idx][y] = TileType.Stone;
					else
						_tiles[idx][y] = TileType.Air;
				}
			}
		}
        
        private void GenerateCaves(int[] heightMap)
		{
			for (int idx = 0; idx < _worldWidth; idx++)
			{
				int worldX = idx + _worldOffset;
				for (int y = 0; y < WORLD_H; y++)
				{
					float noise = (float)(Math.Sin(worldX * 0.08) * Math.Cos(y * 0.06) * 0.5);
					noise += (float)(Math.Sin(worldX * 0.04 + 1.3) * Math.Cos(y * 0.035 + 0.8) * 0.3);
					noise += (float)(Math.Sin(worldX * 0.015 + 2.1) * Math.Cos(y * 0.02 + 1.5) * 0.2);
					
					// 确保索引有效
					if (y > heightMap[idx] + 4 && y < WORLD_H && idx >= 0 && idx < _worldWidth)
					{
						if (noise > 0.62f && _tiles[idx][y] != TileType.Bedrock && _tiles[idx][y] != TileType.Air)
						{
							_tiles[idx][y] = TileType.Air;
						}
					}
				}
			}
		}
        
        private void PlantGrass(int[] heightMap)
		{
			for (int idx = 0; idx < _worldWidth; idx++)
			{
				if (_biomes[idx] == BiomeType.Desert) continue;
				for (int y = WORLD_H - 2; y >= 0; y--)
				{
					if (_tiles[idx][y] != TileType.Air)
					{
						if (y + 1 < WORLD_H && _tiles[idx][y + 1] == TileType.Air && y < heightMap[idx] + 3)
						{
							if (y > 0 && y < WORLD_H && _tiles[idx][y] == TileType.Dirt)
							{
								_tiles[idx][y] = TileType.Grass;
							}
						}
						break;
					}
				}
			}
		}
        
        private void GenerateOres()
        {
            GenerateOreType(TileType.Coal, 5, 80, 0.06f);
            GenerateOreType(TileType.Iron, 5, 50, 0.03f);
            GenerateOreType(TileType.Gold, 5, 35, 0.02f);
            GenerateOreType(TileType.Emerald, 5, 60, 0.012f);
            GenerateOreType(TileType.Redstone, 20, 90, 0.008f);
            GenerateOreType(TileType.Diamond, 70, 117, 0.005f);
            GenerateOreType(TileType.Lava, WORLD_H - 8, WORLD_H - 3, 0.015f);
        }
        
        private void GenerateOreType(TileType ore, int minY, int maxY, float prob)
        {
            for (int x = 0; x < _worldWidth; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float currentProb = prob;
                    if (ore == TileType.Diamond)
                    {
                        float depthBonus = (float)Math.Pow((y - minY) / (float)(maxY - minY), 3);
                        currentProb = prob * (1 + depthBonus * 4);
                    }
                    
                    if (_tiles[x][y] == TileType.Stone && _random.NextDouble() < currentProb)
                    {
                        _tiles[x][y] = ore;
                    }
                }
            }
        }
        
        private void GenerateVegetation()
        {
            GenerateCacti();
            GenerateTrees();
            GenerateGrassDecorations();
        }
        
        private void GenerateCacti()
		{
			for (int idx = 0; idx < _worldWidth; idx++)
			{
				if (_biomes[idx] != BiomeType.Desert || _random.NextDouble() > 0.03) continue;
				
				int surfaceY = FindSurfaceY(idx);
				// 检查边界
				if (surfaceY < 1 || surfaceY >= WORLD_H - 5) continue;
				
				int height = 1 + _random.Next(3);
				for (int i = 0; i < height; i++)
				{
					int yPos = surfaceY - 1 - i;
					if (yPos >= 0 && yPos < WORLD_H && _tiles[idx][yPos] == TileType.Air)
					{
						_tiles[idx][yPos] = TileType.Cactus;
					}
				}
			}
		}
        
        private void GenerateTrees()
		{
			for (int idx = 0; idx < _worldWidth; idx++)
			{
				if (_biomes[idx] != BiomeType.Grassland || _random.NextDouble() > 0.015) continue;
				
				int surfaceY = FindSurfaceY(idx);
				// 检查边界
				if (surfaceY < 3 || surfaceY >= WORLD_H - 8) continue;
				
				int height = 4 + _random.Next(3);
				for (int i = 0; i < height; i++)
				{
					int yPos = surfaceY - 1 - i;
					if (yPos >= 0 && yPos < WORLD_H && _tiles[idx][yPos] == TileType.Air)
					{
						_tiles[idx][yPos] = TileType.Wood;
					}
				}
				
				int radius = 1 + _random.Next(2);
				for (int dx = -radius; dx <= radius; dx++)
				{
					for (int dy = -radius; dy <= radius; dy++)
					{
						if (dx == 0 && dy == height - 1) continue;
						int lidx = idx + dx;
						int ly = surfaceY - height - dy;
						if (lidx >= 0 && lidx < _worldWidth && ly >= 0 && ly < WORLD_H)
						{
							if (_tiles[lidx][ly] == TileType.Air && _random.NextDouble() > 0.3)
							{
								_tiles[lidx][ly] = TileType.Leaves;
							}
						}
					}
				}
			}
		}
        
        private void GenerateGrassDecorations()
		{
			for (int idx = 0; idx < _worldWidth; idx++)
			{
				if (idx < 0 || idx >= _worldWidth) continue;
				if (_biomes[idx] != BiomeType.Grassland) continue;
				if (_random.NextDouble() > 0.08) continue;
				
				int surfaceY = FindSurfaceY(idx);
				
				// 严格边界检查
				if (surfaceY <= 0 || surfaceY >= WORLD_H - 1) continue;
				if (surfaceY - 1 < 0 || surfaceY - 1 >= WORLD_H) continue;
				if (idx < 0 || idx >= _worldWidth) continue;
				
				if (_tiles[idx] != null && surfaceY - 1 < _tiles[idx].Length)
				{
					if (_tiles[idx][surfaceY - 1] == TileType.Air)
					{
						_tiles[idx][surfaceY - 1] = TileType.GrassDecor;
					}
				}
			}
		}
        
        private void ApplySandGravity()
        {
            for (int pass = 0; pass < 20; pass++)
            {
                for (int idx = 0; idx < _worldWidth; idx++)
                {
                    if (_biomes[idx] != BiomeType.Desert) continue;
                    for (int y = WORLD_H - 2; y >= 0; y--)
                    {
                        if (_tiles[idx][y] == TileType.Sand)
                        {
                            int fallY = y + 1;
                            while (fallY < WORLD_H - 1 && _tiles[idx][fallY] == TileType.Air) fallY++;
                            fallY--;
                            
                            if (fallY != y)
                            {
                                _tiles[idx][fallY] = TileType.Sand;
                                _tiles[idx][y] = TileType.Air;
                                _health[idx][fallY] = _health[idx][y];
                                _health[idx][y] = 0;
                            }
                        }
                    }
                }
            }
        }
        
        private void CalculateHealth(int[] heightMap)
        {
            for (int idx = 0; idx < _worldWidth; idx++)
            {
                for (int y = 0; y < WORLD_H; y++)
                {
                    if (_tiles[idx][y] != TileType.Air && _tiles[idx][y] != TileType.Bedrock)
                    {
                        int baseHealth = GetTileMaxHealth(_tiles[idx][y]);
                        float depthFactor = Math.Max(0, (y - heightMap[idx]) / (float)(WORLD_H - heightMap[idx]));
                        _health[idx][y] = (int)(baseHealth * (1 + depthFactor * 3));
                    }
                    else if (_tiles[idx][y] == TileType.Bedrock)
                    {
                        _health[idx][y] = 999999;
                    }
                }
            }
        }
        
        private int GetTileMaxHealth(TileType tile)
        {
            return tile switch
            {
                TileType.Air => 0,
                TileType.Grass => 10,
                TileType.Dirt => 10,
                TileType.Stone => 20,
                TileType.Coal => 50,
                TileType.Iron => 80,
                TileType.Gold => 100,
                TileType.Emerald => 150,
                TileType.Redstone => 200,
                TileType.Diamond => 300,
                TileType.Lava => 1,
                TileType.Bedrock => 999999,
                TileType.TNT => 1,
                TileType.Sand => 8,
                TileType.Cactus => 15,
                TileType.Wood => 20,
                TileType.Leaves => 5,
                TileType.GrassDecor => 0,
                _ => 100
            };
        }
        
        public int FindSurfaceY(int tileX)
		{
			if (tileX < 0 || tileX >= _worldWidth) return 20; // 返回默认值而不是 -1
			
			for (int y = 0; y < WORLD_H; y++)
			{
				if (_tiles[tileX][y] != TileType.Air)
					return y;
			}
			return 20; // 默认地表高度
		}
        
        public TileType GetTile(int x, int y)
        {
            if (x < 0 || x >= _worldWidth || y < 0 || y >= WORLD_H)
                return TileType.Bedrock;
            return _tiles[x][y];
        }
        
        public void SetTile(int x, int y, TileType tile)
        {
            if (x >= 0 && x < _worldWidth && y >= 0 && y < WORLD_H)
            {
                _tiles[x][y] = tile;
                if (tile == TileType.Air)
                    _health[x][y] = 0;
            }
        }
        
        public int GetTileHealth(int x, int y)
        {
            if (x < 0 || x >= _worldWidth || y < 0 || y >= WORLD_H)
                return 999999;
            return _health[x][y];
        }
        
        public void ExpandRight()
        {
            int oldWidth = _worldWidth;
            int newWidth = oldWidth + WORLD_EXPAND_CHUNK;
            
            Array.Resize(ref _tiles, newWidth);
            Array.Resize(ref _health, newWidth);
            Array.Resize(ref _biomes, newWidth);
            
            for (int i = oldWidth; i < newWidth; i++)
            {
                _tiles[i] = new TileType[WORLD_H];
                _health[i] = new int[WORLD_H];
            }
            
            int prevHeight = FindSurfaceY(oldWidth - 1);
            for (int i = oldWidth; i < newWidth; i++)
            {
                int worldX = i + _worldOffset;
                int surfaceY = prevHeight + _random.Next(-2, 3);
                surfaceY = Math.Clamp(surfaceY, 5, WORLD_H - 10);
                
                bool isDesert = _biomes[oldWidth - 1] == BiomeType.Desert;
                
                for (int y = 0; y < WORLD_H; y++)
                {
                    if (y >= WORLD_H - 3)
                        _tiles[i][y] = TileType.Bedrock;
                    else if (y == surfaceY)
                        _tiles[i][y] = isDesert ? TileType.Sand : TileType.Grass;
                    else if (y > surfaceY && y <= surfaceY + 4)
                        _tiles[i][y] = isDesert ? TileType.Sand : TileType.Dirt;
                    else if (y > surfaceY)
                        _tiles[i][y] = TileType.Stone;
                    else
                        _tiles[i][y] = TileType.Air;
                }
                prevHeight = surfaceY;
                _biomes[i] = _biomes[oldWidth - 1];
            }
            
            GenerateOresInRange(oldWidth, newWidth);
            _worldWidth = newWidth;
        }
        
        public void ExpandLeft()
        {
            int expandSize = WORLD_EXPAND_CHUNK;
            int newWidth = _worldWidth + expandSize;
            
            var newTiles = new TileType[newWidth][];
            var newHealth = new int[newWidth][];
            var newBiomes = new BiomeType[newWidth];
            
            for (int i = 0; i < newWidth; i++)
            {
                newTiles[i] = new TileType[WORLD_H];
                newHealth[i] = new int[WORLD_H];
            }
            
            for (int i = 0; i < _worldWidth; i++)
            {
                newTiles[i + expandSize] = _tiles[i];
                newHealth[i + expandSize] = _health[i];
                newBiomes[i + expandSize] = _biomes[i];
            }
            
            int prevHeight = FindSurfaceY(0);
            for (int i = expandSize - 1; i >= 0; i--)
            {
                int worldX = i + _worldOffset - expandSize;
                int surfaceY = prevHeight + _random.Next(-2, 3);
                surfaceY = Math.Clamp(surfaceY, 5, WORLD_H - 10);
                
                bool isDesert = _biomes[0] == BiomeType.Desert;
                
                for (int y = 0; y < WORLD_H; y++)
                {
                    if (y >= WORLD_H - 3)
                        newTiles[i][y] = TileType.Bedrock;
                    else if (y == surfaceY)
                        newTiles[i][y] = isDesert ? TileType.Sand : TileType.Grass;
                    else if (y > surfaceY && y <= surfaceY + 4)
                        newTiles[i][y] = isDesert ? TileType.Sand : TileType.Dirt;
                    else if (y > surfaceY)
                        newTiles[i][y] = TileType.Stone;
                    else
                        newTiles[i][y] = TileType.Air;
                }
                prevHeight = surfaceY;
                newBiomes[i] = _biomes[0];
            }
            
            _tiles = newTiles;
            _health = newHealth;
            _biomes = newBiomes;
            _worldWidth = newWidth;
            _worldOffset += expandSize;
            
            GenerateOresInRange(0, expandSize);
        }
        
        private void GenerateOresInRange(int startX, int endX)
        {
            for (int x = startX; x < endX; x++)
            {
                for (int y = 0; y < WORLD_H; y++)
                {
                    if (_tiles[x][y] == TileType.Stone)
                    {
                        float rand = (float)_random.NextDouble();
                        if (rand < 0.06f && y >= 5 && y <= 80)
                            _tiles[x][y] = TileType.Coal;
                        else if (rand < 0.03f && y >= 5 && y <= 50)
                            _tiles[x][y] = TileType.Iron;
                        else if (rand < 0.02f && y >= 5 && y <= 35)
                            _tiles[x][y] = TileType.Gold;
                        else if (rand < 0.012f && y >= 5 && y <= 60)
                            _tiles[x][y] = TileType.Emerald;
                        else if (rand < 0.008f && y >= 20 && y <= 90)
                            _tiles[x][y] = TileType.Redstone;
                        else if (rand < 0.005f && y >= 70 && y <= 117)
                            _tiles[x][y] = TileType.Diamond;
                    }
                }
            }
        }
        
        public void Update(float dt, Player player)
        {
            UpdateFallingSand(dt, player);
        }
        
        private void UpdateFallingSand(float dt, Player player)
        {
            for (int x = 0; x < _worldWidth; x++)
            {
                for (int y = WORLD_H - 2; y >= 0; y--)
                {
                    if (_tiles[x][y] == TileType.Sand)
                    {
                        if (y + 1 < WORLD_H && _tiles[x][y + 1] == TileType.Air)
                        {
                            float sandX = (x + _worldOffset) * TILE_SIZE + TILE_SIZE / 2;
                            float sandY = (y + 1) * TILE_SIZE;
                            
                            if (!(player.Y < sandY + TILE_SIZE && player.Y + player.Height > sandY &&
                                  player.X - player.Width / 2 < sandX + TILE_SIZE / 2 &&
                                  player.X + player.Width / 2 > sandX - TILE_SIZE / 2))
                            {
                                _fallingSand.Add(new FallingSand
                                {
                                    X = x,
                                    StartY = y,
                                    EndY = y + 1,
                                    Progress = 0,
                                    Speed = 0.3f
                                });
                                _tiles[x][y] = TileType.Air;
                                _health[x][y] = 0;
                            }
                        }
                    }
                }
            }
            
            for (int i = _fallingSand.Count - 1; i >= 0; i--)
            {
                var sand = _fallingSand[i];
                sand.Progress += sand.Speed * dt * 60;
                
                if (sand.Progress >= 1)
                {
                    if (sand.EndY < WORLD_H)
                    {
                        float sandX = (sand.X + _worldOffset) * TILE_SIZE + TILE_SIZE / 2;
                        float sandY = sand.EndY * TILE_SIZE;
                        
                        if (player.Y < sandY + TILE_SIZE && player.Y + player.Height > sandY &&
                            player.X - player.Width / 2 < sandX + TILE_SIZE / 2 &&
                            player.X + player.Width / 2 > sandX - TILE_SIZE / 2)
                        {
                            player.Damage(player.MaxHealth);
                        }
                        
                        _tiles[sand.X][sand.EndY] = TileType.Sand;
                        _health[sand.X][sand.EndY] = GetTileMaxHealth(TileType.Sand);
                    }
                    _fallingSand.RemoveAt(i);
                }
                else
                {
                    _fallingSand[i] = sand;
                }
            }
        }
        
        public void DrawFallingSand(Graphics g, float camX, float camY, int tileSize)
        {
            foreach (var sand in _fallingSand)
            {
                float currentY = sand.StartY + (sand.EndY - sand.StartY) * sand.Progress;
                int sx = (sand.X + _worldOffset) * tileSize - (int)camX;
                int sy = (int)(currentY * tileSize - camY);
                
                g.FillRectangle(Brushes.SandyBrown, sx, sy, tileSize, tileSize);
            }
        }
        
        public object Serialize()
        {
            var tilesArray = new byte[_worldWidth][];
            for (int i = 0; i < _worldWidth; i++)
            {
                tilesArray[i] = new byte[WORLD_H];
                for (int j = 0; j < WORLD_H; j++)
                {
                    tilesArray[i][j] = (byte)_tiles[i][j];
                }
            }
            
            var biomesArray = new byte[_worldWidth];
            for (int i = 0; i < _worldWidth; i++)
            {
                biomesArray[i] = (byte)_biomes[i];
            }
            
            return new
            {
                WorldWidth = _worldWidth,
                WorldOffset = _worldOffset,
                Tiles = tilesArray,
                Health = _health,
                Biomes = biomesArray
            };
        }
        
        public void LoadFromData(object data)
        {
            try
            {
                var obj = (dynamic)data;
                _worldWidth = obj.WorldWidth;
                _worldOffset = obj.WorldOffset;
                
                _tiles = new TileType[_worldWidth][];
                _health = new int[_worldWidth][];
                _biomes = new BiomeType[_worldWidth];
                
                byte[][] tilesArray = obj.Tiles;
                int[][] healthArray = obj.Health;
                byte[] biomesArray = obj.Biomes;
                
                for (int i = 0; i < _worldWidth; i++)
                {
                    _tiles[i] = new TileType[WORLD_H];
                    _health[i] = new int[WORLD_H];
                    
                    for (int j = 0; j < WORLD_H; j++)
                    {
                        _tiles[i][j] = (TileType)tilesArray[i][j];
                        _health[i][j] = healthArray[i][j];
                    }
                    _biomes[i] = (BiomeType)biomesArray[i];
                }
            }
            catch
            {
                GenerateNewWorld();
            }
        }
        
        private struct FallingSand
        {
            public int X;
            public int StartY;
            public int EndY;
            public float Progress;
            public float Speed;
        }
    }
}