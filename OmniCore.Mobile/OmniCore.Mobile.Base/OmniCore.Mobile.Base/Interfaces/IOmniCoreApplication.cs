﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniCore.Mobile.Base.Interfaces
{
    public interface IOmniCoreApplication
    {
        Task RunOnMainThread(Func<Task> funcTask);
        Task<T> RunOnMainThread<T>(Func<Task<T>> funcTask);
        Task RunOnMainThread(Action action);
        Task<T> RunOnMainThread<T>(Func<T> func);
        Task<SynchronizationContext> GetMainSyncContext();
        void Exit();
    }
}
