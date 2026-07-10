using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptionManager : MonoBehaviour
{
    private PlayerConfig playerConfig;
    void OnEnable()
    {
        PlayerPower.OnPowerLevelChanged += SetPowerLevel;
    }
    void OnDisable()
    {
        PlayerPower.OnPowerLevelChanged -= SetPowerLevel;
    }

    void Start()
    {
        playerConfig = this.GetComponent<PlayerController>().playerConfig;
    }
    public List<PlayerOption> options;
    private int currentPowerLevel;
    private bool isFocus;

    public void SetPowerLevel(int level)
    {
        if (currentPowerLevel == level)
            return;

        currentPowerLevel = level;

        RefreshFormation();
    }

    public void SetFocus(bool focus)
    {
        if (isFocus == focus)
            return;

        isFocus = focus;

        RefreshFormation();
    }

    private void RefreshFormation()
    {
        SyncOptionCount();

        SyncOptionPosition();
    }

    private void SyncOptionCount()
    {
        while (options.Count < currentPowerLevel)
        {
            AddOption();
        }

        while (options.Count > currentPowerLevel)
        {
            RemoveOption();
        }
    }

    private void SyncOptionPosition()
    {
        Vector3[] positions = GetCurrentFormation();

        for (int i = 0; i < options.Count; i++)
        {
            options[i].SetTargetPosition(positions[i]);
        }
    }


    private Vector3[] GetCurrentFormation()
    {
        if (isFocus)
        {
            switch (currentPowerLevel)
            {
                case 1: return new[] { playerConfig.optionPositionP1Focus };
                case 2: return playerConfig.optionPositionP2Focus;
                case 3: return playerConfig.optionPositionP3Focus;
                case 4: return playerConfig.optionPositionP4Focus;
            }
        }
        else
        {
            switch (currentPowerLevel)
            {
                case 1: return new[] { playerConfig.optionPositionP1 };
                case 2: return playerConfig.optionPositionP2;
                case 3: return playerConfig.optionPositionP3;
                case 4: return playerConfig.optionPositionP4;
            }
        }

        return System.Array.Empty<Vector3>();
    }

    private void AddOption()
    {
        var obj = Instantiate(playerConfig.option, transform);
        var option = obj.GetComponent<PlayerOption>();
        option.GetBullet(playerConfig.optionBullet, playerConfig.optionBulletSpeed, playerConfig.optionDamage);
        options.Add(option);
    }

    private void RemoveOption()
    {
        if (options.Count == 0)
            return;

        PlayerOption option = options[^1];

        options.RemoveAt(options.Count - 1);

        Destroy(option.gameObject);
    }

    public void OptionFire()
    {
        foreach (var option in options)
        {
            option.OptionFire();
        }
    }
}
