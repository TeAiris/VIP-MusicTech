using System;

namespace VIP.MusicTech.Audio
{
    /// <summary>Time-bucketed min/max history; memory depends on texture width, not song length.</summary>
    public sealed class AudioWaveformHistory
    {
        private readonly float[] minima, maxima;
        private readonly long[] buckets;
        private readonly double bucketSeconds;
        public double TimeSeconds { get; private set; }
        public int Width => buckets.Length;

        public AudioWaveformHistory(int width, double windowSeconds)
        {
            if (width < 1 || double.IsNaN(windowSeconds) || double.IsInfinity(windowSeconds) || windowSeconds <= 0)
                throw new ArgumentOutOfRangeException();
            minima = new float[width];
            maxima = new float[width];
            buckets = new long[width];
            for (int i = 0; i < width; i++) buckets[i] = -1;
            bucketSeconds = windowSeconds / width;
        }

        public void Advance(double timeSeconds) { TimeSeconds = Math.Max(TimeSeconds, timeSeconds); }
        public void Add(double timeSeconds, float minimum, float maximum)
        {
            if (timeSeconds < 0) return;
            long bucket = (long)Math.Floor(timeSeconds / bucketSeconds);
            int slot = (int)(bucket % Width);
            if (buckets[slot] != bucket)
            {
                buckets[slot] = bucket;
                minima[slot] = minimum;
                maxima[slot] = maximum;
            }
            else
            {
                minima[slot] = Math.Min(minima[slot], minimum);
                maxima[slot] = Math.Max(maxima[slot], maximum);
            }
            Advance(timeSeconds);
        }

        public bool ReadAge(int columnsAgo, out float minimum, out float maximum)
        {
            minimum = maximum = 0;
            if (columnsAgo < 0 || columnsAgo >= Width) return false;
            long bucket = (long)Math.Floor(TimeSeconds / bucketSeconds) - columnsAgo;
            if (bucket < 0) return false;
            int slot = (int)(bucket % Width);
            if (buckets[slot] != bucket) return false;
            minimum = minima[slot];
            maximum = maxima[slot];
            return true;
        }

        public bool ReadColumn(int column, bool rightToLeft, out float minimum, out float maximum)
            => ReadAge(rightToLeft ? Width - 1 - column : column, out minimum, out maximum);
    }
}
