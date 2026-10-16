using System;
using System.Threading;
using ColossalFramework.Threading;

static class LiveMainThread
{
    static int thread;
    public static void Initialize(){thread=Thread.CurrentThread.ManagedThreadId;}
    public static void Run(Action action)
    {
        if(thread==0)throw new Exception("Fixture Unity main thread was not initialized.");
        if(Thread.CurrentThread.ManagedThreadId==thread){action();return;}
        Exception failure=null;var complete=new ManualResetEvent(false);
        ThreadHelper.dispatcher.Dispatch(delegate{try{action();}catch(Exception error){failure=error;}finally{complete.Set();}});
        if(!complete.WaitOne(30000))throw new TimeoutException("Fixture main-thread tool activation timed out.");
        complete.Close();if(failure!=null)throw new Exception("Fixture main-thread tool activation failed.",failure);
    }
}
