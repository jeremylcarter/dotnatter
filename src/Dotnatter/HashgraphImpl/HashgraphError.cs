﻿using System;
using System.Runtime.Serialization;

namespace Dotnatter.Common
{
    public class HashgraphError : ApplicationException
    {
        public HashgraphError()
        {
        }

        public HashgraphError(string message) : base(message)
        {
        }

        public HashgraphError(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HashgraphError(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
