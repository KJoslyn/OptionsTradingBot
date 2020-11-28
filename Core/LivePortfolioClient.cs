﻿using Core.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#nullable enable

namespace Core
{
    public abstract class LivePortfolioClient
    {
        public LivePortfolioClient(PortfolioDatabase database, IMarketDataClient marketDataClient)
        {
            Database = database;
            MarketDataClient = marketDataClient;

            //Log.Information("INSERTING POSITIONS");
            //Position pos1 = new Position("ACB_201127C7", 50, (float)0.55);
            //database.InsertPosition(pos1);
            //Position pos2 = new Position("ACB_201218C10", 100, (float)0.34);
            //database.InsertPosition(pos2);
            //Position pos3 = new Position("ACB_201218C7", 50, (float)1.14);
            //database.InsertPosition(pos3);
            //Position pos4 = new Position("ACB_201218C8", 50, (float)0.70);
            //database.InsertPosition(pos4);
            //Position pos5 = new Position("ACB_210115C10", 20, (float)0.76);
            //database.InsertPosition(pos5);
            //Position pos6 = new Position("FSLR_201127C87", 20, (float)1.34);
            //database.InsertPosition(pos6);
            //Position pos7 = new Position("SPWR_201204C21", 20, (float)1.50);
            //database.InsertPosition(pos7);

            //Position pos1 = new Position("NET_201127C65", 10, (float)2.62);
            //database.InsertPosition(pos1);
            //Position pos2 = new Position("OSTK_201127C65", 20, (float)1.07);
            //database.InsertPosition(pos2);
            //Position pos3 = new Position("SPWR_201127C20", 50, (float)1.05);
            //database.InsertPosition(pos3);
            //Position pos4 = new Position("TSLA_201127C500", 1, (float)16.14);
            //database.InsertPosition(pos4);

            //List<FilledOrder> orders = new List<FilledOrder>();
            //FilledOrder o1 = new FilledOrder("CGC_201120C25", (float)0.59, InstructionType.SELL_TO_CLOSE, OrderType.MARKET, 0, 30, new System.DateTime(2020, 11, 17, 10, 11, 56));
            //orders.Add(o1);
            //FilledOrder o2 = new FilledOrder("SFIX_201120C39", (float)0.30, InstructionType.SELL_TO_CLOSE, OrderType.MARKET, 0, 5, new System.DateTime(2020, 11, 13, 12, 23, 37));
            //orders.Add(o2);
            //FilledOrder o3 = new FilledOrder("SPWR_201120C20", (float)0.55, InstructionType.SELL_TO_CLOSE, OrderType.MARKET, 0, 20, new System.DateTime(2020, 11, 13, 12, 31, 13));
            //orders.Add(o3);
            //FilledOrder o4 = new FilledOrder("SFIX_201120C39", (float)0.22, InstructionType.SELL_TO_CLOSE, OrderType.MARKET, 0, 50, new System.DateTime(2020, 11, 13, 12, 33, 25));
            //orders.Add(o4);
            //FilledOrder o5 = new FilledOrder("SPWR_201120C20", (float)0.57, InstructionType.BUY_TO_OPEN, OrderType.MARKET, 0, 30, new System.DateTime(2020, 11, 13, 12, 35, 39));
            //orders.Add(o5);
            //FilledOrder o6 = new FilledOrder("CGC_201120C24", (float)1.16, InstructionType.SELL_TO_CLOSE, OrderType.MARKET, 0, 20, new System.DateTime(2020, 11, 13, 12, 53, 26));
            //orders.Add(o6);

            //database.InsertOrders(orders);
        }

        protected PortfolioDatabase Database { get; init; }

        protected IMarketDataClient MarketDataClient { get; init; }

        public abstract Task<bool> Login();

        public abstract Task<bool> Logout();

        public abstract Task<bool> HavePositionsChanged(bool? groundTruthChanged);

        public abstract Task<bool> HaveOrdersChanged(bool? groundTruthChanged);

        protected abstract Task<UnvalidatedLiveOrdersResult> RecognizeLiveOrders();

        protected abstract Task<UnvalidatedLiveOrdersResult> RecognizeLiveOrders(string ordersFilename);

        // This does not update the database, but the method is not public.
        protected abstract Task<IList<Position>> RecognizeLivePositions();

        public async Task<LiveDeltasResult> GetLiveDeltasFromOrders()
        {
            UnvalidatedLiveOrdersResult unvalidatedLiveOrdersResult = await RecognizeLiveOrders();
            Dictionary<FilledOrder, OptionQuote> validatedLiveOrders = ValidateLiveOrders(unvalidatedLiveOrdersResult.LiveOrders);

            TimeSortedSet<PositionDelta> liveDeltas = new TimeSortedSet<PositionDelta>();
            if (validatedLiveOrders.Keys.Count > 0)
            {
                // TODO: Don't hardcode lookback
                NewAndUpdatedFilledOrders result = Database.IdentifyNewAndUpdatedOrders(new TimeSortedSet<FilledOrder>(validatedLiveOrders.Keys), 10);
                Database.UpdateOrders(result.UpdatedFilledOrders);
                liveDeltas = Database.ComputeDeltasAndUpdateTables(result.NewFilledOrders);
            }
            Dictionary<string, OptionQuote> quotes = validatedLiveOrders.ToDictionary(obj => obj.Key.Symbol, obj => obj.Value);

            return new LiveDeltasResult(liveDeltas, quotes, unvalidatedLiveOrdersResult.SkippedOrderDueToLowConfidence);
        }

        private Dictionary<FilledOrder, OptionQuote> ValidateLiveOrders(IEnumerable<FilledOrder> liveOrders)
        {
            Dictionary<FilledOrder, OptionQuote> validOrdersAndQuotes = new Dictionary<FilledOrder, OptionQuote>();
            foreach (FilledOrder order in liveOrders)
            {
                OptionQuote quote;
                try
                {
                    quote = MarketDataClient.GetOptionQuote(order.Symbol);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error getting quote for symbol {Symbol}", order.Symbol);
                    continue;
                }

                if (order.Price < quote.LowPrice ||
                    order.Price > quote.HighPrice)
                {
                    Log.Warning("Order price not within day's range- symbol {Symbol}, order {@Order}, quote {@Quote}", order.Symbol, order, quote);
                }
                else {
                    validOrdersAndQuotes.Add(order, quote);
                }
            }
            return validOrdersAndQuotes;
        }

        public async Task<LiveDeltasResult> GetLiveDeltasFromOrders(string ordersFilename)
        {
            UnvalidatedLiveOrdersResult unvalidatedLiveOrdersResult = await RecognizeLiveOrders(ordersFilename);
            Dictionary<FilledOrder, OptionQuote> validatedLiveOrders = ValidateLiveOrders(unvalidatedLiveOrdersResult.LiveOrders);

            TimeSortedSet<PositionDelta> liveDeltas = new TimeSortedSet<PositionDelta>();
            if (validatedLiveOrders.Keys.Count > 0)
            {
                // TODO: Don't hardcode lookback
                NewAndUpdatedFilledOrders result = Database.IdentifyNewAndUpdatedOrders(new TimeSortedSet<FilledOrder>(validatedLiveOrders.Keys), 10);
                Database.UpdateOrders(result.UpdatedFilledOrders);
                liveDeltas = Database.ComputeDeltasAndUpdateTables(result.NewFilledOrders);
            }
            Dictionary<string, OptionQuote> quotes = validatedLiveOrders.ToDictionary(obj => obj.Key.Symbol, obj => obj.Value);

            return new LiveDeltasResult(liveDeltas, quotes, unvalidatedLiveOrdersResult.SkippedOrderDueToLowConfidence);
        }

        // This does update the database so that the deltas remain accurate.
        // May throw InvalidPortfolioStateException if the portfolio is not in a valid state
        // (The portfolio may be offline, or its format may have changed.)
        public async Task<IList<PositionDelta>> GetLiveDeltasFromPositions()
        {
            IList<Position> livePositions = await RecognizeLivePositions();
            return Database.ComputeDeltasAndUpdateTables(livePositions);
        }

        //// This does update the database so that the deltas remain accurate.
        //// May throw InvalidPortfolioStateException if the portfolio is not in a valid state
        //// (The portfolio may be offline, or its format may have changed.)
        //public async Task<(IList<Position>, IList<PositionDelta>)> GetLivePositionsAndDeltas()
        //{
        //    IList<Position> livePositions = await RecognizeLivePositions();
        //    IList<PositionDelta> deltas = database.ComputePositionDeltas(livePositions);
        //    database.UpdatePositionsAndDeltas(livePositions, deltas);
        //    return (livePositions, deltas);
        //}

        //// This does update the database so that the deltas remain accurate.
        //// May throw InvalidPortfolioStateException if the portfolio is not in a valid state
        //// (The portfolio may be offline, or its format may have changed.)
        //public async Task<(IList<Position>, IList<PositionDelta>)> GetLivePositionsAndDeltas(IList<PositionDelta> deltas)
        //{
        //    IList<Position> livePositions = await GetLivePositions();
        //    IList<PositionDelta> deltas = database.ComputePositionDeltas(livePositions);
        //    database.UpdatePositionsAndDeltas(null, deltas);
        //    return (null, deltas);
        //}
    }
}
