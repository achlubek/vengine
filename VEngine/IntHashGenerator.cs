﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VDGTech
{
    class IntHashGenerator
    {
        static int Counter = 0;
        public static int GetNext()
        {
            return Counter++;
        }
    }
}
