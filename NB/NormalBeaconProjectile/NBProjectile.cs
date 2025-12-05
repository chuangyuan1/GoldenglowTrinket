using StardewValley.Monsters;
using StardewValley;
using StardewValley.Projectiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Netcode;
using StardewValley.BellsAndWhistles;
using StardewValley.TokenizableStrings;
using StardewValley.TerrainFeatures;
using static StardewValley.Minigames.TargetGame;

namespace GoldenglowTrinket.NB.NormalBeaconProjectile
{
    public class NBProjectile : BasicProjectile
    {
        public readonly NetRef<Character> _firer = new NetRef<Character>();
        public readonly NetRef<Monster> _target = new NetRef<Monster>();
        private readonly NetFloat trackStrength = new NetFloat(0.1f); // 跟踪强度
        private readonly NetFloat maxTrackSpeed = new NetFloat(10f); // 最大跟踪速度
        private readonly NetInt _actualDamage = new NetInt(); // 实际伤害值
        private readonly NetRef<Farmer> _ownerFarmer = new NetRef<Farmer>();
        Random random = new Random();
        private float TrackParticlesTimer;

        protected override void InitNetFields()
        {
            base.InitNetFields();
            base.NetFields.AddField(_firer, "_firer")
                         .AddField(_target, "_target")
                         .AddField(trackStrength, "trackStrength")
                         .AddField(maxTrackSpeed, "maxTrackSpeed")
                         .AddField(_actualDamage, "_actualDamage")
                         .AddField(_ownerFarmer, "_ownerFarmer");
        }

        public NBProjectile(
            int actualDamage,
            int spriteIndex,
            int bouncesTillDestruct,
            int tailLength,
            float rotationVelocity,
            float xVelocity,
            float yVelocity,
            Vector2 startingPosition,
            string collisionSound,
            GameLocation location,
            Character firer,
            Monster target = null,
            float trackStrength = 0.1f,
            float maxTrackSpeed = 10f,
            onCollisionBehavior collisionBehavior = null
            )
            : base(
                damageToFarmer: 0,
                spriteIndex: spriteIndex,
                bouncesTillDestruct: bouncesTillDestruct,
                tailLength: tailLength,
                rotationVelocity: rotationVelocity,
                xVelocity: xVelocity,
                yVelocity: yVelocity,
                startingPosition: startingPosition,
                collisionSound: collisionSound,
                bounceSound: null,
                firingSound: null,
                explode: true,
                damagesMonsters: true, //
                location: location,
                firer: firer,
                collisionBehavior: collisionBehavior) 
        {
            this._actualDamage.Value = actualDamage;
            this._firer.Value = firer;
            this._target.Value = target;
            this.trackStrength.Value = trackStrength;
            this.maxTrackSpeed.Value = maxTrackSpeed;
            if (firer is Farmer farmer)
            {
                _ownerFarmer.Value = farmer;
            }
            InitNetFields();
        }
        private bool HasEnhancementUnit(string itemId)
        {
            if (_ownerFarmer.Value == null)
                return false;

            foreach (var item in _ownerFarmer.Value.Items)
            {
                // 物品不为空且物品ID匹配时返回true
                if (item != null && item.QualifiedItemId == $"(O){itemId}")
                {
                    return true;
                }
            }

            return false;
        }
        private (int addedPrecision, float critChance) GetEnhancedAC()
        {
            int addedPrecision = 0;
            float critChance = 0f;

            if (HasEnhancementUnit("GoldenglowBeaconEnhancementUnit2")|| HasEnhancementUnit("GoldenglowBeaconEnhancementUnit3"))
            {
                addedPrecision += 100;
                critChance += 0.1f;
            }

            return (addedPrecision, critChance); 
        }
        public override void behaviorOnCollisionWithMonster(NPC n, GameLocation location)
        {
            if (!damagesMonsters.Value)
            {
                return;
            }
            if (n is Monster monster)
            {
                if (collisionBehavior != null)
                {
                    // 传递子弹当前位置作为碰撞坐标
                    collisionBehavior?.Invoke(location, getBoundingBox().Center.X, getBoundingBox().Center.Y, GetPlayerWhoFiredMe(location));
                }
                var enhancements = GetEnhancedAC();
                int addedPrecision = enhancements.addedPrecision;
                float critChance = 0;
                critChance += enhancements.critChance;
                // 直接造成伤害，不传递击退
                location.damageMonster(
                monster.GetBoundingBox(),
                _actualDamage.Value,
                _actualDamage.Value + 1,
                isBomb: false,
                knockBackModifier: 1f,//击退
                addedPrecision: addedPrecision,
                critChance: critChance,//暴击率
                critMultiplier: 1.5f,//暴击倍率
                triggerMonsterInvincibleTimer: false,
                _firer.Value as Farmer,
                isProjectile: true
                );
                piercesLeft.Value = 0;
            }

        }
        public override void behaviorOnCollisionWithTerrainFeature(TerrainFeature t, Vector2 tileLocation, GameLocation location)
        {
            return;
        }

        public override bool update(GameTime time, GameLocation location)
        {

            TrackParticlesTimer += time.ElapsedGameTime.Milliseconds;
            if (TrackParticlesTimer >= 30f)
            {
                TrackParticlesTimer = 0;
                TrackParticles(location);
            }
            

            // 跟踪逻辑
            if (_target.Value != null && _target.Value.currentLocation == location && _target.Value.Health > 0)
            {
                // 计算朝向目标的方向
                Vector2 direction = _target.Value.Position - this.position.Value;
                if (direction != Vector2.Zero)
                {
                    direction.Normalize();
                    
                    // 应用跟踪力
                    xVelocity.Value = MathHelper.Lerp(xVelocity.Value, direction.X * maxTrackSpeed.Value, trackStrength.Value);
                    yVelocity.Value = MathHelper.Lerp(yVelocity.Value, direction.Y * maxTrackSpeed.Value, trackStrength.Value);

                    // 限制最大速度
                    float currentSpeed = (float)Math.Sqrt(xVelocity.Value * xVelocity.Value + yVelocity.Value * yVelocity.Value);
                    if (currentSpeed > maxTrackSpeed.Value)
                    {
                        xVelocity.Value = xVelocity.Value / currentSpeed * maxTrackSpeed.Value;
                        yVelocity.Value = yVelocity.Value / currentSpeed * maxTrackSpeed.Value;
                    }

                    // 更新旋转角度，使子弹朝向目标
                    rotation = (float)Math.Atan2(yVelocity.Value, xVelocity.Value) + MathHelper.PiOver2;
                }
            }
            else if (_target.Value == null || _target.Value.Health <= 0)
            {
                // 寻找新目标
                FindNewTarget(location);
                // 确保旋转角度与当前运动方向一致
                rotation = (float)Math.Atan2(yVelocity.Value, xVelocity.Value) + MathHelper.PiOver2;

            }
            //
            return base.update(time, location);
        }

        private void FindNewTarget(GameLocation location)
        {
           
            Monster newTarget = Utility.findClosestMonsterWithinRange(
                location,
                this.position.Value,
                200, // 搜索范围
                ignoreUntargetables: true
            );

            if (newTarget != null)
            {
                _target.Value = newTarget;
            }
            else
            {
              
                piercesLeft.Value--;
            }
        }
        private void TrackParticles(GameLocation location)
        {
            Vector2 Dianhu = new Vector2(0, 0);
            
            Vector2 _bulletFlyDir = new Vector2(0, -1); //修改左右飞行偏移
            Vector2 smallOffset = _bulletFlyDir * -20f; // 

            Vector2 _bulletFlyDir1 = new Vector2(-1, 0); // 修改上下飞行偏移
            Vector2 smallOffset1 = _bulletFlyDir1 * -30f; // 乘倍数
            Vector2 particlePosition = this.position.Value+ Dianhu+ smallOffset+ smallOffset1;
            Random rand = new Random();
            int randomNumber = rand.Next(0, 3);

            string textureName = randomNumber switch
            {
                0 => "Mods/Goldenglow_PinkLightBall",
                1 => "Mods/Goldenglow_BlueLightBall",
                _ => "Mods/Goldenglow_SmallGreenShine"
            };

            for (int i = 0; i < rand.Next(1, 3); i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float speed = (float)(rand.NextDouble() * 0.9 + 0.3);
                float rotationChange = (float)(0.03 + rand.NextDouble() * 0.05);
                float scale = (float)(0.2 + rand.NextDouble() * 0.3);

                var particle = new TemporaryAnimatedSprite(
                    textureName: textureName,
                    sourceRect: new Rectangle(0, 0, 32, 32),
                    animationInterval: 1000f,
                    animationLength: 1,
                    numberOfLoops: 1,
                    position: particlePosition,
                    flicker: false,
                    flipped: false,
                    layerDepth: 0.9f,
                    alphaFade: 0.015f,
                    color: Color.White,
                    scale: scale,
                    scaleChange: 0f,
                    rotation: 0f,
                    rotationChange: rotationChange
                );

                particle.motion = new Vector2(
                    (float)Math.Cos(angle) * speed,
                    (float)Math.Sin(angle) * speed
                );

                location.temporarySprites.Add(particle);
            }



        }
    }
}
