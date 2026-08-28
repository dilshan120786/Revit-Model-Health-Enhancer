using System.Collections.Generic;
using System.Linq;

namespace RevitHealthEnhancer.Core
{
    public sealed class HealthImproverOptions
    {
        public bool PinControlElements { get; set; }
        public bool FixLevelGridWorksets { get; set; }
        public bool DeleteInvalidRooms { get; set; }
        public bool DeleteInvalidSpaces { get; set; }
        public bool DeleteUnusedViewTemplates { get; set; }
        public bool DeleteUnusedFilters { get; set; }
        public bool DeleteUnusedTextStyles { get; set; }
        public bool FixDuplicateMarks { get; set; }
        public bool DeleteDuplicateInstances { get; set; }
        public bool EnsureNavisworkView { get; set; }

        public static HealthImproverOptions AllEnabled()
        {
            return new HealthImproverOptions
            {
                PinControlElements = true,
                FixLevelGridWorksets = true,
                DeleteInvalidRooms = true,
                DeleteInvalidSpaces = true,
                DeleteUnusedViewTemplates = true,
                DeleteUnusedFilters = true,
                DeleteUnusedTextStyles = true,
                FixDuplicateMarks = true,
                DeleteDuplicateInstances = true,
                EnsureNavisworkView = true
            };
        }

        public bool HasAnySelectedAction()
        {
            return GetSelectedActionNames().Any();
        }

        public IList<string> GetSelectedActionNames()
        {
            List<string> actions = new List<string>();

            if (PinControlElements)
            {
                actions.Add("Pin levels, grids, Revit links and CAD links");
            }

            if (FixLevelGridWorksets)
            {
                actions.Add("Move levels and grids to Shared Levels and Grids workset");
            }

            if (DeleteInvalidRooms)
            {
                actions.Add("Delete invalid rooms");
            }

            if (DeleteInvalidSpaces)
            {
                actions.Add("Delete invalid MEP spaces");
            }

            if (DeleteUnusedViewTemplates)
            {
                actions.Add("Delete unused view templates");
            }

            if (DeleteUnusedFilters)
            {
                actions.Add("Delete unused filters");
            }

            if (DeleteUnusedTextStyles)
            {
                actions.Add("Delete unused text styles");
            }

            if (FixDuplicateMarks)
            {
                actions.Add("Fix duplicate Mark warnings");
            }

            if (DeleteDuplicateInstances)
            {
                actions.Add("Delete safe duplicate instances");
            }

            if (EnsureNavisworkView)
            {
                actions.Add("Create Naviswork export 3D view if missing");
            }

            return actions;
        }
    }
}
