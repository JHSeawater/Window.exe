using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WindowExe.Desktop;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public sealed class StartScreenControllerTests
    {
        private GameObject rootGo;
        private StartScreenController controller;
        private TextMeshProUGUI tmpText;

        [SetUp]
        public void SetUp()
        {
            rootGo = new GameObject("TestStartScreen");
            controller = rootGo.AddComponent<StartScreenController>();

            var textGo = new GameObject("Text", typeof(TextMeshProUGUI));
            textGo.transform.SetParent(rootGo.transform);
            tmpText = textGo.GetComponent<TextMeshProUGUI>();
            tmpText.text = "PRESS ANY KEY";
            tmpText.color = Color.white;

            var bgGo = new GameObject("BG", typeof(Image));
            bgGo.transform.SetParent(rootGo.transform);
            var titleGo = new GameObject("Title", typeof(Image));
            titleGo.transform.SetParent(rootGo.transform);

            controller.Configure(bgGo.GetComponent<Image>(), titleGo.GetComponent<Image>(), tmpText, "Desktop");
        }

        [TearDown]
        public void TearDown()
        {
            if (rootGo != null)
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void CalculateBlinkT_ReturnsValueWithinZeroAndOne()
        {
            for (float t = 0f; t <= 5f; t += 0.1f)
            {
                var factor = controller.CalculateBlinkT(t);
                Assert.GreaterOrEqual(factor, 0f, $"Blink factor at time {t} was below 0");
                Assert.LessOrEqual(factor, 1f, $"Blink factor at time {t} was above 1");
            }
        }

        [Test]
        public void EvaluateTextColor_AlphaMode_InterpolatesAlphaProperly()
        {
            var darkColor = controller.EvaluateTextColor(0f);
            var brightColor = controller.EvaluateTextColor(1f);

            Assert.Less(darkColor.a, brightColor.a, "Dark alpha should be less than bright alpha.");
            Assert.AreEqual(1f, brightColor.a, 0.01f, "Max bright alpha should be 1.");
        }

        [Test]
        public void TriggerStart_SetsTransitioningAndInvokesEventOnce()
        {
            var eventCount = 0;
            controller.OnStartTriggered += () => eventCount++;

            Assert.IsFalse(controller.IsTransitioning);

            controller.TriggerStart();

            Assert.IsTrue(controller.IsTransitioning);
            Assert.AreEqual(1, eventCount);

            // Second trigger should be ignored
            controller.TriggerStart();
            Assert.AreEqual(1, eventCount, "Duplicate triggers should be ignored.");
        }
    }
}
