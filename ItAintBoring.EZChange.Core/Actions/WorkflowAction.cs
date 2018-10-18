﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ItAintBoring.EZChange.Common;
using ItAintBoring.EZChange.Core.UI;

namespace ItAintBoring.EZChange.Core.Actions
{
    public class WorkflowAction : IAction
    {
        public override string ToString()
        {
            return Name;
        }
        public string Title { get; set; }
        public string XML { get; set; }
        public string Name { get { return "Workflow Action"; } }

        public string Description { get { return "Run Workflow"; } }

        private UserControl uiControl = new XMLEditor();
        public UserControl UIControl { get { return uiControl; } }

        public void ApplyUIUpdates()
        {
            throw new NotImplementedException();
        }

        public void DoAction()
        {
            throw new NotImplementedException();
        }
    
    }
}
