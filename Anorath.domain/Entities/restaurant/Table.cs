using System;
using System.Collections.Generic;
using System.Text;

namespace Anorath.domain.Entities.restaurant
{
    internal class Table
    {
        public int TableId { get; set; }

        public string TableName { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public bool IsAvailable { get; set; } = true;
    }
}

      