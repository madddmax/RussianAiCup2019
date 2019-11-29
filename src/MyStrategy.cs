using System.Linq;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        static double DistanceSqr(Vec2Double a, Vec2Double b)
        {
            return (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        }
        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            debug.Draw(new CustomData.Log("Version 2"));

            Unit? nearestEnemy = null;
            foreach (var other in game.Units)
            {
                if (other.PlayerId != unit.PlayerId)
                {
                    if (!nearestEnemy.HasValue || 
                        DistanceSqr(unit.Position, other.Position) < DistanceSqr(unit.Position, nearestEnemy.Value.Position))
                    {
                        nearestEnemy = other;
                    }
                }
            }

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

            Vec2Double targetPos = unit.Position;
            if (!unit.Weapon.HasValue && nearestWeapon.HasValue)
            {
                targetPos = nearestWeapon.Value.Position;
            }
            else if (nearestEnemy.HasValue)
            {
                targetPos = nearestEnemy.Value.Position;
            }

            Vec2Double aim = new Vec2Double(0, 0);
            if (nearestEnemy.HasValue)
            {
                aim = new Vec2Double(nearestEnemy.Value.Position.X - unit.Position.X, nearestEnemy.Value.Position.Y - unit.Position.Y);
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
            action.Aim = aim;

            action.Shoot = nearestEnemy.HasValue && 
                           unit.Weapon.HasValue &&
                           DistanceSqr(unit.Position, nearestEnemy.Value.Position) < 10;

            action.SwapWeapon = nearestWeapon.HasValue &&
                                unit.Weapon.HasValue &&
                                DistanceSqr(unit.Position, nearestWeapon.Value.Position) < 10 &&
                                (unit.Weapon.Value.Typ == WeaponType.RocketLauncher && ((Item.Weapon)nearestWeapon.Value.Item).WeaponType != WeaponType.RocketLauncher ||
                                 unit.Weapon.Value.Typ == WeaponType.AssaultRifle && ((Item.Weapon)nearestWeapon.Value.Item).WeaponType == WeaponType.Pistol);

            action.PlantMine = false;

            return action;
        }
    }
}
