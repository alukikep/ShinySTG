using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>
    /// 直线移动，可在动作时长内加速起步、巡航、减速停止。
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

        public enum SpeedProfile
        {
            Constant = 0,
            Accelerate = 1,
            Brake = 2,
            AccelerateAndBrake = 3
        }

        [Tooltip("巡航速度(单位/秒)。开启加减速后，同样时长内的总位移会缩短。")]
        public float Speed = 3f;

        [Tooltip("Constant=匀速；Accelerate=加速起步；Brake=末段刹车；AccelerateAndBrake=两者同时启用。")]
        public SpeedProfile Profile = SpeedProfile.Constant;

        [Min(0f), Tooltip("从静止加速到巡航速度的时间(秒)。0 表示立即达到巡航速度。")]
        public float AccelerationTime = 0.25f;

        [Min(0f), Tooltip("动作结束前减速到零的时间(秒)。越短越有急刹感；0 表示立即停止。")]
        public float BrakingTime = 0.12f;

        Vector2 _dir;
        float _elapsed;
        float _duration;
        float _accelerationTime;
        float _brakingTime;

        public override void OnEnter(Transform enemy)
        {
            _dir = ResolveDirection(enemy);
            _elapsed = 0f;
            _duration = float.PositiveInfinity;
            _accelerationTime = Profile == SpeedProfile.Accelerate || Profile == SpeedProfile.AccelerateAndBrake
                ? Mathf.Max(0f, AccelerationTime) : 0f;
            _brakingTime = 0f;
        }

        public override void OnEnter(Transform enemy, float duration)
        {
            OnEnter(enemy);
            _duration = Mathf.Max(0f, duration);
            _brakingTime = Profile == SpeedProfile.Brake || Profile == SpeedProfile.AccelerateAndBrake
                ? Mathf.Max(0f, BrakingTime) : 0f;
            float transitionTime = _accelerationTime + _brakingTime;
            if (transitionTime > _duration)
            {
                float scale = _duration / transitionTime;
                _accelerationTime *= scale;
                _brakingTime *= scale;
            }
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (enemy == null || dt <= 0f) return;
            // 默认分支保留原来的逐帧匀速语义。
            if (Profile == SpeedProfile.Constant)
            {
                enemy.position += (Vector3)(_dir * Speed * dt);
                return;
            }

            float next = Mathf.Min(_elapsed + dt, _duration);
            float distance = IntegratedSpeed(next) - IntegratedSpeed(_elapsed);
            _elapsed = next;
            enemy.position += (Vector3)(_dir * Speed * distance);
        }

        // 对 SmoothStep 速度积分；跨过分段边界或最后一帧时也不会漏算位移。
        float IntegratedSpeed(float time)
        {
            if (_accelerationTime > 0f && time < _accelerationTime)
                return _accelerationTime * SmoothStepIntegral(time / _accelerationTime);

            float distance = time - _accelerationTime * 0.5f;
            float brakeStart = _duration - _brakingTime;
            if (_brakingTime > 0f && time > brakeStart)
                distance -= _brakingTime * SmoothStepIntegral((time - brakeStart) / _brakingTime);
            return distance;
        }

        static float SmoothStepIntegral(float t) => t * t * t * (1f - 0.5f * t);

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
