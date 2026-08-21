using OpenS4L.Database.Game;

namespace OpenS4L.Server.Game.Data
{
    public class LevelReward
    {
        public int Level { get; set; }
        public MoneyType Type { get; set; }
        public int Money { get; set; }

        public LevelReward(LevelRewardEntity entity)
        {
            Level = entity.Level;
            Type = (MoneyType)entity.MoneyType;
            Money = entity.Money;
        }
    }
}
