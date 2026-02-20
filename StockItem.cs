using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderProcessingSystem
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal WeightPerUnit { get; set; }

        public int Quantity { get; set; }
    }
}
