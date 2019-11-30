using System;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            debug.Draw(new CustomData.Log("Version 2"));

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

            UnitAction action = new UnitAction();
            action.Velocity = GetMaxSpeed(unit.Position, enemy.Position, game.Properties.UnitMaxHorizontalSpeed);
            action.Jump = NeedJump(unit.Position, enemy.Position, game.Level.Tiles);
            action.JumpDown = !NeedJump(unit.Position, enemy.Position, game.Level.Tiles);

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

        private static double GetMaxSpeed(Vec2Double current, Vec2Double target, double maxSpeed)
        {
            return target.X > current.X
                ? maxSpeed
                : -maxSpeed;
        }

        private static bool NeedJump(Vec2Double current, Vec2Double target, Tile[][] tiles)
        {
            if (target.X > current.X && tiles[(int)(current.X + 1)][(int)(current.Y)] == Tile.Wall)
            {
                return true;
            }
            if (target.X < current.X && tiles[(int)(current.X - 1)][(int)(current.Y)] == Tile.Wall)
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
