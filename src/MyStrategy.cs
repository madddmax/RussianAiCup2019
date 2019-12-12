using System;
using System.Collections.Generic;
using System.Linq;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        private Vec2Double _firstUnitPos = new Vec2Double(0, 0);

        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            debug.Draw(new CustomData.Log("Version 7"));

            if (game.CurrentTick == 0)
            {
                _firstUnitPos = unit.Position;
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
                    DistanceSqr(unit.Position, other.Position) <
                    DistanceSqr(unit.Position, nearestEnemy.Value.Position))
                {
                    nearestEnemy = other;
                }
            }

            if (nearestEnemy == null)
            {
                return EmptyAction();
            }

            var enemy = nearestEnemy.Value;

            //////////////////////////////

            if (unit.Weapon == null)
            {
                LootBox? nearestWeapon = null;
                foreach (var lootBox in game.LootBoxes)
                {
                    if (lootBox.Item is Item.Weapon w)
                    {
                        if (!nearestWeapon.HasValue ||
                            DistanceSqr(unit.Position, lootBox.Position) <
                            DistanceSqr(unit.Position, nearestWeapon.Value.Position))
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
            for (int t = 0; t < 30; t++)
            {
                List<Bullet> removedBullets = new List<Bullet>();
                foreach (var bullet in enemyBullets)
                {
                    var x = bullet.Position.X + t * bullet.Velocity.X / 60;
                    var y = bullet.Position.Y + t * bullet.Velocity.Y / 60;
                    var pos = new Vec2Float((float) x, (float) y);
                    var size = new Vec2Float((float) bullet.Size, (float) bullet.Size);
                    //debug.Draw(new CustomData.Rect(pos, size, new ColorFloat(255, 255, 255, 255)));

                    if (game.Level.Tiles[(int) Math.Floor(x)][(int) Math.Floor(y)] == Tile.Wall)
                    {
                        removedBullets.Add(bullet);
                    }
                }

                foreach (var removed in removedBullets)
                {
                    enemyBullets.Remove(removed);
                }
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
                            DistanceSqr(unit.Position, lootBox.Position) <
                            DistanceSqr(unit.Position, nearestHealth.Value.Position) &&
                            DistanceSqr(enemy.Position, lootBox.Position) >
                            DistanceSqr(unit.Position, lootBox.Position))
                        {
                            nearestHealth = lootBox;
                        }
                    }
                }

                var escapePosition = nearestHealth?.Position ?? _firstUnitPos;

                if (Math.Abs(escapePosition.X - unit.Position.X) < 0.2 &&
                    Math.Abs(escapePosition.Y - unit.Position.Y) < 0.2)
                {
                    action.Velocity = 0;
                    action.Jump = false;
                    action.JumpDown = false;
                }
                else
                {
                    action.Velocity = GetMaxSpeed(unit.Position, escapePosition, game.Properties.UnitMaxHorizontalSpeed);
                    action.Jump = NeedJump(unit.Position, escapePosition, game.Level.Tiles);
                    action.JumpDown = !NeedJump(unit.Position, escapePosition, game.Level.Tiles);
                }
            }
            else
            {
                action.Velocity = GetMaxSpeed(unit.Position, enemy.Position, game.Properties.UnitMaxHorizontalSpeed);
                action.Jump = NeedJump(unit.Position, enemy.Position, game.Level.Tiles);
                action.JumpDown = !NeedJump(unit.Position, enemy.Position, game.Level.Tiles);
            }

            action.Aim = new Vec2Double(enemy.Position.X - unit.Position.X, enemy.Position.Y - unit.Position.Y);

            var unitWeaponPos = unit.Weapon.Value.Typ != WeaponType.RocketLauncher
                ? new Vec2Double(unit.Position.X, unit.Position.Y + game.Properties.UnitSize.Y / 2)
                : unit.Position;

            action.Shoot = IsPossibleShoot(unitWeaponPos, enemy.Position, game.Level.Tiles) ||
                           IsPossibleShoot(unitWeaponPos, new Vec2Double(enemy.Position.X, enemy.Position.Y + game.Properties.UnitSize.Y), game.Level.Tiles);

            action.SwapWeapon = false;
            //action.Reload = false;
            //action.PlantMine = false;
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

        private static bool IsPossibleShoot(Vec2Double myPos, Vec2Double enemyPos, Tile[][] tiles)
        {
            bool isPossible = true;
            if (Math.Abs(myPos.X - enemyPos.X) >
                Math.Abs(myPos.Y - enemyPos.Y))
            {
                double startX = myPos.X < enemyPos.X ? myPos.X : enemyPos.X;
                double endX = myPos.X < enemyPos.X ? enemyPos.X : myPos.X;
                for (double x = startX + 0.1; x < endX; x += 0.1)
                {
                    var y = Y(myPos, enemyPos, x);

                    int tileX = (int)x;
                    int tileY = (int)y;

                    if (tileX < 0 ||
                        tileY < 0 ||
                        tileX > tiles.Length - 1 ||
                        tileY > tiles[0].Length - 1)
                    {
                        continue;
                    }

                    if (tiles[tileX][tileY] == Tile.Wall)
                    {
                        isPossible = false;
                    }
                }
            }
            else
            {
                double startY = myPos.Y < enemyPos.Y ? myPos.Y : enemyPos.Y;
                double endY = myPos.Y < enemyPos.Y ? enemyPos.Y : myPos.Y;
                for (double y = startY + 0.1; y < endY; y += 0.1)
                {
                    var x = X(myPos, enemyPos, y);

                    int tileX = (int)x;
                    int tileY = (int)y;

                    if (tileX < 0 ||
                        tileY < 0 ||
                        tileX > tiles.Length - 1 ||
                        tileY > tiles[0].Length - 1)
                    {
                        continue;
                    }

                    if (tiles[tileX][tileY] == Tile.Wall)
                    {
                        isPossible = false;
                    }
                }
            }

            return isPossible;
        }

        private static double GetMaxSpeed(Vec2Double myPos, Vec2Double targetPos, double maxSpeed)
        {
            return myPos.X < targetPos.X
                ? maxSpeed
                : -maxSpeed;
        }

        private static bool NeedJump(Vec2Double myPos, Vec2Double targetPos, Tile[][] tiles)
        {
            if (myPos.X < targetPos.X && tiles[(int)(myPos.X + 1)][(int)(myPos.Y)] == Tile.Wall)
            {
                return true;
            }
            if (myPos.X > targetPos.X && tiles[(int)(myPos.X - 1)][(int)(myPos.Y)] == Tile.Wall)
            {
                return true;
            }

            return targetPos.Y > myPos.Y;
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

        private static Vec2Double GetVec(Vec2Double p1, Vec2Double p2)
        {
            return new Vec2Double(p2.X - p1.X, p2.Y - p1.Y);
        }

        public double LengthVec(Vec2Double v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y);
        }

        public Vec2Double Normalize(Vec2Double v)
        {
            var length = LengthVec(v);
            if (Math.Abs(length) < 0.01)
            {
                return new Vec2Double();
            }

            double invLen = 1.0 / length;
            return new Vec2Double(v.X * invLen, v.Y * invLen);
        }

        public bool IsHit(Vec2Double enemyPos, Vec2Double bulletPos)
        {
            if (bulletPos.X >= enemyPos.X - 0.45 && bulletPos.X <= enemyPos.X + 0.45 &&
                bulletPos.Y >= enemyPos.Y && bulletPos.Y <= enemyPos.Y + 1.8)
            {
                return true;
            }

            return false;
        }
    }
}
