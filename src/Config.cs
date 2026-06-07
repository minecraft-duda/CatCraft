using System;

namespace CatCraft
{
    public static class Config
    {
        // 游戏版本
        public const string VERSION = "3.1 Pre-Release 4";
        public const string GAME_NAME = "CatCraft";
        
        // 物理配置
        public const float GRAVITY = 980f;           // 重力加速度
        public const float MOVE_SPEED = 200f;         // 移动速度
        public const float JUMP_SPEED = -350f;        // 跳跃速度
        
        // 玩家配置
        public const int DEFAULT_HEALTH = 20;         // 默认生命值
        public const int DEFAULT_BAG_SIZE = 10;       // 默认背包大小
        public const int DEFAULT_PICKAXE_LEVEL = 1;    // 默认镐子等级
        public const float INVINCIBLE_TIME = 1.5f;      // 无敌时间（秒）
        public const float HEAL_INTERVAL = 2.5f;      // 回血间隔（秒）
        
        // 世界配置
        public const int WORLD_WIDTH = 200;           // 世界宽度（格子）
        public const int WORLD_HEIGHT = 120;          // 世界高度（格子）
        public const int TILE_SIZE = 40;              // 方块大小
        
        // 存档配置
        public const string SAVE_DIR = "saves";       // 存档目录
        public const string WORLDS_INDEX = "worlds_index.json"; // 世界索引文件名
        
        // 昼夜循环
        public const float DAY_NIGHT_CYCLE_DURATION = 120f; // 昼夜循环周期（秒）
        
        // 调试配置
        public const bool DEBUG_MODE_DEFAULT = false; // 默认调试模式
        
        // 伤害配置
        public const int LAVA_DAMAGE = 4;            // 岩浆伤害
        public const int CACTUS_DAMAGE = 2;           // 仙人掌伤害
    }
}
