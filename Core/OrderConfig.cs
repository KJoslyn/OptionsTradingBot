﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class OrderConfig
    {
        public float LowBuyThreshold { get; init; }
        public float HighBuyThreshold { get; init; }
        public float LowBuyLimit { get; init; }
        public float HighBuyLimit { get; init; }
        public string LowBuyStrategy { get; init; } // One of the BuyStrategyType constants
        public string HighBuyStrategy { get; init; } // One of the BuyStrategyType constants
        public float MyPositionMaxSize { get; init; }
        public float LivePortfolioPositionMaxSize { get; init; }
        public int MinutesUntilBuyOrderExpires { get; init; }
        public int MinutesUntilWarnOldSellOrder { get; init; }
        public double MaxBuyPrice { get; init; }
        public int MaxNumOpenBuyOrdersForSymbol { get; init; }
        public float MinAvailableFundsForTrading { get; init; }
    }
}
