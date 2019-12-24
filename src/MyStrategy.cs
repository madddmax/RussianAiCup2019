using System;
using System.Collections.Generic;
using System.Linq;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        private const int MaxSimulatedTiles = 180;
        private const int MaxDirectionChanges = 2;

        private static Unit _me;
        private static Unit _targetEnemy;
        private static Unit? _friend;
        private static Unit? _otherEnemy;

        private static LootBox[] _weaponLootBoxes;
        private static LootBox[] _healthLootBoxes;
        private static Bullet[] _bullets;
        private static Mine[] _mines;
        private static Tile[][] _tiles;
        private static Properties _properties;
        private static int _currentTick;

        private static Dictionary<int, LootBox?> _nearestWeaponDic = new Dictionary<int, LootBox?>();
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

            _friend = null;
            _otherEnemy = null;
            Unit? targetEnemy = null;
            foreach (var u in game.Units)
            {
                if (u.Health == 0)
                {
                    continue;
                }

                if (u.Id == _me.Id)
                {
                    continue;
                }

                if (u.PlayerId == _me.PlayerId)
                {
                    _friend = u;
                    continue;
                }

                if (targetEnemy == null ||
                    u.Health < targetEnemy.Value.Health - 10 ||
                    (u.Health == targetEnemy.Value.Health &&
                    DistanceSqr(_me.Position, u.Position) <
                    DistanceSqr(_me.Position, targetEnemy.Value.Position)))
                {
                    targetEnemy = u;
                    continue;
                }

                _otherEnemy = u;
            }

            if (targetEnemy == null)
            {
                return new UnitAction();
            }

            _targetEnemy = targetEnemy.Value;

            //////////////////////////////////

            if (_me.Weapon == null)
            {
                if (!_nearestWeaponDic.ContainsKey(_me.Id))
                {
                    _nearestWeaponDic[_me.Id] = null;
                }

                if (_friend != null && !_nearestWeaponDic.ContainsKey(_friend.Value.Id))
                {
                    _nearestWeaponDic[_friend.Value.Id] = null;
                }

                LootBox? nearestWeapon = null;
                LootBox? friendsWeapon = null;
                if (_friend != null && _friend.Value.Weapon == null)
                {
                    friendsWeapon = _nearestWeaponDic[_friend.Value.Id];
                }
                foreach (var weapon in _weaponLootBoxes)
                {
                    if (!nearestWeapon.HasValue ||
                        ((friendsWeapon == null || 
                          Math.Abs(friendsWeapon.Value.Position.X - weapon.Position.X) > 0.02 ||
                          Math.Abs(friendsWeapon.Value.Position.Y - weapon.Position.Y) > 0.02) &&
                        DistanceSqr(_me.Position, weapon.Position) <
                        DistanceSqr(_me.Position, nearestWeapon.Value.Position)))
                    {
                        nearestWeapon = weapon;
                        _nearestWeaponDic[_me.Id] = weapon;
                    }
                }

                var weaponPosition = nearestWeapon?.Position ?? _me.Position;
                var bestMoves = GetMove(_me.Position, weaponPosition, 0, 0);
                var weaponMove = bestMoves.Count != 0 ? bestMoves[0] : Moves.Right;

                var weaponAction = new UnitAction();
                weaponAction.Jump = weaponMove.HasFlag(Moves.Up);
                weaponAction.JumpDown = weaponMove.HasFlag(Moves.Down);
                if (weaponMove.HasFlag(Moves.Right))
                {
                    weaponAction.Velocity = _properties.UnitMaxHorizontalSpeed;
                }
                if (weaponMove.HasFlag(Moves.Left))
                {
                    weaponAction.Velocity = -_properties.UnitMaxHorizontalSpeed;
                }

                return weaponAction;
            }

            //////////////////////////////

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
            Moves move = Moves.Right;

            if (_me.Health < game.Properties.UnitMaxHealth && _me.Health < _targetEnemy.Health)
            {
                LootBox? nearestHealth = null;
                foreach (var health in _healthLootBoxes)
                {
                    if (!nearestHealth.HasValue ||
                        DistanceSqr(_me.Position, health.Position) <
                        DistanceSqr(_me.Position, nearestHealth.Value.Position) &&
                        DistanceSqr(_targetEnemy.Position, health.Position) >
                        DistanceSqr(_me.Position, health.Position))
                    {
                        nearestHealth = health;
                    }
                }

                if (nearestHealth != null)
                {
                    var bestMoves = GetMove(_me.Position, nearestHealth.Value.Position, 0, 0);
                    move = bestMoves.Count != 0 ? bestMoves[0] : Moves.Right;
                }
                else
                {
                    var bestMoves = GetMove(_me.Position, _targetEnemy.Position, 0, 0);
                    move = bestMoves.Count != 0 ? bestMoves[0] : Moves.Right;
                }
            }
            else
            {
                var bestMoves = GetMove(_me.Position, _targetEnemy.Position, 0, 0);
                move = bestMoves.Count != 0 ? bestMoves[0] : Moves.Right;
            }

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

            action.Aim = new Vec2Double(_targetEnemy.Position.X - _me.Position.X, _targetEnemy.Position.Y - _me.Position.Y);
            action.Shoot = IsPossibleShoot(_me.Position, _targetEnemy.Position);
            
            if(!action.Shoot && _otherEnemy != null)
            {
                bool isPossibleShoot = IsPossibleShoot(_me.Position, _otherEnemy.Value.Position);
                if (isPossibleShoot)
                {
                    action.Aim = new Vec2Double(_otherEnemy.Value.Position.X - _me.Position.X, _otherEnemy.Value.Position.Y - _me.Position.Y);
                    action.Shoot = true;
                }
            }

            //action.SwapWeapon = false;
            //action.Reload = false;
            //action.PlantMine = false;
            return action;
        }

        private static List<Moves> GetMove(Vec2Double mePos, Vec2Double targetPos, int tile, int directionChanges)
        {
            var bestMoves = new List<Moves>();
            foreach (Moves move in Enum.GetValues(typeof(Moves)))
            {
                if (!CanMove(mePos, move))
                {
                    continue;
                }

                double x = mePos.X;
                double y = mePos.Y;
                bool isTerminate = false;
                var moves = new List<Moves>();

                for (int t = 0; t < MaxSimulatedTiles; t++)
                {
                    double prevX = x;
                    double prevY = y;

                    if (move.HasFlag(Moves.Up))
                    {
                        y += _properties.UnitJumpSpeed / 60;
                    }

                    if (move.HasFlag(Moves.Down))
                    {
                        y -= _properties.UnitFallSpeed / 60;
                    }

                    if (move.HasFlag(Moves.Left))
                    {
                        x -= _properties.UnitMaxHorizontalSpeed / 60;
                    }

                    if (move.HasFlag(Moves.Right))
                    {
                        x += _properties.UnitMaxHorizontalSpeed / 60;
                    }

                    var newMePos = new Vec2Double(x, y);
                    if (!CanMove(newMePos, move))
                    {
                        if (directionChanges >= MaxDirectionChanges)
                        {
                            isTerminate = true;
                            break;
                        }

                        var newPos = new Vec2Double(prevX, prevY);
                        int newDirectionChanges = directionChanges + 1;
                        var newMoves = GetMove(newPos, targetPos, t, newDirectionChanges);
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

                    if (Math.Abs(targetPos.X - newMePos.X) < 0.2 &&
                        Math.Abs(targetPos.Y - newMePos.Y) < 0.2)
                    {
                        break;
                    }
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

        private static bool CanMove(Vec2Double mePos, Moves move)
        {
            var upY = mePos.Y + _properties.UnitSize.Y;
            var leftX = mePos.X - _properties.UnitSize.X / 2;
            var rightX = mePos.X + _properties.UnitSize.X / 2;

            if (move.HasFlag(Moves.Up) &&
                (GetTile(leftX, mePos.Y + 2) == Tile.Wall ||
                 GetTile(rightX, mePos.Y + 2) == Tile.Wall))
            {
                return false;
            }

            if (move.HasFlag(Moves.Down) &&
                (GetTile(leftX, mePos.Y - 0.2) == Tile.Wall ||
                 GetTile(rightX, mePos.Y - 0.2) == Tile.Wall))
            {
                return false;
            }

            if (move.HasFlag(Moves.Left) &&
                (GetTile(mePos.X - 1, upY) == Tile.Wall ||
                 GetTile(mePos.X - 1, mePos.Y) == Tile.Wall))
            {
                return false;
            }

            if (move.HasFlag(Moves.Right) &&
                (GetTile(mePos.X + 1, upY) == Tile.Wall ||
                 GetTile(mePos.X + 1, mePos.Y) == Tile.Wall))
            {
                return false;
            }

            if (move.HasFlag(Moves.Up) && 
                GetTile(mePos.X, mePos.Y) != Tile.Ladder)
            {
                for (int i = (int)mePos.Y; i >= 0 && i > mePos.Y - 5; i--)
                {
                    var tile = GetTile(mePos.X, i);
                    if (tile == Tile.JumpPad || 
                        tile == Tile.Platform || 
                        tile == Tile.Wall)
                    {
                        return true;
                    }
                }
                return false;
            }

            return true;
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
            const double step = 0.1;

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
            var tile = GetTile(x, y - 1);
            if (y - tileY < 0.02 && (tile == Tile.Wall || tile == Tile.Platform))
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

        public class BulletNode
        {
            public Vec2Double Pos { get; set; }
            public double Size { get; set; }
            public int Tick { get; set; }
        }

        [Flags]
        public enum Moves : byte
        {
            Up = 0b0000_0001,
            Down = 0b0000_0010,
            Left = 0b0000_0100,
            Right = 0b0000_1000,
            UpLeft = 0b0000_0101,
            UpRight = 0b0000_1001,
            DownLeft = 0b0000_0110,
            DownRight = 0b0000_1010
        }
    }
}
