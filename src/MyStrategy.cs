using System;
using System.Linq;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            debug.Draw(new CustomData.Log("Version 2"));

            LootBox? nearestWeapon = null;
            var hasLight = game.LootBoxes.Any(l => l.Item is Item.Weapon w && w.WeaponType != WeaponType.RocketLauncher);
            foreach (var lootBox in game.LootBoxes)
            {
                if (lootBox.Item is Item.Weapon w)
                {
                    if (hasLight && w.WeaponType == WeaponType.RocketLauncher)
                    {
                        continue;
                    }

                    if (!nearestWeapon.HasValue || 
                        DistanceSqr(unit.Position, lootBox.Position) < DistanceSqr(unit.Position, nearestWeapon.Value.Position))
                    {
                        nearestWeapon = lootBox;
                    }
                }
            }

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

            Vec2Double targetPos = unit.Position;
            if (!unit.Weapon.HasValue && nearestWeapon.HasValue)
            {
                targetPos = nearestWeapon.Value.Position;
            }
            else
            {
                targetPos = enemy.Position;
            }

            bool jump = targetPos.Y > unit.Position.Y;
            if (targetPos.X > unit.Position.X && game.Level.Tiles[(int)(unit.Position.X + 1)][(int)(unit.Position.Y)] == Tile.Wall)
            {
                jump = true;
            }
            if (targetPos.X < unit.Position.X && game.Level.Tiles[(int)(unit.Position.X - 1)][(int)(unit.Position.Y)] == Tile.Wall)
            {
                jump = true;
            }

            UnitAction action = new UnitAction();

            action.Velocity = targetPos.X > unit.Position.X
                ? game.Properties.UnitMaxHorizontalSpeed
                : -game.Properties.UnitMaxHorizontalSpeed;

            action.Jump = jump;
            action.JumpDown = !jump;
            action.Aim = new Vec2Double(enemy.Position.X - unit.Position.X, enemy.Position.Y - unit.Position.Y); ;

            action.Shoot = false;
            if (unit.Weapon.HasValue)
            {
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
            }

            action.SwapWeapon = nearestWeapon.HasValue &&
                                unit.Weapon.HasValue &&
                                DistanceSqr(unit.Position, nearestWeapon.Value.Position) < 10 &&
                                (unit.Weapon.Value.Typ == WeaponType.RocketLauncher && ((Item.Weapon)nearestWeapon.Value.Item).WeaponType != WeaponType.RocketLauncher ||
                                 unit.Weapon.Value.Typ == WeaponType.AssaultRifle && ((Item.Weapon)nearestWeapon.Value.Item).WeaponType == WeaponType.Pistol);

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
