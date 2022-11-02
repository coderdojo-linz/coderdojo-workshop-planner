using System;
using System.Collections.Generic;
using System.Text;

using CDWPlanner.DTO;

namespace CDWPlanner.Helpers
{
    internal static class WorkshopHelpers
    {
        public static bool TimeHasChanged(Workshop oldWorkshop, Workshop newWorkshop)
        {
            oldWorkshop = oldWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");
            newWorkshop = newWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");

            return BeginTimeChanged(oldWorkshop, newWorkshop) || EndTimeChanged(oldWorkshop, newWorkshop);
        }

        public static bool EndTimeChanged(Workshop oldWorkshop, Workshop newWorkshop)
        {
            oldWorkshop = oldWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");
            newWorkshop = newWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");

            return oldWorkshop.endtimeAsShortTime != newWorkshop.endtimeAsShortTime;
        }

        public static bool BeginTimeChanged(Workshop oldWorkshop, Workshop newWorkshop)
        {
            oldWorkshop = oldWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");
            newWorkshop = newWorkshop ?? throw new ArgumentNullException($"{nameof(oldWorkshop)} cannot be null");

            return oldWorkshop.begintimeAsShortTime != newWorkshop.begintimeAsShortTime;
        }

        public static bool WorkshopChanged(Workshop oldWorkshop, Workshop newWorkshop)
        {
            // Workshop does not exist yet
            if (oldWorkshop == null)
            {
                return true;
            }

            // Event is not new and workshop is not new
            return newWorkshop.title != oldWorkshop.title
                   || newWorkshop.description != oldWorkshop.description
                   || newWorkshop.prerequisites != oldWorkshop.prerequisites
                   || newWorkshop.thumbnail != oldWorkshop.thumbnail
                   || TimeHasChanged(oldWorkshop, newWorkshop);
        }
    }
}