using System;

namespace Lib.Utils
{
    public class Disposer: IDisposable
    {
        readonly Action _onDispose;

        public Disposer(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}
