using System;

namespace CatCraft
{
    public class LightEngine
    {
        private const int LIGHT_RADIUS = 3;
        private float[][] _lightMap;
        private int _lastUpdateWorldWidth;
        
        public void Update(World world, Player player, float dayNightCycle)
        {
            int worldWidth = world.WorldWidth;
            
            if (_lightMap == null || _lastUpdateWorldWidth != worldWidth)
            {
                _lightMap = new float[worldWidth][];
                for (int i = 0; i < worldWidth; i++)
                    _lightMap[i] = new float[120];
                _lastUpdateWorldWidth = worldWidth;
            }
            
            float timeFactor = GetTimeLightFactor(dayNightCycle);
            int playerTileX = (int)(player.X / 40);
            int playerTileY = (int)(player.Y / 40);
            
            for (int x = 0; x < worldWidth; x++)
            {
                int surfaceY = world.FindSurfaceY(x);
                for (int y = 0; y < 120; y++)
                {
                    bool isAboveGround = surfaceY != -1 && y <= surfaceY;
                    if (isAboveGround)
                    {
                        _lightMap[x][y] = Math.Max(0.2f, timeFactor);
                    }
                    else
                    {
                        float dist = MathF.Sqrt(MathF.Pow(x - playerTileX, 2) + MathF.Pow(y - playerTileY, 2));
                        if (dist <= LIGHT_RADIUS)
                        {
                            _lightMap[x][y] = Math.Max(0.05f, 1.0f - dist / LIGHT_RADIUS);
                        }
                        else
                        {
                            _lightMap[x][y] = Math.Max(0.02f, 0.02f + timeFactor * 0.05f);
                        }
                    }
                }
            }
        }
        
        private float GetTimeLightFactor(float dayNightCycle)
        {
            float cycle = dayNightCycle * MathF.PI * 2 - MathF.PI;
            return Math.Max(0.1f, 0.5f + 0.5f * MathF.Cos(cycle));
        }
        
        public float GetLight(int x, int y)
        {
            if (_lightMap == null || x < 0 || x >= _lightMap.Length || y < 0 || y >= 120)
                return 1f;
            return _lightMap[x][y];
        }
    }
}