using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 匀速直线移动。Direction 决定方向,Speed 决定速率(单位/秒)。
    /// </summary>
    [Serializable, SRName("Move/Linear")]
    public class LinearMove : MoveBehaviour
    {
        public enum MoveDirection
        {
            Down,
            Up,
            Left,
            Right,
            ToPlayer,   // 进入时锁定一次方向
            Custom      // 使用 CustomAngleDeg
        }

        [Tooltip("移动方向。ToPlayer = 进入该行为时,瞬间计算一次朝玩家的方向并锁定。")]
        public MoveDirection Direction = MoveDirection.Down;

        [Tooltip("当 Direction=Custom 时使用。单位:度,0=右,90=上,180=左,270=下。")]
        public float CustomAngleDeg = 270f;

        [Tooltip("移动速度(单位/秒)。")]
        public float Speed = 3f;

        Vector2 _dir;

        public override void OnEnter(Transform enemy)
        {
            _dir = ResolveDirection(enemy);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            enemy.position += (Vector3)(_dir * Speed * dt);
        }

        Vector2 ResolveDirection(Transform enemy)
        {
            switch (Direction)
            {
                case MoveDirection.Down:  return Vector2.down;
                case MoveDirection.Up:    return Vector2.up;
                case MoveDirection.Left:  return Vector2.left;
                case MoveDirection.Right: return Vector2.right;
                case MoveDirection.Custom: return AngleToDir(CustomAngleDeg);
                case MoveDirection.ToPlayer:
                    var p = GameObject.FindGameObjectWithTag("Player");
                    if (p == null) return Vector2.down;
                    Vector2 to = (Vector2)(p.transform.position - enemy.position);
                    return to.sqrMagnitude < 0.0001f ? Vector2.down : to.normalized;
            }
            return Vector2.down;
        }

        static Vector2 AngleToDir(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }
    }
}
