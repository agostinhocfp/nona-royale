// Assets/Tests/EditMode/Unity/HoldRelayTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The press on an ability card (CAST_ONBOARDING.md CO3): a quick tap is
    /// reported as a tap and still clicks; leaving the card cancels it. The
    /// hold itself runs on unscaled time and is checked in Play Mode.
    /// </summary>
    [TestFixture]
    public class HoldRelayTests
    {
        private GameObject _go;
        private HoldRelay _relay;
        private int _taps;
        private int _holds;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("card", typeof(RectTransform));
            _taps = 0;
            _holds = 0;
            _relay = HoldRelay.On(_go.transform, () => _holds++, () => _taps++);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void AQuickTap_IsATap_AndStaysEligibleForTheClick()
        {
            var press = Press();
            _relay.OnPointerDown(press);
            Assert.IsTrue(_relay.Pressed);

            _relay.OnPointerUp(press);

            Assert.AreEqual(1, _taps);
            Assert.AreEqual(0, _holds);
            Assert.IsTrue(press.eligibleForClick);
            Assert.IsFalse(_relay.Pressed);
        }

        [Test]
        public void LeavingTheCard_CancelsThePress()
        {
            var press = Press();
            _relay.OnPointerDown(press);
            _relay.OnPointerExit(press);
            _relay.OnPointerUp(press);

            Assert.AreEqual(0, _taps);
            Assert.IsFalse(_relay.Pressed);
        }

        [Test]
        public void ARightClick_IsIgnored()
        {
            var press = Press();
            press.button = PointerEventData.InputButton.Right;

            _relay.OnPointerDown(press);
            _relay.OnPointerUp(press);

            Assert.AreEqual(0, _taps);
        }

        [Test]
        public void On_ReusesTheRelay_AndReplacesItsCallbacks()
        {
            var again = HoldRelay.On(_go.transform, null, null);

            Assert.AreSame(_relay, again);
            Assert.IsNull(again.Tapped);
        }

        private static PointerEventData Press() => new PointerEventData(null)
        {
            button = PointerEventData.InputButton.Left,
            pointerId = 0,
            eligibleForClick = true,
        };
    }
}
