﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Model
{
    public class LiveOrdersResult
    {
        public LiveOrdersResult(TimeSortedSet<FilledOrder> liveOrders, bool skippedOrderDueToLowConfidence)
        {
            LiveOrders = liveOrders;
            SkippedOrderDueToLowConfidence = skippedOrderDueToLowConfidence;
        }

        public TimeSortedSet<FilledOrder> LiveOrders { get; }
        public bool SkippedOrderDueToLowConfidence { get; }
    }
}
