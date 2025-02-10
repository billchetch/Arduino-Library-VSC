using System;

namespace Chetch.Arduino.Devices.Buzzers;

public class Buzzer : SwitchDevice
{

    #region Properties
    public bool Silenced {get; internal set; } = false;
    #endregion

    #region Fields
    System.Timers.Timer silenceTimer = new System.Timers.Timer();
    bool restoreAsOn = true;
    #endregion

    #region Constructors
    public Buzzer(byte id, string name) : base(id, name)
    {
        silenceTimer.AutoReset = false;
        silenceTimer.Elapsed += (sender, eargs) => {
            if(Silenced)
            {
                Silenced = false;
                if(restoreAsOn)
                {
                    TurnOn();
                }
            }
        };
    }
    #endregion

    #region Methods
    //If the buzzer is on then we flag as silenced and turn it off for a while
    public void Silence(int durationInSecs)
    {
        if(durationInSecs <= 0)
        {
            throw new ArgumentException("Duration for buzzer must be positive");
        }
        if(Silenced || IsOff)return; // fail silently if the buzzer isn't even on or has very recently been silenced

        TurnOff();
        Silenced = true;
        restoreAsOn = true;
        silenceTimer.Interval = durationInSecs * 1000;
        silenceTimer.Start();
    }

    public void Unsilence()
    {
        if(!Silenced)return; 

        Silenced = false;
        silenceTimer.Stop();
        if(restoreAsOn)
        {
            TurnOn();
        }
    }

    //If something external tries to turn the buzzer on it can't cos it's been silenced    
    public override void TurnOn()
    {
        if(Silenced)
        {
            restoreAsOn = true;
        }
        else
        {
            base.TurnOn();
        }
    }

    public override void TurnOff()
    {
        if(Silenced)
        {
            restoreAsOn = false;   
        }
        else
        {
            base.TurnOff();
        }
    }
    #endregion
}
