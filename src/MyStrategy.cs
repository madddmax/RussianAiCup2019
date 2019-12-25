using System;
using System.Collections.Generic;
using System.Linq;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        private const int MaxSimulatedTicks = 180;
        private const int TicksInSec = 60;
        private const int MaxDirectionChanges = 2;

        private static Unit _me;
        private static Unit? _friend;
        private static List<Unit> _enemies;
        private static LootBox[] _weaponLootBoxes;
        private static LootBox[] _healthLootBoxes;
        private static Bullet[] _bullets;
        private static Mine[] _mines;
        private static Tile[][] _tiles;
        private static Properties _properties;
        private static int _currentTick;

        private static Vec2Double _prevAim = new Vec2Double(0, 0);
        private static List<List<BulletNode>> _bulletMap = new List<List<BulletNode>>();

        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            _me = unit;
            _weaponLootBoxes = game.LootBoxes.Where(l => l.Item is Item.Weapon).ToArray();
            _healthLootBoxes = game.LootBoxes.Where(l => l.Item is Item.HealthPack).ToArray();
            _bullets = game.Bullets;
            _mines = game.Mines;
            _tiles = game.Level.Tiles;
            _properties = game.Properties;
            _currentTick = game.CurrentTick;

            _enemies = new List<Unit>();
            foreach (var u in game.Units)
            {
                if (u.Id == _me.Id)
                {
                    continue;
                }

                if (u.PlayerId == _me.PlayerId)
                {
                    _friend = u;
                    continue;
                }

                _enemies.Add(u);
            }

            if (_enemies.Count == 0)
            {
                return new UnitAction();
            }

            var enemyBullets = _bullets.Where(b => b.UnitId != _me.Id).ToList();
            _bulletMap = new List<List<BulletNode>>();
            foreach (var bullet in enemyBullets)
            {
                var bulletNodes = new List<BulletNode>();
                for (int t = 0; t < 120; t++)
                {
                    var x = bullet.Position.X + t * bullet.Velocity.X / 60;
                    var y = bullet.Position.Y + t * bullet.Velocity.Y / 60;
                    if (GetTile(x, y) == Tile.Wall)
                    {
                        break;
                    }

                    bulletNodes.Add(new BulletNode
                    {
                        Pos = new Vec2Double(x, y),
                        Size = bullet.Size,
                        Tick = t
                    });
                }

                _bulletMap.Add(bulletNodes);
            }

            //////////////////////////////
            UnitAction action = new UnitAction();
            var bestMoves = GetMove(_me.Position, _me.JumpState, 0, 0);
            var move = bestMoves.Count != 0 && bestMoves.Count < 180 ? bestMoves[0] : Moves.UpLeft;

            action.Jump = move.HasFlag(Moves.Up);
            action.JumpDown = move.HasFlag(Moves.Down);
            if (move.HasFlag(Moves.Right))
            {
                action.Velocity = _properties.UnitMaxHorizontalSpeed;
            }
            if (move.HasFlag(Moves.Left))
            {
                action.Velocity = -_properties.UnitMaxHorizontalSpeed;
            }

            foreach (var enemy in _enemies)
            {
                var isPossibleShoot = IsPossibleShoot(_me.Position, enemy.Position);
                if (isPossibleShoot)
                {
                    action.Aim = new Vec2Double(enemy.Position.X - _me.Position.X, enemy.Position.Y - _me.Position.Y);
                    action.Shoot = true;
                    break;
                }
            }

            //action.SwapWeapon = false;
            //action.Reload = false;
            //action.PlantMine = false;
            return action;
        }

        private static List<Moves> GetMove(Vec2Double mePos, JumpState jumpState, int tick, int directionChanges)
        {
            var bestMoves = new List<Moves>();
            foreach (Moves move in Enum.GetValues(typeof(Moves)))
            {
                double x = mePos.X;
                double y = mePos.Y;
                bool isTerminate = false;
                var moves = new List<Moves>();
                var moveJumpState = jumpState;

                if (move.HasFlag(Moves.Up) &&
                    GetTile(x - _properties.UnitSize.X / 2, y) != Tile.Ladder &&
                    GetTile(x + _properties.UnitSize.X / 2, y) != Tile.Ladder &&
                    (!moveJumpState.CanJump || moveJumpState.MaxTime <= 0))
                {
                    continue;
                }

                for (int t = tick; t < MaxSimulatedTicks; t++)
                {
                    double prevX = x;
                    double prevY = y;

                    if (move.HasFlag(Moves.Up))
                    {
                        y += _properties.UnitJumpSpeed / TicksInSec;
                        moveJumpState.MaxTime -= (double)1 / TicksInSec;
                    }

                    if (move.HasFlag(Moves.Down))
                    {
                        y -= _properties.UnitFallSpeed / TicksInSec;
                    }

                    if (move.HasFlag(Moves.Left))
                    {
                        x -= _properties.UnitMaxHorizontalSpeed / TicksInSec;
                    }

                    if (move.HasFlag(Moves.Right))
                    {
                        x += _properties.UnitMaxHorizontalSpeed / TicksInSec;
                    }

                    var upY = y + _properties.UnitSize.Y;
                    var leftX = x - _properties.UnitSize.X / 2;
                    var rightX = x + _properties.UnitSize.X / 2;
                    if (GetTile(leftX, upY) == Tile.Wall ||
                        GetTile(rightX, upY) == Tile.Wall ||
                        GetTile(leftX, y) == Tile.Wall ||
                        GetTile(rightX, y) == Tile.Wall ||
                        (move.HasFlag(Moves.Up) &&
                         GetTile(leftX, y) != Tile.Ladder && 
                         GetTile(rightX, y) != Tile.Ladder &&
                         (!moveJumpState.CanJump || moveJumpState.MaxTime <= 0)))
                    {
                        if (directionChanges >= MaxDirectionChanges)
                        {
                            isTerminate = true;
                            break;
                        }

                        var newPos = new Vec2Double(prevX, prevY);
                        bool onGround = OnGround(prevX, prevY);
                        var newJumpState = new JumpState(onGround, _properties.UnitJumpSpeed, _properties.UnitJumpTime, true);
                        int newDirectionChanges = directionChanges + 1;
                        var newMoves = GetMove(newPos, newJumpState, t, newDirectionChanges);
                        if (newMoves.Count == 0)
                        {
                            isTerminate = true;
                            break;
                        }

                        moves.AddRange(newMoves);
                        break;
                    }

                    moves.Add(move);
                    if (bestMoves.Count > 0 && moves.Count > bestMoves.Count)
                    {
                        isTerminate = true;
                        break;
                    }

                    var newMePos = new Vec2Double(x, y);
                    if (_me.Weapon == null)
                    {
                        if (_weaponLootBoxes.Any(lootBox => Math.Abs(lootBox.Position.X - newMePos.X) < lootBox.Size.X / 4 &&
                                                            Math.Abs(lootBox.Position.Y - newMePos.Y) < lootBox.Size.Y / 4))
                        {
                            break;
                        }
                    }

                    if (_me.Health < _properties.UnitMaxHealth && _healthLootBoxes.Length > 0)
                    {
                        if (_healthLootBoxes.Any(lootBox => Math.Abs(lootBox.Position.X - newMePos.X) < lootBox.Size.X / 4 &&
                                                            Math.Abs(lootBox.Position.Y - newMePos.Y) < lootBox.Size.Y / 4))
                        {
                            break;
                        }
                    }
                    else if (_me.Weapon != null)
                    {
                        bool end = false;
                        foreach (var enemy in _enemies)
                        {
                            var isPossibleShoot = IsPossibleShoot(_me.Position, enemy.Position);
                            if (isPossibleShoot)
                            {
                                end = true;
                                break;
                            }
                        }

                        if (end)
                        {
                            break;
                        }
                    }

                    //foreach (var bulletNodes in bulletMap)
                    //{
                    //    if (bulletNodes.Count <= t)
                    //    {
                    //        break;
                    //    }
                    //}

                }

                if (isTerminate)
                {
                    continue;
                }

                if (bestMoves.Count == 0 || moves.Count < bestMoves.Count)
                {
                    bestMoves = moves;
                }
            }

            return bestMoves;
        }

        private static bool IsPossibleShoot(Vec2Double mePos, Vec2Double enemyPos)
        {
            if (_me.Weapon == null)
            {
                return false;
            }

            var meWeapon = _me.Weapon.Value;
            if (meWeapon.FireTimer != null && meWeapon.FireTimer >= 0.02)
            {
                return false;
            }

            var halfBulletSize = meWeapon.Parameters.Bullet.Size / 2;
            var leftBulletPos = new Vec2Double(mePos.X - halfBulletSize, mePos.Y + _properties.UnitSize.Y / 2);
            var rightBulletPos = meWeapon.Typ != WeaponType.RocketLauncher
                ? new Vec2Double(mePos.X + halfBulletSize, mePos.Y + _properties.UnitSize.Y / 2)
                : new Vec2Double(mePos.X + halfBulletSize, mePos.Y);

            var enemyUp = enemyPos.Y + _properties.UnitSize.Y;
            var enemyLeftUpAngle = new Vec2Double(enemyPos.X - _properties.UnitSize.X / 2, enemyUp);
            var enemyRightDownAngle = new Vec2Double(enemyPos.X + _properties.UnitSize.X / 2, enemyPos.Y);

            if (meWeapon.Typ != WeaponType.RocketLauncher)
            {
                return (IsVisible(leftBulletPos, enemyRightDownAngle) && IsVisible(rightBulletPos, enemyRightDownAngle)) ||
                       (IsVisible(leftBulletPos, enemyLeftUpAngle) && IsVisible(rightBulletPos, enemyLeftUpAngle));
            }

            var downLeftBulletPos = new Vec2Double(mePos.X - halfBulletSize, mePos.Y);
            var upLeftBulletPos = new Vec2Double(mePos.X - halfBulletSize, mePos.Y + _properties.UnitSize.Y);
            var downRightBulletPos = new Vec2Double(mePos.X + halfBulletSize, mePos.Y);
            var upRightBulletPos = new Vec2Double(mePos.X + halfBulletSize, mePos.Y + _properties.UnitSize.Y);
            return IsVisible(downLeftBulletPos, enemyRightDownAngle) && IsVisible(downRightBulletPos, enemyRightDownAngle) &&
                   IsVisible(upLeftBulletPos, enemyRightDownAngle) && IsVisible(upRightBulletPos, enemyRightDownAngle) &&
                   IsVisible(downLeftBulletPos, enemyLeftUpAngle) && IsVisible(downRightBulletPos, enemyLeftUpAngle) &&
                   IsVisible(upLeftBulletPos, enemyLeftUpAngle) && IsVisible(upRightBulletPos, enemyLeftUpAngle);
        }

        private static bool IsVisible(Vec2Double mePos, Vec2Double enemyPos)
        {
            const double step = 0.5;

            if (Math.Abs(mePos.X - enemyPos.X) >
                Math.Abs(mePos.Y - enemyPos.Y))
            {
                double startX = mePos.X < enemyPos.X ? mePos.X : enemyPos.X;
                double endX = mePos.X < enemyPos.X ? enemyPos.X : mePos.X;
                for (double x = startX + step; x < endX; x += step)
                {
                    var y = Y(mePos, enemyPos, x);
                    if (GetTile(x, y) == Tile.Wall)
                    {
                        return false;
                    }

                    if (_friend != null && IsHit(x, y, _friend.Value.Position))
                    {
                        return false;
                    }
                }
            }
            else
            {
                double startY = mePos.Y < enemyPos.Y ? mePos.Y : enemyPos.Y;
                double endY = mePos.Y < enemyPos.Y ? enemyPos.Y : mePos.Y;
                for (double y = startY + step; y < endY; y += step)
                {
                    var x = X(mePos, enemyPos, y);
                    if (GetTile(x, y) == Tile.Wall)
                    {
                        return false;
                    }

                    if (_friend != null && IsHit(x, y, _friend.Value.Position))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsHit(double bulletX, double bulletY, Vec2Double unitPos)
        {
            if (bulletX >= unitPos.X - _properties.UnitSize.X / 2 && bulletX <= unitPos.X + _properties.UnitSize.X / 2 &&
                bulletY >= unitPos.Y && bulletY <= unitPos.Y + _properties.UnitSize.Y)
            {
                return true;
            }

            return false;
        }

        private static Tile GetTile(double x, double y)
        {
            int tileX = (int)x;
            int tileY = (int)y;

            if (tileX < 0 ||
                tileY < 0 ||
                tileX > _tiles.Length - 1 ||
                tileY > _tiles[0].Length - 1)
            {
                return Tile.Wall;
            }

            return _tiles[tileX][tileY];
        }

        private static bool OnGround(double x, double y)
        {
            int tileY = (int)y;
            var tile = GetTile(x, y - 0.02);
            if (y - tileY < 0.02 && (tile == Tile.Wall || tile == Tile.Platform || tile == Tile.JumpPad))
            {
                return true;
            }

            return false;
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

        public double LengthVec(Vec2Double v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y);
        }

        public class BulletNode
        {
            public Vec2Double Pos { get; set; }
            public double Size { get; set; }
            public int Tick { get; set; }
        }

        [Flags]
        public enum Moves : byte
        {
            Left = 0b0000_0100,
            Right = 0b0000_1000,
            Down = 0b0000_0010,
            DownLeft = 0b0000_0110,
            DownRight = 0b0000_1010,
            Up = 0b0000_0001,
            UpLeft = 0b0000_0101,
            UpRight = 0b0000_1001
        }
    }
}
