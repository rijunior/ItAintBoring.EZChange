﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItAintBoring.EZChange.Common.Packaging
{
    public class Solution
    {
        public string Name { get; set; }

        public List<IAction> PreImportActions;
        public List<IAction> PostImportActions;
    }
}