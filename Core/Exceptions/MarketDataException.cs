﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class MarketDataException : Exception
    {
        public MarketDataException(string message) : base(message) { }
    }
}
