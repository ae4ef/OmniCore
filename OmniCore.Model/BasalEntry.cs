﻿using OmniCore.Model.Constants;
using System;
using System.Collections.Generic;
using System.Text;

namespace OmniCore.Model
{
    public class BasalEntry
    {
        public decimal Rate { get; }
        public TimeSpan StartOffset { get; }
        public TimeSpan EndOffset { get; }
        public BasalEntry(decimal hourlyRate, TimeSpan startOffsetFromMidnight, TimeSpan endOffsetFromMidnight)
        {
            if (hourlyRate < BasalConstants.MinimumRate)
                throw new ArgumentException($"Basal rate cannot be less than {BasalConstants.MinimumRate}");

            if (hourlyRate > BasalConstants.MaximumRate)
                throw new ArgumentException($"Basal rate cannot be more than {BasalConstants.MaximumRate}");

            if (hourlyRate % BasalConstants.RateIncrements != 0)
                throw new ArgumentException($"Basal rate must be increments of {BasalConstants.RateIncrements} units");

            if (startOffsetFromMidnight >= endOffsetFromMidnight)
                throw new ArgumentException("Start offset must come before end offset");

            if (!VerifyTimeBoundary(startOffsetFromMidnight))
                throw new ArgumentException($"Start offset must be at {BasalConstants.TimeIncrements} boundary.");

            if (!VerifyTimeBoundary(endOffsetFromMidnight))
                throw new ArgumentException($"End offset must be at {BasalConstants.TimeIncrements} boundary.");

            this.Rate = hourlyRate;
            this.StartOffset = startOffsetFromMidnight;
            this.EndOffset = endOffsetFromMidnight;
        }

        public bool VerifyTimeBoundary(TimeSpan ts)
        {
            return (ts.TotalSeconds % BasalConstants.TimeIncrements.TotalSeconds < 1);
        }
    }
}
