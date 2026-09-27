using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI
{
    /// <summary>在 MoveAction 的时长内从当前位置移动到指定世界坐标，可选起步加速与末段刹车。</summary>
    [Serializable, SRName("Move/Move To Position")]
    public class MoveToPositionMove : MoveBehaviour
    {
        public enum SpeedProfile
        {
            Constant = 0,
            Accelerate = 1,
            Brake = 2,
            AccelerateAndBrake = 3
        }

        [Tooltip("目标世界坐标。动作自然结束时会准确到达此点。")]
        public Vector2 TargetPosition = Vector2.zero;

        [Tooltip("Constant=匀速；Accelerate=加速起步；Brake=末段刹车；AccelerateAndBrake=两者同时启用。")]
        public SpeedProfile Profile = SpeedProfile.AccelerateAndBrake;

        [Min(0f), Tooltip("从静止加速到巡航速度的时间(秒)。")]
        public float AccelerationTime = 0.25f;

        [Min(0f), Tooltip("动作结束前减速到零的时间(秒)。")]
        public float BrakingTime = 0.12f;

        Vector2 _start;
        float _elapsed;
        float _duration;
        float _accelerationTime;
        float _brakingTime;
        float _totalProgress;

        public override bool SnapOnEnter => true;

        public override void OnEnter(Transform enemy)
        {
            _start = enemy != null ? (Vector2)enemy.position : Vector2.zero;
            _elapsed = 0f;
            _duration = float.PositiveInfinity;
            _accelerationTime = 0f;
            _brakingTime = 0f;
            _totalProgress = 0f;
        }

        public override void OnEnter(Transform enemy, float duration)
        {
            OnEnter(enemy);
            _duration = Mathf.Max(0f, duration);
            _accelerationTime = Profile == SpeedProfile.Accelerate || Profile == SpeedProfile.AccelerateAndBrake
                ? Mathf.Max(0f, AccelerationTime) : 0f;
            _brakingTime = Profile == SpeedProfile.Brake || Profile == SpeedProfile.AccelerateAndBrake
                ? Mathf.Max(0f, BrakingTime) : 0f;

            float transition = _accelerationTime + _brakingTime;
            if (transition > _duration && transition > 0f)
            {
                float scale = _duration / transition;
                _accelerationTime *= scale;
                _brakingTime *= scale;
            }
            _totalProgress = IntegratedProgress(_duration);
        }

        public override void OnTick(Transform enemy, float dt)
        {
            if (enemy == null) return;
            if (_duration <= 0f)
            {
                enemy.position = TargetPosition;
                return;
            }
            if (dt <= 0f) return;
            if (_elapsed >= _duration)
            {
                enemy.position = TargetPosition;
                return;
            }

            float next = Mathf.Min(_elapsed + dt, _duration);
            float progress;
            if (float.IsPositiveInfinity(_duration))
            {
                progress = Mathf.Clamp01(next);
            }
            else
            {
                progress = _totalProgress > 0f
                    ? IntegratedProgress(next) / _totalProgress
                    : 1f;
            }

            enemy.position = Vector2.LerpUnclamped(_start, TargetPosition, Mathf.Clamp01(progress));
            _elapsed = next;
            if (_elapsed >= _duration) enemy.position = TargetPosition;
        }

        float IntegratedProgress(float time)
        {
            if (Profile == SpeedProfile.Constant) return time;
            if (_accelerationTime > 0f && time < _accelerationTime)
                return _accelerationTime * SmoothStepIntegral(time / _accelerationTime);

            float distance = time - _accelerationTime * 0.5f;
            float brakeStart = _duration - _brakingTime;
            if (_brakingTime > 0f && time > brakeStart)
                distance -= _brakingTime * SmoothStepIntegral((time - brakeStart) / _brakingTime);
            return distance;
        }

        static float SmoothStepIntegral(float t) => t * t * t * (1f - 0.5f * t);
    }
}
