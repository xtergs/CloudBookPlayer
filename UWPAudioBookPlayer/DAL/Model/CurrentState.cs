﻿using System;
using PropertyChanged;

namespace UWPAudioBookPlayer.DAL.Model
{
    [ImplementPropertyChanged]
    public class CurrentState
    {
        //public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public string BookName { get; set; } = "";
        //public int CurrentFile { get; set; } = 0;
        public bool SleepTimerIsSet { get; set; } = false;
        public TimeSpan SleepTimerDuration { get; set; }
    }
}
