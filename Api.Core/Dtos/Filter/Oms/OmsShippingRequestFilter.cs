﻿using System;

namespace Api.Core.Dtos.Filter.Oms
{
    public class OmsShippingRequestFilter
    {
        public string Shipment { get; set; }

        public int? StateId { get; set; } 

        public int? ClientId { get; set; }

        public DateTime? StateDateFrom { get; set; }

        public DateTime? StateDateTo { get; set; }

        public int? DeliveryMode { get; set; }

    }
}
