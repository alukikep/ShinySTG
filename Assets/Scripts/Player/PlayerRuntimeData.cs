using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRuntimeData : Singleton<PlayerRuntimeData>
{
    public int playerLife;
    public int playerBomb;
    public int playerScore;
    public int playerGraze;
    private float _currentPower;
    [SerializeField] private float maxPower = 4;

    public static event Action<float, float> OnPowerChanged;

    public float CurrentPower
    {
        get => _currentPower;
        set
        {
            // 【核心限制】如果新值和旧值一样，直接拦截，什么都不做
            if (_currentPower == value) return;

            float oldPower = _currentPower;

            // 限制数值在 0 到 maxPower 之间
            _currentPower = Mathf.Clamp(value, 0, maxPower);

            // 【核心触发】只有在数值真的变了，才触发事件
            OnPowerChanged?.Invoke(oldPower, _currentPower);
        }
    }

    public void AddPower(float amount)
    {
        CurrentPower += amount; // 这里会自动触发 set 访问器
    }

}
