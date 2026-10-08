using NUnit.Framework;

namespace VIP.MusicTech.Audio.Tests
{
    public sealed class AudioVisualizationTests
    {
        [Test]
        public void HistoryPreservesExtremaAndScrollsByElapsedTime()
        {
            var history = new AudioWaveformHistory(4, 4);
            history.Add(0.2, -0.2f, 0.5f);
            history.Add(0.8, -0.4f, 0.3f);
            history.Advance(1.2);
            Assert.IsTrue(history.ReadAge(1, out float minimum, out float maximum));
            Assert.AreEqual(-0.4f, minimum);
            Assert.AreEqual(0.5f, maximum);
            Assert.IsFalse(history.ReadAge(0, out _, out _));
            history.Advance(4.2);
            for (int age = 0; age < 4; age++) Assert.IsFalse(history.ReadAge(age, out _, out _));
        }

        [Test]
        public void DirectionPlacesNewestAudioAtTheCorrectEdge()
        {
            var history = new AudioWaveformHistory(4, 4);
            history.Add(2.2, -0.8f, 0.9f);
            Assert.IsTrue(history.ReadColumn(3, true, out _, out _));
            Assert.IsFalse(history.ReadColumn(0, true, out _, out _));
            Assert.IsTrue(history.ReadColumn(0, false, out _, out _));
            Assert.IsFalse(history.ReadColumn(3, false, out _, out _));
        }

        [Test]
        public void WindowSizeDeterminesHistoryRetention()
        {
            var shortWindow = new AudioWaveformHistory(4, 1);
            var longWindow = new AudioWaveformHistory(4, 4);
            shortWindow.Add(0.1, -1, 1);
            longWindow.Add(0.1, -1, 1);
            shortWindow.Advance(1.1);
            longWindow.Advance(1.1);
            Assert.IsFalse(shortWindow.ReadAge(3, out _, out _));
            Assert.IsTrue(longWindow.ReadAge(1, out _, out _));
        }

        [Test]
        public void MeterUsesConfiguredFloorAndCeilingAndClampsOutsideRange()
        {
            Assert.AreEqual(0, AudioMeterView.NormalizeDecibels(-80, -48, -6));
            Assert.AreEqual(0.5f, AudioMeterView.NormalizeDecibels(-27, -48, -6), 0.0001f);
            Assert.AreEqual(1, AudioMeterView.NormalizeDecibels(0, -48, -6));
        }
    }
}
