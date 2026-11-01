using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class GameOverSceneControllerTests
    {
        private GameObject controllerGo;
        private GameOverSceneController controller;

        [SetUp]
        public void SetUp()
        {
            controllerGo = new GameObject("GameOverSceneController");
            controller = controllerGo.AddComponent<GameOverSceneController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (controllerGo != null)
            {
                Object.DestroyImmediate(controllerGo);
            }
            ImageWindow.ResetGlobalExtractionState();
        }

        [Test]
        public void DefaultValuesMatchSpecification()
        {
            Assert.AreEqual("Desktop", controller.RestartSceneName);
            Assert.AreEqual(0.25f, controller.InputDelay, 0.001f);
            Assert.IsFalse(controller.IsRestarting);
        }

        [Test]
        public void RestartDesktopScene_ResetsGlobalExtractionStateAndFlags()
        {
            // 와이파이를 true로 설정 후 리셋 테스트
            WifiConnection.GlobalIsConnected = true;

            controller.RestartDesktopScene();

            Assert.IsTrue(controller.IsRestarting);
            Assert.IsFalse(ImageWindow.IsKeyGloballyExtracted);
            Assert.IsFalse(WifiConnection.GlobalIsConnected);
        }
    }
}

