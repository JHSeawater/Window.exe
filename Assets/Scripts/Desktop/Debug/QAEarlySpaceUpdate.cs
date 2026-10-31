#if UNITY_EDITOR
using System;
using UnityEngine;

namespace WindowExe.Desktop.Tests
{
    // EventSystem can query the router before its own Update; invalidate first.
    [DefaultExecutionOrder(-2000)]
    public sealed class QAEarlySpaceUpdate : MonoBehaviour
    {
        public Action Once;
        private void Update()
        {
            var callback=Once;
            Once=null;
            callback?.Invoke();
        }
    }
}
#endif
