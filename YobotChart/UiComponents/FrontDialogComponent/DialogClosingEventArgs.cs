﻿using System;

namespace YobotChart.UiComponents.FrontDialogComponent
{
    public class DialogClosingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
    }
}