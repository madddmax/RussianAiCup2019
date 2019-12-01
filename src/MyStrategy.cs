using System;
using System.Collections.Generic;
using System.Linq;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        private static Vec2Double prevEnemyPos = new Vec2Double(0, 0);
        private Vec2Double firstUnitPos = new Vec2Double(0, 0);

        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            debug.Draw(new CustomData.Log("Version 2"));

            if (game.CurrentTick == 0)
            {
                firstUnitPos = unit.Position;
            }

            //////////////////////////////

            Unit? nearestEnemy = null;
            foreach (var other in game.Units)
            {
                if (other.PlayerId == unit.PlayerId)
                {
                    continue;
                }

                if (!nearestEnemy.HasValue ||
                    DistanceSqr(unit.Position, other.Position) < DistanceSqr(unit.Position, nearestEnemy.Value.Position))
                {
                    nearestEnemy = other;
                }
            }

            if (nearestEnemy == null)
            {
                return EmptyAction();
            }
            var enemy = nearestEnemy.Value;
            var enemyVelocity = new Vec2Double(enemy.Position.X - prevEnemyPos.X, enemy.Position.Y - prevEnemyPos.Y);
            prevEnemyPos = enemy.Position;

            //////////////////////////////

            if (unit.Weapon == null)
            {
                LootBox? nearestWeapon = null;
                foreach (var lootBox in game.LootBoxes)
                {
                    if (lootBox.Item is Item.Weapon w)
                    {
                        if (!nearestWeapon.HasValue ||
                            DistanceSqr(unit.Position, lootBox.Position) < DistanceSqr(unit.Position, nearestWeapon.Value.Position))
                        {
                            nearestWeapon = lootBox;
                        }
                    }
                }

                var weaponPosition = nearestWeapon?.Position ?? unit.Position;
                return new UnitAction
                {
                    Velocity = GetMaxSpeed(unit.Position, weaponPosition, game.Properties.UnitMaxHorizontalSpeed),
                    Jump = NeedJump(unit.Position, weaponPosition, game.Level.Tiles),
                    JumpDown = !NeedJump(unit.Position, weaponPosition, game.Level.Tiles)
                };
            }

            //////////////////////////////
            
            var enemyBullets = game.Bullets.Where(b => b.UnitId != unit.Id).ToList();
            for (int t = 0; t < 60; t++)
            {
                List<Bullet> removedBullets = new List<Bullet>();
                foreach (var bullet in enemyBullets)
                {
                    var x = bullet.Position.X + t * bullet.Velocity.X / 60;
                    var y = bullet.Position.Y + t * bullet.Velocity.Y / 60;
                    var pos = new Vec2Float((float)x, (float)y);
                    var size = new Vec2Float((float)bullet.Size, (float)bullet.Size);
                    debug.Draw(new CustomData.Rect(pos, size, new ColorFloat(255, 255, 255, 255)));

                    if (game.Level.Tiles[(int)Math.Floor(x)][(int)Math.Floor(y)] == Tile.Wall)
                    {
                        removedBullets.Add(bullet);
                    }
                }

                foreach (var removed in removedBullets)
                {
                    enemyBullets.Remove(removed);
                }

                //DrawRect(enemy.Position, enemyVelocity, t, enemy.Size.X, enemy.Size.Y, debug);
            }

            //////////////////////////////
            
            UnitAction action = new UnitAction();

            if (unit.Health < game.Properties.UnitMaxHealth)
            {
                LootBox? nearestHealth = null;
                foreach (var lootBox in game.LootBoxes)
                {
                    if (lootBox.Item is Item.HealthPack w)
                    {
                        if (!nearestHealth.HasValue ||
                            DistanceSqr(unit.Position, lootBox.Position) < DistanceSqr(unit.Position, nearestHealth.Value.Position))
                        {
                            nearestHealth = lootBox;
                        }
                    }
                }

                var escapePosition = nearestHealth?.Position ?? firstUnitPos;

                action.Velocity = GetMaxSpeed(unit.Position, escapePosition, game.Properties.UnitMaxHorizontalSpeed);
                action.Jump = NeedJump(unit.Position, escapePosition, game.Level.Tiles);
                action.JumpDown = !NeedJump(unit.Position, escapePosition, game.Level.Tiles);
            }
            else
            {
                action.Velocity = GetMaxSpeed(unit.Position, enemy.Position, game.Properties.UnitMaxHorizontalSpeed);
                action.Jump = NeedJump(unit.Position, enemy.Position, game.Level.Tiles);
                action.JumpDown = !NeedJump(unit.Position, enemy.Position, game.Level.Tiles);
            }

            action.Aim = new Vec2Double(enemy.Position.X - unit.Position.X, enemy.Position.Y - unit.Position.Y);
            action.Shoot = true;
            if (Math.Abs(unit.Position.X - enemy.Position.X) >
                Math.Abs(unit.Position.Y - enemy.Position.Y))
            {
                double startX = unit.Position.X < enemy.Position.X ? unit.Position.X : enemy.Position.X;
                double endX = unit.Position.X < enemy.Position.X ? enemy.Position.X : unit.Position.X;
                for (double x = startX + 0.5; x < endX; x += 0.5)
                {
                    var y = Y(unit.Position, enemy.Position, x);
                    if (game.Level.Tiles[(int) Math.Floor(x)][(int) Math.Floor(y)] == Tile.Wall)
                    {
                        action.Shoot = false;
                    }
                }
            }
            else
            {
                double startY = unit.Position.Y < enemy.Position.Y ? unit.Position.Y : enemy.Position.Y;
                double endY = unit.Position.Y < enemy.Position.Y ? enemy.Position.Y : unit.Position.Y;
                for (double y = startY + 0.5; y < endY; y += 0.5)
                {
                    var x = X(unit.Position, enemy.Position, y);
                    if (game.Level.Tiles[(int)Math.Floor(x)][(int)Math.Floor(y)] == Tile.Wall)
                    {
                        action.Shoot = false;
                    }
                }
            }

            action.SwapWeapon = false;
            action.PlantMine = false;
            return action;
        }

        private static UnitAction EmptyAction()
        {
            return new UnitAction
            {
                Velocity = 0,
                Aim = new Vec2Double(0, 0),
                Jump = false,
                JumpDown = false,
                PlantMine = false,
                Shoot = false,
                SwapWeapon = false
            };
        }

        private static void DrawRect(Vec2Double position, Vec2Double velocity, int tick, double sizeX, double sizeY, Debug debug)
        {
            var x = position.X + tick * velocity.X;
            var y = position.Y + tick * velocity.Y;
            var pos = new Vec2Float((float)x, (float)y);
            var size = new Vec2Float((float)sizeX, (float)sizeY);
            debug.Draw(new CustomData.Rect(pos, size, new ColorFloat(255, 255, 255, 255)));
        }

        private static double GetMaxSpeed(Vec2Double current, Vec2Double target, double maxSpeed)
        {
            return target.X > current.X
                ? maxSpeed
                : -maxSpeed;
        }

        private static bool NeedJump(Vec2Double current, Vec2Double target, Tile[][] tiles)
        {
            if (current.X < target.X && tiles[(int)(current.X + 1)][(int)(current.Y)] == Tile.Wall)
            {
                return true;
            }
            if (current.X > target.X && tiles[(int)(current.X - 1)][(int)(current.Y)] == Tile.Wall)
            {
                return true;
            }

            if (Math.Abs(current.X - prevEnemyPos.X) < 2 && Math.Abs(current.Y - prevEnemyPos.Y) < 2)
            {
                return true;
            }

            return target.Y > current.Y;
        }

        private static double DistanceSqr(Vec2Double a, Vec2Double b)
        {
            return (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        }

        private static double X(Vec2Double p1, Vec2Double p2, double y)
        {
            return (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y) + p1.X;
        }

        private static double Y(Vec2Double p1, Vec2Double p2, double x)
        {
            return (x - p1.X) * (p2.Y - p1.Y) / (p2.X - p1.X) + p1.Y;
        }
    }
}
