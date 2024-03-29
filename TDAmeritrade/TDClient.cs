﻿using Core;
using Core.Model;
using Core.Model.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TDAmeritrade.Authentication;
using TDAmeritrade.Model;
#nullable enable

namespace TDAmeritrade
{
    public class TDClient : MarketDataClient, IBrokerClient
    {
        public TDClient(TDAmeritradeConfig config)
        {
            AccountNumber = config.AccountNumber;
            Authenticator = new Authenticator(config.ConsumerKey, config.AuthInfoPath);
        }

        private AuthInfo AuthInfo => AuthInfo.Read(Authenticator.AuthInfoPath);
        private string AccessToken => AuthInfo.access_token;
        private Authenticator Authenticator { get; }
        private string AccountNumber { get; }

        public static IRestResponse ExecuteRequest(RestClient client, RestRequest request)
        {
            IRestResponse response = client.Execute(request);

            if (!response.IsSuccessful)
            {
                APIExceptions.ThrowAPIException(response);
            }

            return response;
        }

        public Position? GetPosition(string symbol)
        {
            if (OptionSymbolUtils.IsOptionSymbol(symbol))
            {
                OptionSymbolUtils.ValidateDateIsFormatAndInNearFuture(symbol, OptionSymbolUtils.StandardDateFormat);
            }

            IEnumerable<Position> positions = GetPositions();
            return positions.Where(pos => pos.Symbol == symbol).FirstOrDefault();
        }

        public IEnumerable<Position> GetPositions()
        {
            Account account = GetAccount();
            return account.SecuritiesAccount.Positions.Cast<Position>().ToList();
        }

        public override OptionQuote GetOptionQuote(string symbol)
        {
            if (!OptionSymbolUtils.IsOptionSymbol(symbol))
            {
                throw new ArgumentException("Provided symbol is not an option symbol: " + symbol);
            }
            string tdAmSymbol = OptionSymbolUtils.ConvertDateFormat(symbol, OptionSymbolUtils.StandardDateFormat, Constants.TDOptionDateFormat);

            RestClient client = new RestClient("https://api.tdameritrade.com/v1/marketdata/" + tdAmSymbol + "/quotes");
            RestRequest request = CreateRequest(Method.GET);
            IRestResponse response = ExecuteRequest(client, request);
            if (!response.IsSuccessful || response.Content.Contains("Symbol not found"))
            {
                throw new MarketDataException("Get quote unsuccessful for symbol " + symbol);
            }
            Regex responseRegex = new Regex("{\"assetType.*?}");
            Match match = responseRegex.Match(response.Content);
            OptionQuote quote = JsonConvert.DeserializeObject<TDOptionQuote>(match.Value);
            return quote;
        }

        public override bool IsMarketOpenToday()
        {
            RestClient client = new RestClient("https://api.tdameritrade.com/v1/marketdata/OPTION/hours");
            RestRequest request = CreateRequest(Method.GET);
            IRestResponse response = ExecuteRequest(client, request);
            return response.Content.Contains("\"isOpen\":true");
        }

        public IEnumerable<Order> GetOpenOrdersForSymbol(string symbol)
        {
            return GetOpenOrderBodiesForSymbol(symbol)
                .Select(body => body.ToOrder());
        }

        public float GetAvailableFundsForTrading()
        {
            Account account = GetAccount();
            return account.SecuritiesAccount.CurrentBalances.AvailableFundsNonMarginableTrade;
        }

        public void PlaceOrder(Order order)
        {
            RestClient client = new RestClient("https://api.tdameritrade.com/v1/accounts/" + AccountNumber + "/orders");
            RestRequest request = CreateRequest(Method.POST);
            string body = CreateOrderBodyString(order);
            request.AddJsonBody(body);
            IRestResponse response = ExecuteRequest(client, request);
            Log.Information("Response: {@Response}", response);

            FetchedOrderBody fetchedOrderBody = FetchOrderBody(order);
            if (fetchedOrderBody.Status == OrderStatus.REJECTED)
            {
                TDClientException ex = new TDClientException("Order rejected");
                Log.Error(ex, "Order rejected: Order {@Order}, OrderBody {@OrderBody}", order, fetchedOrderBody);
                throw ex;
            }
            else if (fetchedOrderBody.IsOpenOrder && 
                order.CancelTime != null)
            {
                CancelOrderAtCancelTime(fetchedOrderBody.OrderId, (DateTime)order.CancelTime);
            }
        }

        public void CancelExistingBuyOrders(string symbol)
        {
            IEnumerable<FetchedOrderBody> openOrders = GetOpenOrderBodiesForSymbol(symbol);
            foreach(FetchedOrderBody body in openOrders.Where(body => body.Instruction == InstructionType.BUY_TO_OPEN))
            {
                CancelOrder(body.OrderId);
            }
        }

        public IEnumerable<Order> GetOrders()
        {
            IEnumerable<FetchedOrderBody> bodies = GetOrderBodies();
            return bodies.Select(body => body.ToOrder());
        }

        private void CancelOrderAtCancelTime(string orderId, DateTime cancelTime)
        {
            bool IsOrderOpen() => FetchOrderBody(orderId).IsOpenOrder;
            void DeleteOrder() => CancelOrder(orderId);
            new DelayedActionThread(IsOrderOpen, DeleteOrder, cancelTime, 60 * 1000).Run();
        }

        private FetchedOrderBody FetchOrderBody(Order order)
        {
            IEnumerable<FetchedOrderBody> orderBodies = GetOrderBodies()
                .Where(body => body.Symbol == order.Symbol)
                .OrderByDescending(body => body.EnteredDateTime);

            FetchedOrderBody? thisOrderBody = orderBodies.FirstOrDefault(body => body.ToOrder().Equals(order));

            if (thisOrderBody == null)
            {
                TDClientException ex = new TDClientException("Could not fetch order body from broker");
                Log.Error(ex, "Could not fetch order body from broker. Order {@Order}", order);
                throw ex;
            }
            else if (thisOrderBody != orderBodies.First())
            {
                Log.Warning("Fetched order body is not the most recent order for this symbol. Order {@Order}, OrderBody {@OrderBody}", order, thisOrderBody);
            }
            return thisOrderBody;
        }

        private FetchedOrderBody FetchOrderBody(string orderId)
        {
            RestClient client = new RestClient("https://api.tdameritrade.com/v1/accounts/" + AccountNumber + "/orders/" + orderId);
            RestRequest request = CreateRequest(Method.GET);
            ExecuteRequest(client, request);
            IRestResponse response = ExecuteRequest(client, request);
            return JsonConvert.DeserializeObject<FetchedOrderBody>(response.Content);
        }

        private IEnumerable<FetchedOrderBody> GetOpenOrderBodiesForSymbol(string symbol)
        {
            return GetOrderBodies()
                .Where(body => body.Symbol == symbol && body.IsOpenOrder);
        }

        private void CancelOrder(string orderId)
        {
            RestClient client = new RestClient("https://api.tdameritrade.com/v1/accounts/" + AccountNumber + "/orders/" + orderId);
            RestRequest request = CreateRequest(Method.DELETE);
            ExecuteRequest(client, request);
        }

        private IEnumerable<FetchedOrderBody> GetOrderBodies()
        {
            RestClient client = new RestClient("https://api.tdameritrade.com/v1/accounts/" + AccountNumber + "/orders");
            RestRequest request = CreateRequest(Method.GET);
            IRestResponse response = ExecuteRequest(client, request);
            return JsonConvert.DeserializeObject<List<FetchedOrderBody>>(response.Content);
        }

        private Account GetAccount()
        {
            RestClient client = new RestClient("https://api.tdameritrade.com/v1/accounts/" + AccountNumber);
            RestRequest request = CreateRequest(Method.GET);
            request.AddParameter("fields", "positions");
            IRestResponse response = ExecuteRequest(client, request);
            return JsonConvert.DeserializeObject<Account>(response.Content);
        }

        private RestRequest CreateRequest(Method method)
        {
            Authenticator.Authenticate();
            RestRequest request = new RestRequest(method);
            request.AddHeader("Authorization", "Bearer " + AccessToken);
            return request;
        }

        private static string CreateOrderBodyString(Order order)
        {
            Instrument instrument = new Instrument(order.Symbol, AssetType.OPTION, OptionSymbolUtils.StandardDateFormat);
            OrderLeg orderLeg = new OrderLeg(order.Instruction, order.Quantity, instrument);
            List<OrderLeg> orderLegCollection = new List<OrderLeg>();
            orderLegCollection.Add(orderLeg);
            string? priceStr = null;
            if (order.OrderType == OrderType.LIMIT)
            {
                double doublePrice = Math.Round(order.Limit, 2);
                priceStr = doublePrice.ToString();
            }

            OrderBody orderBody = new OrderBody(
                "NONE",
                order.OrderType,
                "NORMAL",
                priceStr,
                order.CancelTime == null ? "DAY" : "GOOD_TILL_CANCEL",
                "SINGLE",
                orderLegCollection);

            string orderBodyStr = JsonConvert.SerializeObject(orderBody, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            });
            Log.Information("TDAm Order: {@Order}, string: {OrderStr}, Symbol {Symbol}", orderBody, orderBodyStr, instrument.Symbol);
            return orderBodyStr;
        }
    }
}
