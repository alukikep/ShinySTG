using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPower : MonoBehaviour
{
    void OnEnable()
    {
        PlayerRuntimeData.OnPowerChanged += CheckPower;
    }

    void OnDisable()
    {
        PlayerRuntimeData.OnPowerChanged -= CheckPower;
    }

    public static event Action<int> OnPowerLevelChanged;
    private int _currentPowerLevel;


    private void CheckPower(float oldPower, float newPower)
    {
        if (newPower < 1f)
        {
            CurrentPowerLevel = 0;
        }
        else if (newPower < 2f && newPower >= 1f)
        {
            CurrentPowerLevel = 1;
        }
        else if (newPower < 3f && newPower >= 2f)
        {
            CurrentPowerLevel = 2;
        }
        else if (newPower < 4f && newPower >= 3f)
        {
            CurrentPowerLevel = 3;
        }
        else
        {
            CurrentPowerLevel = 4;
        }
    }

    public int CurrentPowerLevel
    {
        get => _currentPowerLevel;
        set
        {
            if (_currentPowerLevel == value) return;
            else
            {
                OnPowerLevelChanged?.Invoke(_currentPowerLevel);
            }
        }
    }
}
