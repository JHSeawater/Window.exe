using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class WifiConnectionTests
    {
        private GameObject wifiGo;
        private WifiConnection wifi;

        [SetUp]
        public void SetUp()
        {
            WifiConnection.ResetGlobalState();
            wifiGo = new GameObject("WifiConnection");
            wifi = wifiGo.AddComponent<WifiConnection>();
            WifiConnection.ResetInstance(wifi);
        }

        [TearDown]
        public void TearDown()
        {
            if (wifiGo != null)
            {
                Object.DestroyImmediate(wifiGo);
            }
            WifiConnection.ResetInstance(null);
            WifiConnection.ResetGlobalState();
        }

        [Test]
        public void WifiConnection_InitializesToFalseOnAwake()
        {
            Assert.IsFalse(wifi.IsConnected);
            Assert.IsFalse(WifiConnection.GlobalIsConnected);
        }

        [Test]
        public void WifiConnection_ResetGlobalState_ResetsBothInstanceAndGlobalFlag()
        {
            wifi.SetConnected(true);
            Assert.IsTrue(wifi.IsConnected);
            Assert.IsTrue(WifiConnection.GlobalIsConnected);

            WifiConnection.ResetGlobalState();

            Assert.IsFalse(wifi.IsConnected);
            Assert.IsFalse(WifiConnection.GlobalIsConnected);
        }

        [Test]
        public void WifiConnection_SettingGlobalFlag_SynchronizesWithInstance()
        {
            WifiConnection.GlobalIsConnected = true;
            Assert.IsTrue(wifi.IsConnected);

            WifiConnection.GlobalIsConnected = false;
            Assert.IsFalse(wifi.IsConnected);
        }
    }
}

