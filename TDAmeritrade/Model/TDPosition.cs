﻿using Core;
using Core.Model;

namespace TDAmeritrade.Model
{
    internal class TDPosition : Position
    {
        public TDPosition(string symbol, float longQuantity, float averagePrice) : base(symbol, longQuantity, averagePrice) { }

        public float ShortQuantity { get; init; }
        public Instrument Instrument { get; init; }
        public float MarketValue { get; init; }
        public override string Symbol
        {
            get
            {
                string instrumentSymbol = Instrument.Symbol;
                return OptionSymbolUtils.IsOptionSymbol(instrumentSymbol)
                    ? OptionSymbolUtils.ConvertToStandardDateFormatIfNecessary(Instrument.Symbol, Constants.TDOptionDateFormat)
                    : instrumentSymbol;
            }
        }
    }
}
