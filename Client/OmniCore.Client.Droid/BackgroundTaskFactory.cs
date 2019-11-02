﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using OmniCore.Model.Interfaces;

namespace OmniCore.Mobile.Droid
{
    public class BackgroundTaskFactory<T> : IBackgroundTaskFactory<T>
    {
        public IBackgroundTask<T> CreateBackgroundTask(T parameter, Action<T> action)
        {
            throw new NotImplementedException();
        }
    }
}