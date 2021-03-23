using System;
using System.Collections.Generic;
using System.Text;
using CDWPlanner.DTO;
using Microsoft.Azure.Amqp.Framing;

namespace CDWPlanner.Model
{
    public class WorkshopChangedEvent
    {
        public Workshop OldWorkshop { get; }
        public Workshop NewWorkshop { get; }

        public WorkshopChangedEvent(Workshop oldWorkshop, Workshop newWorkshop)
        {
            OldWorkshop = oldWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");
            NewWorkshop = newWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");
        }

        public bool BeginTimeChanged => OldWorkshop.begintimeAsShortTime != NewWorkshop.begintimeAsShortTime;
        public bool EndTimeChanged => OldWorkshop.endtimeAsShortTime != NewWorkshop.endtimeAsShortTime;

        public bool TimeHasChanged => BeginTimeChanged || EndTimeChanged;
    }
}
